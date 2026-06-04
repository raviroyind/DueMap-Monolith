# DueMap — Application Overview & Flow

> A team-discussion reference describing what DueMap does today, how a property
> manager moves through it, where the nightly worker fits, and how the billing
> engine sits in the middle. Diagrams are [Mermaid](https://mermaid.js.org/) —
> they render on GitHub, in VS Code (with a Mermaid extension), and in most
> Markdown viewers.

---

## 1. What DueMap is

DueMap automates **rent reminders, state-compliant late fees, and tenant
notifications** on top of the accounting system a property manager (PM) already
uses — **QuickBooks Online** or **Xero**. The PM never migrates their books; we
read customers + invoices each day and act on them.

**The core promise:** "Connect your accounting system once. We handle the
nightly busywork — reminding tenants before rent is due, assessing
legally-compliant late fees when it isn't paid, and emailing you a daily
summary."

### Who uses it

| Persona | What they do in DueMap |
| --- | --- |
| **Property Manager (PM)** | Connects QBO/Xero, sets reminder + late-fee preferences, reviews the daily close. The primary customer. |
| **Tenant** | Receives reminder / late-fee emails; can sign into a lightweight portal to view balance, pay, and download invoices. |
| **Platform admin (us)** | Monitors the worker via a Basic-Auth-gated log viewer + the Hangfire dashboard. |

---

## 2. Module architecture

DueMap is a modular monolith — one solution, clean module boundaries, two
runnable processes (Web + Worker) that load the **same** module set so behavior
is identical regardless of which process calls a service.

```mermaid
graph TD
    subgraph Processes
        WEB["DueMap.Web<br/>(Blazor Server + Razor Pages)"]
        WORKER["DueMap.Worker<br/>(Hangfire nightly cron)"]
    end

    subgraph Modules
        IDENTITY["DueMap.Identity<br/>users, auth, claims"]
        TENANCY["DueMap.Tenancy<br/>PMs, customers, leases,<br/>invoices, notice prefs,<br/>onboarding, tenant portal"]
        RULES["DueMap.Rules<br/>state late-fee rules engine"]
        NOTICES["DueMap.Notices<br/>templates + Scriban rendering"]
        BILLING["DueMap.Billing<br/>policy merge, planner,<br/>orchestrator, late-fee assess,<br/>daily close report"]
        INTEGRATIONS["DueMap.Integrations<br/>QBO/Xero OAuth + sync,<br/>SendGrid, Twilio, PDF fetch,<br/>email chrome"]
    end

    subgraph External
        QBO["QuickBooks Online / Xero"]
        SG["SendGrid (email)"]
        TW["Twilio (SMS)"]
        DB[("SQL Server")]
    end

    WEB --> IDENTITY & TENANCY & RULES & NOTICES & BILLING & INTEGRATIONS
    WORKER --> TENANCY & RULES & NOTICES & BILLING & INTEGRATIONS
    BILLING --> RULES & NOTICES & TENANCY
    INTEGRATIONS --> QBO & SG & TW
    TENANCY --> DB
    INTEGRATIONS --> DB
    BILLING --> DB
    NOTICES --> DB
    IDENTITY --> DB
```

**Key boundary rules**

- **Billing** is the brain. It merges policy, plans actions, and executes them — but it never talks to QBO/Xero or SendGrid directly. It calls **Integrations** (for dispatch + invoice PDF) and **Notices** (for template rendering).
- **Integrations** owns every third-party adapter. Swapping SendGrid for SES, or adding a new accounting provider, touches only this module.
- **Tenancy** owns the domain data (PMs, customers, leases, invoices) and is the only module that writes those tables.
- Web + Worker share the module list. Anything the worker does at midnight, the web app could trigger on demand (and does, for the onboarding sync).

---

## 3. Sign-up → setup → first notification (the PM journey)

There are **two entry paths** into onboarding, both landing in the same
multi-step wizard.

```mermaid
flowchart TD
    START([PM lands on /Account/Register or /Account/Login])

    START --> CHOICE{How do they sign up?}

    CHOICE -->|Email + password| EMAILREG["Create account<br/>(email/password)<br/>→ welcome email sent"]
    CHOICE -->|Sign in with Intuit| INTUIT["QBO OAuth flow<br/>exchange code → tokens<br/>read CompanyInfo email+name<br/>provision user + PM<br/>+ accounting connection"]

    EMAILREG --> KIOSK
    INTUIT -->|accounting already connected,<br/>step 1 auto-completed| STEP2

    KIOSK{{"Kiosk router:<br/>resume at first<br/>incomplete step"}}

    KIOSK --> STEP1

    subgraph Onboarding["Multi-step onboarding wizard (kiosk mode — app locked until complete)"]
        STEP1["STEP 1 · Connect Accounting<br/>QuickBooks or Xero OAuth<br/>→ initial sync of customers + invoices<br/>→ status: Connected → Synced"]
        STEP2["STEP 2 · Daily Close Settings<br/>timezone (auto-detected from QBO/Xero),<br/>send hour, recipient + CC"]
        STEP3["STEP 3 · Notice Preferences<br/>master toggles: pre-due / due-date / post-due,<br/>grace days, edit email templates"]
        STEP4["STEP 4 · Notify Tenants (optional)<br/>paged customer grid → select → send<br/>one-time preflight intro email"]

        STEP1 --> STEP2 --> STEP3 --> STEP4
    end

    STEP4 -->|MarkPreflightDone| ACTIVE["Status → Active (4)<br/>worker will process this PM<br/>at next local midnight"]
    ACTIVE --> DASH([Dashboard unlocked])

    DASH -.daily, automatically.-> WORKER[["Nightly worker pipeline<br/>(see §4)"]]
```

**Onboarding status flags** (`tenancy.property_managers.onboarding_status`):

| Value | Name | Meaning |
| --- | --- | --- |
| 1 | Registered | Account exists, no accounting connection |
| 2 | Connected | OAuth tokens persisted |
| 3 | Synced | First customer/invoice pull succeeded |
| 4 | **Active** | Fully onboarded — **the worker only processes PMs at status 4** |

Each wizard step also stamps its own completion timestamp
(`step_connect_done_at`, `step_close_done_at`, `step_notice_prefs_done_at`,
`step_preflight_done_at`) so a PM who leaves mid-flow resumes exactly where they
left off. "Sign in with Intuit" pre-stamps step 1 because the OAuth grant
established the accounting connection during sign-up.

---

## 4. The nightly worker — once-daily cron, per PM at local midnight

The worker is a Hangfire recurring job. A **sweep** runs on a schedule, finds
every Active PM whose local clock has just crossed midnight, and enqueues a
per-PM **orchestrator** run. Each run is a 4-stage pipeline.

```mermaid
flowchart TD
    CRON[["Hangfire recurring sweep<br/>(runs frequently; acts per PM<br/>at that PM's local midnight)"]]

    CRON --> PICK{"For each PM:<br/>is it local midnight<br/>AND status = Active?"}
    PICK -->|no| SKIP1["skip"]
    PICK -->|yes| ENQUEUE["Enqueue PmDailyOrchestrator<br/>(PM, businessDate)"]

    ENQUEUE --> ORCH

    subgraph ORCH["PmDailyOrchestrator.ProcessAsync(pm, businessDate)"]
        S0["① Account integrity check<br/>accounting connection present + healthy?<br/>(sync invoices/customers if due)"]
        S1["② Reminders<br/>pre-due + due-date notices<br/>for invoices coming due"]
        S2["③ Late fees<br/>past grace period →<br/>assess state-compliant fee<br/>+ post-due / late-fee notice"]
        S3["④ Daily Close report<br/>email PM a summary of the day"]

        S0 -->|connection not ready| HALT["log + skip remaining stages"]
        S0 -->|ok| S1 --> S2 --> S3
    end

    S3 --> DONE([Run recorded in pm_processing_runs])

    style S2 fill:#4f46e5,color:#fff
    style S3 fill:#0ea5e9,color:#fff
```

**Idempotency & resilience**

- Each (PM, business-date) run claims a slot in `pm_processing_runs`; a duplicate enqueue short-circuits ("already processed").
- Each per-lease action is keyed in `assessment_runs` by (lease, due-date, action kind) — re-running a day never double-sends or double-charges.
- The Daily Close email is chained as a Hangfire **continuation** of the orchestrator, so it always carries that day's totals.
- Every log line carries `PmId` / `RunId` / `BusinessDate` scope and lands in `logs.events` for the admin log viewer.

---

## 5. Where billing sits — the decision + action engine

"Billing" here doesn't mean charging the PM money. It's the engine that decides
**what to do for each lease today** and **executes** it. It's the connective
tissue between the rules engine, the tenant data, and the notification adapters.

```mermaid
flowchart LR
    subgraph INPUTS["Inputs (read)"]
        PMPREF["PM notice preferences<br/>(master toggles, defaults)"]
        LEASESET["Per-lease notice settings<br/>(overrides)"]
        STATE["State rules<br/>(late-fee %, min grace,<br/>caps — DueMap.Rules)"]
        INV["Synced rent invoices<br/>(due dates, balances, pay URLs)"]
    end

    subgraph BILLING["DueMap.Billing"]
        POLICY["EffectivePolicyService<br/>merge: state ▸ PM default ▸ lease override"]
        PLANNER["AssessmentPlanner<br/>what action is due for this<br/>lease on this date?"]
        EXEC["ActionExecutor<br/>render + dispatch + record"]
        FEEREPO["LateFeeAssessmentRepository<br/>idempotent fee ledger"]
        CLOSE["DailyCloseReportService<br/>end-of-run summary"]
    end

    subgraph OUTPUTS["Outputs (write / call)"]
        NOTICES2["DueMap.Notices<br/>resolve + render template<br/>(Scriban)"]
        DISPATCH["DueMap.Integrations<br/>SendGrid / Twilio<br/>+ invoice PDF fetch"]
        LEDGER[("billing.late_fee_assessments")]
        DELIV[("notices.deliveries")]
    end

    PMPREF & LEASESET & STATE --> POLICY
    POLICY --> PLANNER
    INV --> PLANNER
    PLANNER -->|PlannedAction list| EXEC
    EXEC --> NOTICES2 --> DISPATCH
    EXEC --> FEEREPO --> LEDGER
    EXEC --> DELIV
    EXEC --> CLOSE
```

**The merge order** (EffectivePolicyService) — most specific wins:

```
State rule (legal floor/ceiling)  ▸  PM portfolio default  ▸  Per-lease override
```

State law is a **guardrail**, not just a default: if a PM sets a 2-day grace
period but the state mandates a 5-day minimum, the engine uses 5. This is the
core compliance value of the product.

---

## 6. The notification itself — render → dispatch → record

When the ActionExecutor decides "send a late-fee notice to lease #42", here's
what happens:

```mermaid
sequenceDiagram
    participant EX as ActionExecutor (Billing)
    participant TN as Tenancy
    participant NT as Notices
    participant IN as Integrations
    participant QB as QBO/Xero
    participant SG as SendGrid

    EX->>TN: Resolve tenant contact (email/phone) for lease
    EX->>NT: ResolveRenderable(notice type, PM, state)
    Note over NT: PM template override ▸ system template<br/>(highest approved version)
    EX->>TN: Get current invoice pay URL + due date
    EX->>NT: Render(template, {tenant_name, amount_owed,<br/>due_date, pay_url, ...}) via Scriban
    alt notice type == late_fee_notice
        EX->>IN: Fetch invoice PDF for current period
        IN->>QB: GET invoice PDF (authoritative doc)
        QB-->>IN: application/pdf bytes
    end
    EX->>IN: Dispatch(email, subject, html, text, [pdf attachment])
    IN->>SG: Send (SendGrid)
    SG-->>IN: queued / message id
    EX->>TN: Record delivery in notices.deliveries
    EX->>EX: Record assessment_run (idempotency)
```

**Notice copy today** (system templates, PM-overridable):

- **Pre-due reminder** — friendly nudge N days before due, with Pay Now button.
- **Due-date reminder** — morning-of reminder, Pay Now button.
- **Grace-period reminder** — past due but inside grace; "pay to avoid a fee."
- **Late-fee notice** — fee applied; **the actual QBO/Xero invoice PDF is attached**, Pay Now button, and an "ignore if already paid" line. No "reply to this email" language.

All transactional emails (welcome, magic link, preflight) share one branded
chrome via `TransactionalEmailBuilder`.

---

## 7. Tenant portal (`/t/*`)

A lightweight, co-branded surface for tenants — separate auth, separate layout.

```mermaid
flowchart LR
    LOGIN["/t/login<br/>enter email"] -->|magic link emailed| REDEEM["/t/login/{token}<br/>single-use, 15-min TTL"]
    LOGIN -->|optional password| HOME
    REDEEM -->|sets dm_tenant cookie<br/>30-day session| HOME["/t<br/>balance + Pay Now"]
    HOME --> INV["/t/invoices<br/>history + PDF download + Pay"]
    HOME --> PROF["/t/profile<br/>read-only + request change"]
```

- **Auth**: magic link (default) + optional password. Tokens hashed at rest, constant-time compare, anti-enumeration.
- **Pay Now**: links to the QBO/Xero hosted payment page (we never touch card data).
- **PDF download**: fetched live from the accounting system, scoped to the signed-in tenant's own invoices.

---

## 8. Admin & ops surfaces

| Surface | Auth | Purpose |
| --- | --- | --- |
| `/admin/worker-logs` | Basic Auth (dev creds) | Structured worker log stream, filterable by PM / severity / time window |
| `/hangfire` | Authenticated user | Job queue, trigger sweeps, inspect failures |
| `/health` | Anonymous | Liveness probe |

---

## 9. Data model (the tables that matter)

```mermaid
erDiagram
    PROPERTY_MANAGERS ||--o{ CUSTOMERS : "has"
    PROPERTY_MANAGERS ||--o{ LEASES : "has"
    PROPERTY_MANAGERS ||--|| PM_NOTICE_PREFERENCES : "defaults"
    PROPERTY_MANAGERS ||--|| PM_DAILY_CLOSE_SETTINGS : "close config"
    PROPERTY_MANAGERS ||--o| ACCOUNTING_CONNECTIONS : "OAuth tokens"
    CUSTOMERS ||--o{ LEASES : "billed to (customer_id)"
    CUSTOMERS ||--o{ RENT_INVOICES : "owes"
    LEASES ||--o{ RENT_INVOICES : "linked"
    LEASES ||--o| LEASE_NOTICE_SETTINGS : "overrides"
    LEASES ||--o{ LATE_FEE_ASSESSMENTS : "fees"
    LEASES ||--o{ NOTICE_DELIVERIES : "notices sent"
    CUSTOMERS ||--o{ TENANT_LOGINS : "portal access"
```

- **`customers`** = synced from QBO/Xero (who pays). **`leases`** = DueMap's own concept (the agreement: rent, state, dates) — the attachment point for rules + notices. One customer ↔ one lease in the common case.
- **`accounting_connections`** holds encrypted OAuth tokens (Data Protection); Web and Worker share the keyring.
- **`logs.events`** (separate schema) is the worker's structured log sink.

> **Known UX rough edge under discussion:** the split between the **Leases** and
> **Link Customers** screens exposes this `customers`↔`leases` duality to the PM,
> which feels awkward. A proposed redesign collapses both into a single
> **Tenants** screen where lease data is an inline property of each synced
> customer. Not yet built — flagged for the team conversation.

---

## 10. Tech stack

| Concern | Choice |
| --- | --- |
| Runtime | .NET 8 |
| Web UI | Blazor Server (app) + Razor Pages (auth surfaces) |
| Styling | Tailwind CSS v4 |
| Background jobs | Hangfire (SQL Server storage) |
| Database | SQL Server (LocalDB in dev) |
| Accounting | QuickBooks Online + Xero (OAuth 2.0) |
| Email / SMS | SendGrid / Twilio |
| Template rendering | Scriban |
| Logging | Serilog → console + `logs.events` table |
| Token encryption | ASP.NET Core Data Protection |
| Schema migrations | Append-only `db/duemap_schema_v*.sql`, auto-applied in dev by `DevSchemaBootstrap` |

---

## 11. End-to-end, in one sentence per stage

1. **Sign up** — PM registers with email or "Sign in with Intuit" (which connects QBO in the same step).
2. **Connect** — OAuth to QuickBooks/Xero; we pull customers + invoices.
3. **Configure** — daily-close timezone/time, reminder + late-fee preferences, email templates.
4. **Introduce** — optional one-time preflight email to tenants.
5. **Go active** — status flips to Active; the PM's dashboard unlocks.
6. **Every night** — the worker, at the PM's local midnight, checks connection health, sends due reminders, assesses state-compliant late fees, and emails the PM a daily close.
7. **Tenants** — receive branded reminder/late-fee emails (late fees carry the real invoice PDF + a Pay Now link) and can self-serve in the portal.
8. **Billing engine** — sits between the rules, the tenant data, and the notification adapters, deciding and executing what each lease needs each day.

---

*Generated as a point-in-time snapshot for team discussion. The codebase is
under active development; treat this as "as of now," not a spec.*

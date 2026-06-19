# DueMap aka Rentdrate

The portfolio dashboard that maps every overdue rent â€” and handles the late fees, notices, and accounting posts so you don't have to.

## What it does

Property managers waste 5â€“15 hours per week chasing late rent and frequently miss applying late fees they're legally entitled to. DueMap puts every overdue unit across a portfolio on a single dashboard â€” color-coded by stage, sortable by amount, filterable by property â€” and runs the underlying workflow in the background: it knows each lease's rent schedule, the per-state legal rules, and the per-property time zone. When rent goes unpaid past the grace period, DueMap sends notices on schedule, applies fees according to state law, and posts everything to the property manager's QuickBooks or Xero account.

The map is the headline feature. Automation is what makes the map stay accurate without anybody typing into it.

## Target customer

US property management companies with **50â€“500 rental units**. They typically:

- Already use QuickBooks or Xero for accounting
- Have a basic property management tool, or spreadsheets for everything else
- Have one staff member who spends a meaningful slice of their week chasing late rent
- Lose more in unapplied fees per month than this tool costs

## Business model

**Pricing (initial â€” flat tiers):**

| Tier | Price | Units |
|------|-------|-------|
| Starter | $49/mo | up to 50 |
| Growth | $99/mo | up to 200 |
| Pro | $199/mo | up to 500 |
| Enterprise | from $299/mo | 500+, custom |

**Path to first $1K MRR:** 10 customers at the Growth tier. The sweet spot is property managers with 100â€“250 units. Lower-tier customers help validate; the unit economics work best in the middle.

## Launch states (Phase 1)

Texas, Florida, Georgia, North Carolina, Arizona. Landlord-friendly states with simpler late fee rules. California, New York, Oregon, and Washington land in Phase 2 once the state rules engine is battle-tested.

## Tech stack

| Concern | Choice |
|---------|--------|
| Backend | ASP.NET Core 8 (LTS), C# 12 |
| UI | Blazor Server |
| Database | SQL Server 2019+ |
| ORM | Entity Framework Core 8 |
| Background jobs | Hangfire (SQL Server storage) |
| Template rendering | Scriban |
| Email | SendGrid |
| SMS | Twilio |
| Hosting | Azure (Container Apps preferred for scale-to-zero pricing) |
| Auth | ASP.NET Core Identity + cookies |

## Architecture

**Single ASP.NET Core solution, two processes:**

1. `DueMap.Web` â€” Blazor Server admin UI + Minimal APIs for webhooks (QuickBooks, Xero, Stripe callbacks, future integrations)
2. `DueMap.Worker` â€” Hangfire worker hosting scheduled jobs, most importantly the per-property late fee assessment loop

Both processes deploy from the same codebase, reference the same module projects, and share one SQL Server database.

### Modules

Six business modules, each its own .NET class library. Modules communicate via interfaces â€” never via DbContext sharing or direct entity access.

| Module | Concern |
|--------|---------|
| `Tenancy` | Property managers, properties, units, leases, residents |
| `Rules` | State rule versions, jurisdiction overrides, rule resolution |
| `Notices` | Template versions, rendering pipeline, immutable delivery audit trail |
| `Billing` | Rent invoices, payments, late fee assessments |
| `Integrations` | QuickBooks, Xero, Stripe adapters |
| `Identity` | Users, auth, multi-tenant isolation |

## Architecture decisions

### ADR-001 â€” Modular monolith over microservices

Microservices solve organizational problems we don't have. With a solo developer and 0â€“50 customers, every service multiplies operational burden without proportional benefit. We organize the code as if it could be split later, but deploy as one unit. The seams are drawn; we cut along them only when we have data showing where they actually need to be.

### ADR-002 â€” Blazor Server for admin UI, not React or Angular

Internal B2B admin tool, used by office staff on broadband. Blazor Server's downsides (latency on flaky networks, SignalR fragility) don't apply to this audience. The upsides eliminate roughly 40% of the code we'd otherwise write: no separate frontend codebase, no DTO duplication, no JWT lifecycle, no CORS, direct service calls from components.

Reversibility note: business logic lives in module services, which are pure C# with no Blazor dependency. If Blazor Server turns out wrong, we bolt Minimal API endpoints onto the existing services and build a SPA against them. The backend doesn't change.

Future tenant-facing portal (for residents to view notices, pay rent) will *not* use Blazor Server â€” different audience profile (mobile, flaky networks, occasional spikes). That gets a SPA or Razor Pages.

### ADR-003 â€” Shared database with `property_manager_id` filter for multi-tenancy

Simplest of three options (shared / schema-per-tenant / database-per-tenant). Enforced via global EF Core query filters so no query can forget the filter. Migrate to database-per-tenant only when a customer's compliance requirements force it (typically past 100 customers).

### ADR-004 â€” Time-versioned rules with database-enforced immutability

State late fee laws change. A fee assessed in 2026 must be defensible in court in 2030 using the rule version that was active in 2026. We never `UPDATE` rule rows â€” we expire them and insert new ones. Notice deliveries store the rendered subject and body, not just a template reference. `INSTEAD OF UPDATE` and `INSTEAD OF DELETE` triggers enforce this at the database layer.

### ADR-005 â€” Per-property time zone handling via Hangfire 15-minute polling

We don't run "one midnight job." We run a Hangfire job every 15 minutes that asks: which leases have just crossed midnight in their property's local time zone, and haven't been assessed yet? Property time zone is stored as an IANA name (`America/Los_Angeles`), not Windows-style. The job is idempotent â€” re-running on the same lease for the same date is a no-op (unique constraint on the assessment row).

### ADR-006 â€” Product name: DueMap

Chosen for distinctiveness over descriptiveness. "DueMap" is a fanciful compound (two real words joined into a non-dictionary phrase), which is the strongest kind of mark for trademark purposes and avoids the SEO and trademark conflicts that came with the earlier candidates ("Cadence" had adjacency issues with Mortgage Cadence; "DueDate" had a same-name iOS app, pregnancy-content SEO dominance, and descriptiveness-rejection risk).

The "Map" part also commits us to a product positioning: the dashboard / portfolio-visibility view is the headline feature, not the background automation. A property manager opens DueMap and sees every overdue unit in one screen â€” that's the demo. Automation is what makes the map stay accurate.

## Repository layout

```
DueMap/
â”œâ”€â”€ DueMap.sln
â”œâ”€â”€ Directory.Build.props          # Shared MSBuild settings
â”œâ”€â”€ README.md                      # This file
â”œâ”€â”€ db/
â”‚   â””â”€â”€ duemap_schema.sql          # SQL Server DDL (rules + notices)
â””â”€â”€ src/
    â”œâ”€â”€ DueMap.Web/                # Blazor Server + Minimal APIs
    â”œâ”€â”€ DueMap.Worker/             # Hangfire scheduled jobs
    â”œâ”€â”€ DueMap.Common/             # IModule contract, shared types
    â””â”€â”€ Modules/
        â”œâ”€â”€ DueMap.Tenancy/
        â”œâ”€â”€ DueMap.Rules/
        â”œâ”€â”€ DueMap.Notices/
        â”œâ”€â”€ DueMap.Billing/
        â”œâ”€â”€ DueMap.Integrations/
        â””â”€â”€ DueMap.Identity/
```

## Validation track (parallel to building, not after)

- [ ] Read 12 months of late-fee threads on r/PropertyManagement and r/Landlord
- [ ] One-page landing site with waitlist form (Carrd or static HTML on Azure Static Web Apps)
- [ ] 20 outreach DMs/week to property managers (Reddit, LinkedIn)
- [ ] Apply for QuickBooks App Store developer account â€” approval takes 4â€“8 weeks
- [ ] Apply for Xero developer access
- [ ] Join NARPM as an associate member, browse the directory, identify a target chapter
- [ ] Secure `duemap.com` (or fallback `.app` / `.io`); USPTO TESS search for classes 9 and 42

## Open decisions

- **Hosting platform** â€” Azure Container Apps (scale-to-zero pricing, ~$20â€“40/mo at low traffic) vs Azure App Service (more familiar)
- **Auth provider** â€” ASP.NET Core Identity (built-in, more code) vs Auth0 / Azure AD B2C (third-party, less code, monthly cost from 1k MAU)
- **Notice delivery transport** â€” direct SendGrid/Twilio calls (simpler) vs Azure Service Bus queue (more resilient, more moving parts)
- **When to extract `Integrations` into its own service** â€” probably never for v1, but watch QuickBooks API rate limits as customer count grows past ~50

## Getting started

Requires .NET 8 SDK and SQL Server (or LocalDB / SQL Server in Docker).

```bash
# Create the database (or point your existing one at)
sqlcmd -S "(localdb)\mssqllocaldb" -Q "CREATE DATABASE DueMap"

# Run the schema migration
sqlcmd -S "(localdb)\mssqllocaldb" -d DueMap -i db/duemap_schema.sql

# Restore and build
dotnet restore
dotnet build

# Run the web app
dotnet run --project src/DueMap.Web

# In a second terminal, run the worker
dotnet run --project src/DueMap.Worker
```


## Smoke test (local)

A fresh checkout takes ~5 minutes to bring up. The Web host seeds a demo PM
with sample data in Development, so you can click through every page without
configuring QuickBooks / Xero / SendGrid / Twilio.

### 1. Apply the SQL schema + seeds, in order

```sh
sqlcmd -S "(localdb)\mssqllocaldb" -Q "CREATE DATABASE DueMap"
SCRIPTS="duemap_schema.sql duemap_schema_v2_lease_settings.sql duemap_schema_v3_assessments.sql \
         duemap_schema_v4_pm_processing.sql duemap_schema_v5_accounting.sql \
         duemap_schema_v6_customers_invoices.sql duemap_schema_v7_identity.sql \
         seed_state_rules.sql seed_notice_templates.sql"
for s in $SCRIPTS; do
    sqlcmd -S "(localdb)\mssqllocaldb" -d DueMap -i "db/$s"
done
```

### 2. Configure connection strings (dev defaults are OK)

`src/DueMap.Web/appsettings.json` and `src/DueMap.Worker/appsettings.json` both
default to LocalDB targeting the `DueMap` database. Leave QuickBooks / Xero /
SendGrid / Twilio keys empty in dev — the dispatcher returns clean `Failed`
results when keys are missing.

### 3. Run

```sh
# Terminal 1
dotnet run --project src/DueMap.Web

# Terminal 2
dotnet run --project src/DueMap.Worker
```

On first run in Development, the Web host's `DemoSeeder` creates:

| Resource          | Value                                                 |
|-------------------|-------------------------------------------------------|
| PM org            | "Demo Property Management"                            |
| Admin user        | `demo@duemap.dev`                                     |
| Password          | `DemoUser1!`                                          |
| Customers         | Alice (CA), Bob (TX), Carla (NY)                      |
| Leases            | 3, monthly rent $2,100 / $1,450 / $2,950              |
| Invoices          | One past-due, one due today, one due in 3 days        |

The seeder is idempotent — subsequent runs are no-ops.

### 4. Click through

Open <https://localhost:5001>, sign in with the demo credentials. The full
menu should be live:

- **Dashboard** — PM id readout
- **Accounting** — shows "Not connected"; the QuickBooks / Xero buttons hit
  the OAuth flow (will fail without configured client ids — expected)
- **Notice preferences** — auto-creates a defaults row; tweak the toggles
- **Templates** — the four system templates exist via the SQL seed; create
  a PM-specific override and save
- **Leases** — the three demo leases appear; try the bulk-apply modal
- **Link customers** — all three leases are already auto-linked; try
  unlink + relink
- **Processing status** — empty until the Hangfire worker ticks (every 15
  min by default; trigger manually via the Hangfire dashboard at `/hangfire`)

### 5. Trigger a job manually (optional)

The Worker registers the daily sweep on `*/15 * * * *`. To exercise it
immediately, open `/hangfire` and click "Trigger now" on the
`billing.daily-assessment-sweep` recurring job. Or wait up to 15 minutes.
You should see a row appear on **Processing status** with the past-due lease
producing a late-fee assessment + late-fee notice.

## Status

- Business model, ICP, pricing model decided
- Database schema (rules engine, notice templates, accounting, identity) designed and DDL generated
- Architecture decisions made (modular monolith, Blazor Server, Hangfire)
- Product name decided: DueMap
- All 7 modules built; full Blazor admin UI; ASP.NET Identity wired; SQL seeds + demo seeder in place
- Smoke-testable end-to-end without external accounts
- **Next:** real QuickBooks / Xero credentials in provider sandboxes; SendGrid + Twilio for live notice delivery; deploy / CI scaffolding

## Docker (full local stack)

`docker compose up --build` brings up SQL Server + a one-shot schema bootstrap +
Web + Worker. Faster than the bare-metal smoke test path because the bootstrap
container handles `sqlcmd` for you, and the Data Protection keyring lives in a
named volume so OAuth tokens survive restarts.

```sh
# Required: SA password the SQL Server container will adopt.
export MSSQL_SA_PASSWORD='YourStrong!Passw0rd'

docker compose up --build
```

Boot order:

1. `mssql` starts; healthcheck polls `SELECT 1` until ready
2. `db-bootstrap` runs once: creates the `DueMap` database, applies every
   schema + seed SQL file in order, exits 0
3. `web` and `worker` start; `DemoSeeder` fires on first boot inside `web`
4. Browse to <http://localhost:8080> and sign in as
   `demo@duemap.dev` / `DemoUser1!`

Idempotency: subsequent `docker compose up` runs reuse the volumes; bootstrap
is a no-op (schema applies use `IF NOT EXISTS` / `MERGE`), demo seeder skips.

To wipe state and start fresh:

```sh
docker compose down -v
```

## CI

`.github/workflows/ci.yml` runs on every push and PR to `main`:

- `dotnet restore` + `dotnet build --configuration Release`
  (warnings-as-errors stays on via [Directory.Build.props](Directory.Build.props))
- `dotnet test` (no test projects yet — adding them is the obvious next step)
- Docker image build for Web + Worker, gated to `push` events or PRs labeled
  `build-images` so ordinary code PRs don't pay the image-build cost

GHA cache keys on the .csproj hashes for NuGet and uses BuildKit's GHA cache
for Docker layers, so warm builds are fast.

## Deploy

**Azure Container Apps** is the supported path. Bicep template + step-by-step
docs live in [deploy/](deploy/):

- [`deploy/azure/main.bicep`](deploy/azure/main.bicep) — Container Apps env +
  Azure SQL serverless + ACR + Azure Files for Data Protection keys +
  managed identity for ACR pull
- [`deploy/README.md`](deploy/README.md) — walks through `az login`, image
  push to ACR, deployment, post-deploy schema apply, and the open caveats
  before going to production

The image build in CI produces the artifacts the Bicep template consumes.
Cost shape per the analysis above: ~$70/mo idle, ~$200–300/mo at 10 paying
PMs.

For a cheaper self-hosted path, `docker compose up -d` on a small VM plus
Caddy for TLS works — briefly documented at the end of `deploy/README.md`.

# DueMap — Manual QA Test Plan

**Audience:** Manual QA engineer running the suite top-to-bottom against a Dev or Staging environment.
**Goal:** Validate every customer-facing flow plus the worker pipeline and admin tooling, in the sequence a real onboarding would follow.
**How to use:** Walk the sections in order. Each test row has an ID, preconditions, steps, and the exact expected outcome. Mark **Pass / Fail / Blocked** in the rightmost column. File any failures with the test ID, the step that failed, and a screenshot.

---

## 0. Glossary & Conventions

| Term            | Meaning                                                                                           |
| --------------- | ------------------------------------------------------------------------------------------------- |
| **PM**          | Property Manager — the primary customer persona.                                                  |
| **Tenant**      | Resident under a lease. Not to be confused with multi-tenancy of PMs.                             |
| **Run**         | A single nightly orchestrator execution for one PM on one business date.                          |
| **Business date** | The local-calendar date in the PM's timezone at the moment the sweep fires (≈ local midnight). |
| **Kiosk mode**  | The locked-down shell shown to PMs whose accounting system isn't connected yet.                   |
| **Status**      | `OnboardingStatus` enum: 1=Registered, 2=Connected, 3=Synced, 4=Active.                           |

**Browser matrix:** Chrome (primary), Edge, Firefox. Mobile spot-check: iOS Safari + Android Chrome on the layouts marked **(responsive)**.
**Theme matrix:** Light + Dark. Every visual test must pass in both unless noted.

---

## 1. Environment Pre-flight

Run these once before starting. If any fail, stop and fix infra — every test downstream depends on them.

| ID    | Test                          | Steps                                                                                      | Expected                                                                                                                  | Result |
| ----- | ----------------------------- | ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------- | ------ |
| ENV-1 | Web process boots             | Start `DueMap.Web`. Watch console.                                                         | "DueMap.Web starting up" logged. No fatal exceptions. Listens on the expected HTTPS port.                                 |        |
| ENV-2 | Worker process boots          | Start `DueMap.Worker`. Watch console.                                                      | Hangfire recurring jobs registered. Serilog MSSqlServer sink initializes without error. Worker reports "Started."         |        |
| ENV-3 | Database reachable            | Open `localhost`, login with any user.                                                     | Login page renders. No "Cannot open database" toast.                                                                      |        |
| ENV-4 | Schema migrations applied     | Web Dev startup completes; check console for `DevSchemaBootstrap`.                         | All `db/duemap_schema_v*.sql` scripts marked applied. `logs.events` table exists (v11).                                   |        |
| ENV-5 | Demo seed present             | Connect to SQL, `SELECT COUNT(*) FROM tenancy.property_managers`.                          | At least one demo PM row exists. Demo user can sign in (see AUTH-2).                                                      |        |
| ENV-6 | Data Protection keyring       | Confirm `…/.dataprotection-keys` folder exists and contains at least one XML key file.     | File present. Web and Worker share the same path (otherwise token decryption will fail later).                            |        |
| ENV-7 | User secrets configured (Dev) | `dotnet user-secrets list --project src/DueMap.Web`.                                       | `Integrations:QuickBooks:ClientId/Secret` and `Integrations:Xero:ClientId/Secret` are set if SSO/accounting will be used. |        |
| ENV-8 | HTTPS only                    | Browse `http://localhost:<port>/`                                                          | Auto-redirects to `https://`. No mixed-content warnings in console.                                                       |        |

---

## 2. Authentication

### 2.1 Email/Password

| ID     | Test                                  | Steps                                                                                                 | Expected                                                                                                                  | Result |
| ------ | ------------------------------------- | ----------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- | ------ |
| AUTH-1 | Register a new PM                     | `/Register` → enter unique email + strong password → submit.                                          | Redirected to onboarding. New row in `identity.users` and `tenancy.property_managers`; `PmId` claim present in cookie.    |        |
| AUTH-2 | Login existing user                   | `/Login` → demo email + password.                                                                     | Redirect to `/`. Top-right shows user email. Auth cookie set with `HttpOnly`, `Secure`, `SameSite=Lax`.                   |        |
| AUTH-3 | Login wrong password                  | `/Login` → demo email + bad password.                                                                 | Stays on Login. Validation message visible. No cookie issued. No PII leaked ("user not found" must not be distinguishable). |        |
| AUTH-4 | Logout                                | Click user menu → Logout.                                                                             | Redirect to Login. Cookie cleared. Back button + reload do not restore the session. No 400 antiforgery error.             |        |
| AUTH-5 | Register failure compensates PM row   | Force a user-creation failure (e.g. duplicate email after a partial create).                          | The PM row created during the attempt is deleted (compensation). No orphan PM left behind.                                 |        |
| AUTH-6 | Password policy                       | Try weak passwords: short, no digits, common.                                                         | Each rejected with the matching validation message. Strong password accepted.                                              |        |
| AUTH-7 | Anti-CSRF on POST forms               | Inspect Login/Register/Logout forms in DevTools.                                                      | Hidden `__RequestVerificationToken` is present on Login/Register. Logout works without antiforgery (by design — documented).|       |

### 2.2 SSO

| ID      | Test                                  | Steps                                                                       | Expected                                                                                                                                            | Result |
| ------- | ------------------------------------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| AUTH-8  | "Sign in with Intuit" present         | Visit `/Login`.                                                             | Intuit brand button visible with the correct asset (white/blue per Intuit brand kit). Same for Xero (blue).                                          |        |
| AUTH-9  | Intuit SSO happy path                 | Click "Sign in with Intuit" → consent in Intuit popup.                      | Returns to `/signin-intuit` → app. New user provisioned with email from id_token; PM row created. Lands on `/connections` if not yet connected.      |        |
| AUTH-10 | Xero SSO happy path                   | Same as AUTH-9 but Xero.                                                    | Same outcome via `/signin-xero`.                                                                                                                    |        |
| AUTH-11 | SSO button when provider unconfigured | Remove ClientId from user-secrets → restart → click the button.             | Friendly "not configured" notice. **No** 500 from Challenge.                                                                                         |        |
| AUTH-12 | SSO link to existing local account    | Sign up with email X, log out, sign in via SSO whose id_token email = X.    | The SSO identity links to the existing user; no duplicate PM row created.                                                                            |        |

---

## 3. Onboarding & Kiosk Mode (multi-step wizard)

Onboarding is a three-step wizard (`/onboarding/connect` → `/onboarding/close-settings`
→ `/onboarding/preflight`), persisted per PM. Each step completion stamps a
timestamp on `tenancy.property_managers` so a PM who leaves mid-flow resumes
on their next sign-in. The full app unlocks only when all three are done.

### 3.1 Shell & stepper

| ID    | Test                                       | Steps                                                                                                                | Expected                                                                                                                                                                            | Result |
| ----- | ------------------------------------------ | -------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| ONB-1 | Fresh PM lands in onboarding shell         | Register a new PM. Don't connect anything.                                                                            | Page is `/onboarding/connect`. Header shows brand + "Setting up your workspace" pill. Stepper visible: 1 highlighted indigo, 2 + 3 muted. No sidebar, no app nav.                   |        |
| ONB-2 | Kiosk blocks deep links to main app        | While mid-onboarding, manually type `/leases`, `/templates`, `/dashboard`, `/`.                                       | Each URL is rewritten to the PM's first incomplete step's route. The PM cannot reach any main-app surface until all three steps are done.                                          |        |
| ONB-3 | Cannot skip forward in the wizard          | Mid-step-1 (connect not done), type `/onboarding/close-settings` or `/onboarding/preflight`.                          | Both URLs rewrite back to `/onboarding/connect`. Stepper pill 1 is the only one active.                                                                                            |        |
| ONB-4 | Can revisit a completed step               | After finishing step 1, type `/onboarding/connect` again.                                                             | Page renders normally (the PM hasn't been forward-jumped). The stepper shows step 1 with a green tick, step 2 active.                                                              |        |
| ONB-5 | Sign-out from kiosk                        | Click "Sign out" in the onboarding shell header.                                                                      | Logs out cleanly. Re-signing in resumes at the same incomplete step.                                                                                                                |        |

### 3.2 Step 1 — Connect Accounting

| ID     | Test                                        | Steps                                                                            | Expected                                                                                                                                | Result |
| ------ | ------------------------------------------- | -------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| ONB-6  | Brand buttons render                        | View `/onboarding/connect`.                                                       | Both "Connect to QuickBooks" (Intuit green button) and the Xero brand pill are visible and clickable.                                  |        |
| ONB-7  | Connect → sync → forward to step 2          | Click QBO, complete sandbox consent.                                              | OAuth callback → `/onboarding/syncing` ticks each step → on success forwards to `/onboarding/close-settings`. Step 1 pill is green.    |        |
| ONB-8  | Connect failure stays on step 1             | Trigger a failure (e.g. cancel consent).                                          | Lands back on `/onboarding/connect`. Step 1 NOT marked done. PM can retry.                                                              |        |
| ONB-9  | DB stamp                                    | After ONB-7, inspect `tenancy.property_managers`.                                  | `step_connect_done_at` is populated. `step_close_done_at` and `step_preflight_done_at` are still NULL.                                  |        |

### 3.3 Step 2 — Daily Close Settings

| ID     | Test                                        | Steps                                                                                          | Expected                                                                                                                                                | Result |
| ------ | ------------------------------------------- | ---------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| ONB-10 | Form pre-fills from defaults                | Land on `/onboarding/close-settings` for the first time.                                       | Enabled toggle ON. Timezone = whatever the PM's TimeZoneId currently is. Send hour = 7:00 AM. Recipient + CC blank.                                    |        |
| ONB-11 | Disabling greys out fields                  | Toggle Enabled off.                                                                            | Timezone, hour, recipient, CC inputs become disabled visually.                                                                                          |        |
| ONB-12 | Save persists + advances                    | Pick a non-default timezone + 9:00 AM, save.                                                    | New row in `tenancy.pm_daily_close_settings` with chosen values. `property_managers.time_zone_id` mirrors the same tz. Navigates to step 3.            |        |
| ONB-13 | DB stamp                                    | After ONB-12.                                                                                   | `step_close_done_at` is populated. Step 2 pill turns green in the stepper.                                                                              |        |
| ONB-14 | Resume on re-entry                          | Sign out after step 2, sign in again.                                                            | Lands directly on `/onboarding/preflight` (next incomplete step). NOT on step 1 or 2.                                                                  |        |
| ONB-15 | Invalid recipient email                     | Enter `not-an-email` in Recipient override, save.                                               | Validation rejects (`type="email"`); save is blocked.                                                                                                  |        |

### 3.4 Step 3 — Notify Tenants (Preflight)

| ID     | Test                                          | Steps                                                                                                       | Expected                                                                                                                                                                                  | Result |
| ------ | --------------------------------------------- | ----------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| ONB-16 | Customer grid renders                         | Visit `/onboarding/preflight`.                                                                              | Tabular grid of synced customers for THIS PM only. Cross-tenant isolation enforced.                                                                                                       |        |
| ONB-17 | Search filter                                 | Type a tenant name fragment.                                                                                | List narrows live to matching rows. Pagination resets to page 1.                                                                                                                          |        |
| ONB-18 | Status filter                                 | Switch "All / Pending / Already notified".                                                                  | Grid filters accordingly. "Already notified" rows show a green "Sent YYYY-MM-DD" pill.                                                                                                    |        |
| ONB-19 | No-email rows are non-selectable              | Find a customer row whose Email column says "no email on file".                                              | Checkbox is disabled. "Select all on page" skips them.                                                                                                                                    |        |
| ONB-20 | Pagination                                    | With > 25 customers.                                                                                         | Page size 25; "Prev" / "Next" buttons enable/disable correctly. Page indicator shows "Page N of M".                                                                                       |        |
| ONB-21 | Bulk select all on page                       | Click "Select all on page".                                                                                  | Every row on the current page with an email is selected. Header checkbox checked.                                                                                                         |        |
| ONB-22 | Send modal opens                              | Select ≥1 customer, click "Send notification email".                                                         | Modal opens with editable Subject input and rich-text body. Default copy is friendly + tenant-agnostic. Recipient count is shown in subtitle.                                            |        |
| ONB-23 | Rich-text toolbar                             | In the body editor, select text and click B / I / U / List buttons.                                          | Formatting applies in the editor (uses `document.execCommand`).                                                                                                                           |        |
| ONB-24 | Cancel modal                                  | Open modal, click Cancel.                                                                                    | Modal closes. No DB writes. No selections cleared.                                                                                                                                        |        |
| ONB-25 | Send marks customers                          | Click "Send to N".                                                                                           | Modal closes. Selected customers' `preflight_notified_at` is stamped UTC-now. Grid pills flip to "Sent today". Selection cleared.                                                         |        |
| ONB-26 | Resend skips already-notified                 | Re-select a customer that's already "Sent" + a fresh one, click Send.                                        | Only the fresh customer is stamped (idempotent). Count of "rows stamped" matches.                                                                                                         |        |
| ONB-27 | Tenant isolation on send                      | Open DevTools → manipulate the POST payload to include another PM's customer id, send.                       | The cross-tenant id is silently ignored by `MarkPreflightSentAsync`. Only own-PM rows stamped. No 500.                                                                                    |        |
| ONB-28 | Done — go to dashboard                        | Click the primary "Done — go to dashboard →" button.                                                         | `step_preflight_done_at` is stamped. `onboarding_status` flips to 4 (Active). Redirected to `/` with the driver.js tour fired once.                                                       |        |
| ONB-29 | Done with zero sends is allowed                | On a fresh preflight page, click "Done" without sending anyone.                                              | Step still marked complete; status → Active; lands on dashboard. The footer copy reflected this was a skip path ("You can come back to this from the Tenants page").                     |        |

### 3.5 Resume & ordering

| ID     | Test                                                 | Steps                                                                                                                            | Expected                                                                                                                                          | Result |
| ------ | ---------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| ONB-30 | Resume after browser close mid-step-2                | Mid-step-2 (form un-saved), close browser. Sign back in.                                                                          | Lands on `/onboarding/close-settings` again. (Form values not preserved — that's OK; the page is stateless.)                                     |        |
| ONB-31 | Resume after step-3 without sending                  | Reach step 3, do nothing, close browser. Sign back in.                                                                            | Lands on `/onboarding/preflight`. Customer grid loads fresh.                                                                                     |        |
| ONB-32 | Active PM never sees onboarding URLs                 | Once onboarded, hit `/onboarding/connect` directly.                                                                              | Redirected to `/`. Onboarding shell does not render.                                                                                              |        |
| ONB-33 | Legacy PM (DB-seeded Active before v12) doesn't loop | Run the v12 migration against a DB with existing Active PMs.                                                                      | The back-fill UPDATE stamps all three step columns with `created_at`. These PMs never re-enter onboarding.                                       |        |

---

## 4. Accounting Connections (QBO + Xero)

| ID    | Test                                          | Steps                                                                                                       | Expected                                                                                                                                                          | Result |
| ----- | --------------------------------------------- | ----------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| ACC-1 | Connect QuickBooks sandbox                    | `/connections` → "Connect QuickBooks" → choose `sandbox_company_US_1` → grant consent.                       | Redirected back to `/onboarding/syncing` then `/`. Row in `integrations.accounting_connections` with provider=QuickBooks, realm_id set, encrypted tokens stored.  |        |
| ACC-2 | Invalid redirect URI                          | Modify the sandbox app's redirect to a wrong value → attempt connect.                                       | Intuit shows the redirect_uri error page. App does not crash. Returning to `/connections` shows still-not-connected state.                                        |        |
| ACC-3 | Connect Xero                                  | Same as ACC-1 against a Xero demo org.                                                                       | Equivalent row in `accounting_connections` for Xero.                                                                                                              |        |
| ACC-4 | Reconnect overwrites tokens                   | Disconnect → connect again to the same realm.                                                                | Token columns updated; no duplicate row.                                                                                                                          |        |
| ACC-5 | Token decryption survives Web ↔ Worker        | Connect via Web. Restart Worker. Trigger a sync from Hangfire.                                               | Worker decrypts tokens written by Web (shared keyring). No CryptographicException in worker logs.                                                                 |        |
| ACC-6 | Concurrent navigation after connect           | Click rapidly between Dashboard ↔ Connections ↔ Accounting menu.                                            | No "A second operation was started on this context" exception. (Regression check for the IDbContextFactory fix.)                                                  |        |
| ACC-7 | Initial sync populates data                   | Right after first connect.                                                                                   | `tenancy.customers` and `tenancy.rent_invoices` get rows. Each invoice with a payment-capable status has `public_payment_url` populated where the provider offers it. |    |

---

## 5. Dashboard

| ID   | Test                              | Steps                                                            | Expected                                                                                                                                       | Result |
| ---- | --------------------------------- | ---------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| DB-1 | Renders with data                 | Visit `/` as a connected PM.                                     | Connected-to badge with the right brand logo. Org name shown. Counts of tenants, invoices, recent runs.                                        |        |
| DB-2 | Drill-down list                   | Click a tile/section.                                            | Navigates to the corresponding list view (Leases / Invoices / Processing status).                                                              |        |
| DB-3 | Empty state                       | Brand-new PM with no synced data yet (mid-sync).                 | "We're still syncing — come back in a minute" or equivalent friendly message. No NREs.                                                          |        |
| DB-4 | Dark mode contrast                | Toggle theme.                                                    | All text reaches AA contrast against background. No washed-out badges.                                                                          |        |

---

## 6. Leases

### 6.1 List page `/leases`

| ID   | Test                            | Steps                                                                                                  | Expected                                                                                                                              | Result |
| ---- | ------------------------------- | ------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| LS-1 | Renders rows                    | Visit `/leases`.                                                                                       | Tabular grid of leases for THIS PM only. No rows from other PMs (cross-tenant check).                                                |        |
| LS-2 | Sort by header                  | Click each sortable column header.                                                                     | Ascending → descending toggle. Arrow indicator updates.                                                                               |        |
| LS-3 | Text search                     | Type a tenant name fragment.                                                                           | Grid narrows live to matching rows. Clearing input restores full list.                                                                |        |
| LS-4 | Bulk select + apply notice rule | Tick 2+ rows → "Bulk apply" → choose template + cadence → confirm.                                     | Each selected lease's `lease_notice_settings` updated. Toast confirms count.                                                          |        |
| LS-5 | Row click → detail              | Click any row.                                                                                         | Navigates to `/leases/{id}`.                                                                                                          |        |
| LS-6 | Empty/no-leases                 | Fresh PM with no leases.                                                                               | Friendly empty state with CTAs: "New lease" and "Import rent roll".                                                                   |        |

### 6.2 Lease detail `/leases/{id}` (Tier 2)

| ID    | Test                                  | Steps                                                                                  | Expected                                                                                                                            | Result |
| ----- | ------------------------------------- | -------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- | ------ |
| LSD-1 | Tenant card                            | Open a lease.                                                                          | Shows tenant name, email, phone (if known), customer link to QBO/Xero.                                                              |        |
| LSD-2 | Activity timeline                     | Same.                                                                                  | Lists invoices, notices sent, late fees assessed, payments — most-recent first.                                                     |        |
| LSD-3 | Inline notice settings (pending #64)  | Locate the notice-settings panel.                                                      | Read-only display for now (acceptable). When editor lands, edits persist + reflect immediately.                                     |        |
| LSD-4 | Manual notice (pending #65)            | Click "Send manual notice".                                                            | Either functional, or a clearly disabled button with "Coming soon" tooltip. No 500.                                                  |        |
| LSD-5 | Waive fee                              | If an unpaid late fee exists, click "Waive".                                            | Fee marked waived in DB; timeline shows the waive event. Confirmation prompt before action.                                          |        |
| LSD-6 | Pay-Now link rendering                 | Invoice row with `public_payment_url` populated.                                       | "Pay Now Online" link opens the QBO/Xero hosted payment page in a new tab. Rel=noopener.                                            |        |

### 6.3 Manual lease creation

| ID    | Test                            | Steps                                                                                | Expected                                                                                                                          | Result |
| ----- | ------------------------------- | ------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------- | ------ |
| LSN-1 | New lease modal                 | `/leases` → "New lease" → fill all fields → save.                                    | Row appears at top of grid. Persists across refresh.                                                                              |        |
| LSN-2 | Required field validation       | Save with missing tenant or rent amount.                                              | Inline validation. Save button disabled or error toast. No partial DB write.                                                      |        |
| LSN-3 | Date sanity                     | End date before start date.                                                          | Validation message blocks save.                                                                                                   |        |

### 6.4 Rent-roll Excel import `/rent-roll/import`

| ID    | Test                          | Steps                                                                          | Expected                                                                                                                                                  | Result |
| ----- | ----------------------------- | ------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| RR-1  | Happy path                    | Upload a clean `.xlsx` matching the documented column layout → review → import. | Preview table renders. Confirm creates N lease rows. Toast shows imported count.                                                                          |        |
| RR-2  | CSV upload                    | Upload `.csv` instead of `.xlsx`.                                              | Same flow works.                                                                                                                                          |        |
| RR-3  | Bad headers                   | Upload a file missing a required column.                                       | Error highlights the missing column. No DB write.                                                                                                         |        |
| RR-4  | Dirty data                    | Rows with blank tenant name, negative rent, malformed email.                   | Each bad row flagged in preview. PM can deselect and import the rest.                                                                                     |        |
| RR-5  | Huge file                     | Upload 5k-row file.                                                            | Browser doesn't hang. Server processes within reasonable time (< 30 s for 5k rows in Dev).                                                                |        |

---

## 7. Customer ↔ Lease Linking `/link-customers`

| ID    | Test                          | Steps                                                                  | Expected                                                                                                                       | Result |
| ----- | ----------------------------- | ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ | ------ |
| LNK-1 | Suggested matches             | Visit page after sync.                                                  | Fuzzy match suggestions between QB customers and existing leases. Confidence indicated.                                       |        |
| LNK-2 | Confirm link                  | Accept a suggestion.                                                    | `tenancy.leases.customer_id` updated. Suggestion list refreshes.                                                              |        |
| LNK-3 | Manual search & link          | Search by name, pick a customer, link to a lease.                       | Link persists.                                                                                                                |        |
| LNK-4 | Unlink                        | Detach an existing link.                                                | `customer_id` cleared.                                                                                                        |        |

---

## 8. Notice Preferences `/notice-preferences`

| ID    | Test                                       | Steps                                                                                       | Expected                                                                                                                          | Result |
| ----- | ------------------------------------------ | ------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- | ------ |
| NP-1  | Page loads with state defaults             | First visit after registration.                                                              | Defaults pre-filled from `state_defaults` for the PM's state. State select honored on change (no blank UI).                       |        |
| NP-2  | State change updates defaults              | Switch state dropdown.                                                                       | Default values reload for the new state.                                                                                          |        |
| NP-3  | Edit-template modal opens                  | Click "View/Edit template" next to a notice type.                                            | MODAL opens (NOT navigation). Page URL unchanged.                                                                                 |        |
| NP-4  | Keyword insertion                          | In the modal, click a keyword chip.                                                          | Inserted at the current cursor position (not appended).                                                                           |        |
| NP-5  | Design / Preview tabs                      | Switch tabs.                                                                                 | Both clearly visible (high-emphasis styling). Preview reflects current draft including theme.                                     |        |
| NP-6  | Theme picker                               | Choose 2+ different prebuilt themes.                                                         | Preview re-skins with that theme. Saved selection persists.                                                                       |        |
| NP-7  | Save                                       | Edit body + subject → Save.                                                                  | Modal closes. Returning shows the saved copy.                                                                                     |        |
| NP-8  | Discard                                    | Make changes → Cancel.                                                                       | No DB write.                                                                                                                       |        |

---

## 9. Templates `/templates`

Templates page should mirror the Notice Preferences capabilities.

| ID   | Test                            | Steps                                                       | Expected                                                                                                  | Result |
| ---- | ------------------------------- | ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- | ------ |
| TP-1 | Keyword insertion               | Click a keyword chip in the editor.                          | Inserted at cursor.                                                                                       |        |
| TP-2 | Design / Preview                | Switch tabs.                                                 | Both visible with emphasis. Preview matches current draft.                                                |        |
| TP-3 | Theme presets                   | Pick a theme.                                                | Preview re-skins. Saved.                                                                                  |        |
| TP-4 | Reset to system default         | Use "Revert" / "Reset".                                      | PM override cleared; system template re-displayed.                                                        |        |
| TP-5 | Service collision (regression)  | Page loads at all.                                            | No "Templates injection collision" runtime error (regression: renamed service to TemplateResolver).       |        |

---

## 10. Invoices `/invoices`

| ID    | Test                          | Steps                                                  | Expected                                                                                  | Result |
| ----- | ----------------------------- | ------------------------------------------------------ | ----------------------------------------------------------------------------------------- | ------ |
| INV-1 | Renders                       | Visit page.                                            | Grid of synced invoices for this PM.                                                      |        |
| INV-2 | Status filters work           | Click "Paid".                                          | List narrows to paid only. Same for Open / Overdue.                                       |        |
| INV-3 | Keyword search                | Type tenant or invoice #.                              | List filters live.                                                                        |        |
| INV-4 | Column sort                   | Click each header.                                     | Asc/desc toggle works.                                                                    |        |
| INV-5 | Pay-Now link present          | Row with `public_payment_url`.                         | Link opens hosted payment page in new tab.                                                |        |

---

## 11. Processing Status `/processing`

| ID    | Test                          | Steps                                                        | Expected                                                                                | Result |
| ----- | ----------------------------- | ------------------------------------------------------------ | --------------------------------------------------------------------------------------- | ------ |
| PR-1  | Lists recent runs             | Visit after a nightly run.                                   | Each run shows date, success/failure, counts (reminders, fees, emails).                |        |
| PR-2  | Drill into a run              | Click a row.                                                  | Details of actions taken with timestamps.                                              |        |
| PR-3  | Failure surfaces              | Trigger a deliberate failure (bad SMTP).                      | Run row shows failure indicator. Detail explains.                                       |        |

---

## 12. Worker Nightly Pipeline

The worker fires per PM at their local midnight. To test in Dev, enqueue manually from Hangfire dashboard or shorten the cron.

| ID    | Test                                            | Steps                                                                                                                  | Expected                                                                                                                                                                                              | Result |
| ----- | ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| WK-1  | Sweep filters by Active status                  | Set one PM to status 4, another to status 2. Trigger sweep.                                                            | Orchestrator runs only for the Active PM. The other is skipped.                                                                                                                                       |        |
| WK-2  | Business date = local                           | Set TimeZoneId="America/Los_Angeles" on a PM; trigger sweep at 09:00 UTC (=02:00 PT).                                  | Business date passed to orchestrator equals the PT calendar date.                                                                                                                                     |        |
| WK-3  | Account-integrity check                         | Disconnect QBO. Trigger sweep.                                                                                          | Run is skipped with a clear log entry ("connection not ready"). No notices/fees attempted.                                                                                                            |        |
| WK-4  | Reminder step                                   | Have an upcoming due invoice for a lease with a reminder rule.                                                          | Reminder notice is dispatched (email / SMS per pref). Row in `notices.dispatched`. Tenant receives email in inbox.                                                                                    |        |
| WK-5  | Late-fee assessment                             | Have a past-due invoice meeting the configured rule (days late + grace).                                                | Assessment row created in `billing.assessments`. Fee amount matches the resolved rule. Idempotency key prevents double-assessment on rerun.                                                           |        |
| WK-6  | Late-fee notice                                 | After WK-5.                                                                                                              | Tenant receives a late-fee notice rendered from the configured template + theme.                                                                                                                       |        |
| WK-7  | Daily Close email arrives                       | Chain completes for a PM with an email on file.                                                                          | PM inbox receives the Daily Close Report email. Totals match the day's reminders + fees + dispatches.                                                                                                  |        |
| WK-8  | PM with no email skips Close                    | Clear primary email on the PM. Trigger.                                                                                  | Warning logged ("no primary email — skipping"). Pipeline does not crash. No NRE.                                                                                                                       |        |
| WK-9  | Idempotency of full chain                       | Trigger the same (PM, business_date) twice.                                                                              | Second run logs "already processed" and short-circuits. No duplicate emails sent.                                                                                                                      |        |
| WK-10 | Hangfire continuation order                     | Watch dashboard during a sweep.                                                                                          | Orchestrator job runs → on success, DailyClose continuation fires for that PM. Order is observable in the Jobs UI.                                                                                    |        |
| WK-11 | Worker resilience to transient SQL              | Stop SQL briefly during a run, then restart.                                                                              | Worker retries via Hangfire. Eventually succeeds. No data corruption.                                                                                                                                 |        |

---

## 13. Daily Close Report Email

| ID    | Test                                  | Steps                                                                                  | Expected                                                                                                                            | Result |
| ----- | ------------------------------------- | -------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- | ------ |
| DC-1  | HTML rendering                        | Open the email in a mail client.                                                       | Renders cleanly in Gmail, Outlook web, and a mobile mail app. Brand chrome present.                                                |        |
| DC-2  | Plain-text fallback                   | View source / "Show original".                                                          | Plain-text alternative present and readable. No `{{placeholder}}` left unrendered.                                                  |        |
| DC-3  | Totals accuracy                       | Count actual notices/fees for the day; compare.                                         | Numbers in the email match.                                                                                                         |        |
| DC-4  | Cultural formatting                   | Inspect dates and currency.                                                              | Invariant culture used (no locale leakage causing "12.34" vs "12,34" inconsistency).                                                 |        |

---

## 14. Hangfire Dashboard `/hangfire`

| ID   | Test                          | Steps                                                | Expected                                                                                                                 | Result |
| ---- | ----------------------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------ | ------ |
| HF-1 | Auth gate                     | Sign out → visit `/hangfire`.                        | Redirects to Login (filter requires auth).                                                                               |        |
| HF-2 | Authenticated access          | Sign in as any user.                                  | Dashboard renders. Recurring jobs visible.                                                                               |        |
| HF-3 | Trigger ad-hoc                | Click "Trigger now" on the sweep.                     | A new run enqueues and executes. Logs land in `logs.events`.                                                             |        |

---

## 15. Admin Worker Logs `/admin/worker-logs`

| ID    | Test                              | Steps                                                                                  | Expected                                                                                                                                          | Result |
| ----- | --------------------------------- | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| ADM-1 | Basic-Auth prompt                  | Browse `/admin/worker-logs` in a private window.                                       | Browser shows native Basic Auth prompt with realm "DueMap Admin".                                                                                  |        |
| ADM-2 | Wrong credentials                  | Submit bad user/password.                                                                | 401 returned. Prompt re-shows. Page never renders.                                                                                                |        |
| ADM-3 | Correct credentials                | Submit the developer credentials.                                                       | Page renders inside the bare Admin layout (no PM sidebar). Log rows visible.                                                                       |        |
| ADM-4 | PM cookie does NOT bypass         | Sign in as a PM in another tab. Browse `/admin/worker-logs`.                            | Still prompts for Basic Auth — PM cookie is ignored on this path.                                                                                  |        |
| ADM-5 | Severity filter                   | Pick "Error+".                                                                            | List narrows to Error + Fatal only.                                                                                                                |        |
| ADM-6 | PM id filter                      | Enter a known PM id.                                                                     | Rows narrow to that PM (uses the `PmId` scope from `BeginScope`).                                                                                  |        |
| ADM-7 | Time window                       | Switch 1h / 6h / 24h / 7d.                                                               | Result set scales. "Newest first" ordering preserved.                                                                                              |        |
| ADM-8 | Search                            | Type a known exception fragment.                                                          | Matching rows surfaced via `LIKE` over message + exception + source_context.                                                                       |        |
| ADM-9 | Row expand                        | Click a row that has an exception.                                                       | Expands to show the stack trace + properties JSON. Click again collapses.                                                                          |        |
| ADM-10 | Empty state                       | Tighten filters until no rows match.                                                     | Friendly empty state. No table chrome left dangling.                                                                                               |        |
| ADM-11 | Logout effect                     | Close all browser windows → reopen → `/admin/worker-logs`.                                | Basic Auth prompt re-appears (creds not persisted beyond the browser session).                                                                     |        |

---

## 16. Multi-Tenant Isolation (CRITICAL)

These must pass. Any failure here is a P0 security defect.

| ID    | Test                                  | Steps                                                                                                            | Expected                                                                                  | Result |
| ----- | ------------------------------------- | ---------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- | ------ |
| MT-1  | Leases isolation                      | PM A signs in → notes a lease id. PM B signs in → visits `/leases/{A's id}`.                                     | 404 / forbidden. **Never** renders A's data.                                              |        |
| MT-2  | Invoices isolation                    | Same with invoices.                                                                                                | Same.                                                                                     |        |
| MT-3  | Templates / settings isolation        | PM A edits a template. PM B opens templates.                                                                       | B sees their own (or system) copy — not A's overrides.                                    |        |
| MT-4  | Customers isolation                   | Same with `/link-customers`.                                                                                       | Each PM sees only their synced customers.                                                 |        |
| MT-5  | Processing status isolation           | PM A's runs visible to A only.                                                                                     | Same.                                                                                     |        |
| MT-6  | Direct API guesses                    | Hit any minimal-API endpoint with a forged PM id parameter (e.g. `/oauth/quickbooks/connect/{otherPmId}`).         | Server enforces caller identity — either rejects or maps to caller's own PM id.            |        |
| MT-7  | Worker logs cross-tenant (known gap)  | PM signs in, attempts `/admin/worker-logs`.                                                                        | Basic Auth blocks (gate fronting the page). Note: reader itself doesn't tenant-scope yet. |        |

---

## 17. Theme / Dark Mode

| ID    | Test                          | Steps                              | Expected                                                                                  | Result |
| ----- | ----------------------------- | ---------------------------------- | ----------------------------------------------------------------------------------------- | ------ |
| THM-1 | Toggle persists               | Toggle dark mode → reload.         | Setting survives reload (localStorage).                                                   |        |
| THM-2 | No FOUC                       | Hard refresh.                      | No light-mode flash before dark applies.                                                  |        |
| THM-3 | All pages dark-compliant      | Walk through every page.            | No hardcoded light backgrounds, no unreadable text.                                       |        |

---

## 18. Responsive / Mobile (spot-check)

| ID    | Test                          | Steps                                          | Expected                                                                                  | Result |
| ----- | ----------------------------- | ---------------------------------------------- | ----------------------------------------------------------------------------------------- | ------ |
| RSP-1 | Login on mobile               | DevTools → iPhone 12 viewport.                  | Buttons reachable, no horizontal scroll.                                                  |        |
| RSP-2 | Dashboard                     | Same.                                            | Cards stack; sidebar collapses to off-canvas / hamburger.                                  |        |
| RSP-3 | Leases grid                   | Same.                                            | Horizontal scroll on the grid is acceptable; headers and search bar usable.               |        |
| RSP-4 | Notice editor modal           | Same.                                            | Modal scrolls inside the viewport; buttons not clipped.                                   |        |

---

## 19. Accessibility Smoke

| ID   | Test                          | Steps                                          | Expected                                                                                  | Result |
| ---- | ----------------------------- | ---------------------------------------------- | ----------------------------------------------------------------------------------------- | ------ |
| A-1  | Tab order                     | Tab through Login.                              | Logical order, visible focus ring on every interactive element.                            |        |
| A-2  | Form labels                   | Inspect form fields.                            | Every input has an associated `<label>` (or aria-label).                                  |        |
| A-3  | Color contrast                | Spot-check pills (Level pills on Worker Logs).  | All pills meet WCAG AA against their background in both themes.                            |        |
| A-4  | Keyboard close on modals      | Open Edit-template modal → Esc.                 | Modal closes. Focus returns to the trigger button.                                         |        |

---

## 20. Error & Edge Cases

| ID    | Test                                  | Steps                                                                | Expected                                                                                  | Result |
| ----- | ------------------------------------- | -------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- | ------ |
| ERR-1 | 404                                   | Visit `/nonsense`.                                                    | Friendly 404 page. Sidebar still functional.                                              |        |
| ERR-2 | 500 surfaces                          | Force a server error (e.g. delete a row referenced by a page).        | Friendly error page in Prod env; dev page in Dev. Serilog captures the exception in `logs.events`. |  |
| ERR-3 | Slow network                          | Throttle to 3G.                                                       | Loading states show; no blank screens.                                                    |        |
| ERR-4 | Long input                            | Paste 10 kB into a single-line field.                                  | Validation rejects or truncates. No SQL truncation surprises.                              |        |
| ERR-5 | Special chars in template body         | Type `<script>`, emoji, RTL text.                                      | Stored safely. Preview renders as text (not executed). Emails escape properly.            |        |

---

## 21. Security Spot-Checks

| ID    | Test                          | Steps                                                                                | Expected                                                                                  | Result |
| ----- | ----------------------------- | ------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------- | ------ |
| SEC-1 | Cookies flags                 | DevTools → Application → Cookies.                                                     | Auth cookie: HttpOnly, Secure, SameSite=Lax.                                              |        |
| SEC-2 | CSRF on POST Razor pages      | Submit Login with no token (via curl).                                                | Rejected.                                                                                 |        |
| SEC-3 | SQL injection on search       | Try `'; DROP TABLE x; --` in every search box.                                        | Treated as literal text. No errors. No schema effects.                                    |        |
| SEC-4 | OAuth state validation        | Tamper with `state` on `/oauth/{provider}/callback`.                                  | Server rejects callback. No connection persisted.                                         |        |
| SEC-5 | Hangfire access               | Anonymous browser to `/hangfire`.                                                     | Auth filter blocks.                                                                       |        |
| SEC-6 | Webhook endpoints anon-only   | POST without signature.                                                                | Returns 200 today (TODO: signature verification). Document expected status here.          |        |
| SEC-7 | Secret hygiene                | Grep config files committed to git.                                                    | No ClientId/Secret in `appsettings*.json`. User-secrets is the source of truth.           |        |

---

## 22. Regression Sweep (known fragile spots)

| ID    | Test                                                     | Watch for                                                                                                          | Result |
| ----- | -------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ | ------ |
| REG-1 | Rapid navigation between Dashboard / Accounting / Leases | "A second operation was started on this context."                                                                  |        |
| REG-2 | Logout                                                    | HTTP 400 antiforgery.                                                                                              |        |
| REG-3 | Notice preferences state dropdown                        | Defaults not populating / blank text on state change.                                                              |        |
| REG-4 | Razor compile of code blocks                              | Compilation error referencing raw string literals inside `@code`.                                                 |        |
| REG-5 | Schema bootstrap on fresh DB                              | "Invalid column name" or "object already exists" — bootstrap should baseline cleanly either way.                  |        |
| REG-6 | Worker SQL sink                                           | NU1605 warnings on build; missing `logs.events` table on first start.                                              |        |
| REG-7 | Concurrent IDbContext                                    | Same error class as REG-1, surfaced from non-Integrations modules — file as task #71 evidence.                    |        |

---

## 23. Exit Criteria

The build is **ready for release** when:

1. Every row in sections 1, 2, 3, 4, 12, 13, 16, 21 is **Pass**.
2. Section 22 (regression sweep) is fully **Pass**.
3. No P0/P1 defects open.
4. Worker has run uninterrupted for at least one full night across two PMs in different time zones, with Daily Close emails received by both.
5. `/admin/worker-logs` confirms zero unhandled exceptions during the test window.

---

## 24. Defect Reporting Template

```
Test ID: <e.g. WK-5>
Build: <git SHA or build number>
Environment: Dev / Staging / Prod
Browser: <name + version>
Severity: P0 / P1 / P2 / P3
Steps to reproduce:
  1.
  2.
Expected:
Actual:
Screenshots / log row id (from /admin/worker-logs):
Repro rate: <1/1, 3/5, etc.>
```

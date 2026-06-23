# DueMap — Osen theme (Tailwind v4) · Claude Code handoff

A drop-in theme layer that brings the **Coderthemes “Osen”** look to the DueMap
Blazor admin, built for **Tailwind CSS v4**. Light + dark, one-line accent.

```
theme/
├─ app.css     ← Tailwind v4 entry: tokens (@theme) + base + component classes
└─ theme.js    ← light/dark toggle, no-flash init, persistence
DueMap Osen Style Guide.dc.html   ← live preview of every component (open in browser)
README.md      ← this file
```

Open **`DueMap Osen Style Guide.dc.html`** to see the target look. Toggle dark mode
and click the accent swatches (top-right) to preview the three accent options.

---

## 1. Install

**Fonts** — `app.css` pulls Inter from Google Fonts so it works with zero config.
For production/offline, self-host instead:

```bash
npm i @fontsource-variable/inter
```
…then in `app.css` replace the Google `@import url(...Inter...)` line with:
```css
@import "@fontsource-variable/inter";
```

**Stylesheet** — make `theme/app.css` your Tailwind entry (it already
`@import "tailwindcss"`). With the Tailwind CLI:

```bash
npx @tailwindcss/cli -i ./theme/app.css -o ./wwwroot/css/app.css --watch
```
Then reference the built file in `App.razor` / `_Host.cshtml`:
```html
<link rel="stylesheet" href="css/app.css" />
```

**Toggle script** — add to `<head>` **before** the stylesheet so the saved theme
applies with no flash:
```html
<script src="theme/theme.js"></script>
```

**Default mode is light.** To follow the OS instead, add `data-default="system"`
to the `<html>` tag.

---

## 2. Using the theme

Two interchangeable ways — use whichever fits the Razor component.

**Component classes** (closest to Osen, least markup):
```html
<button class="btn btn-primary">Sign in</button>
<div class="card"><div class="card-body">…</div></div>
<span class="badge badge-success"><span class="dot"></span>Paid</span>
<input class="form-control" /> · <select class="form-select">…</select>
```

**Token utilities** (the tokens are real Tailwind colors, auto-flipping for dark):
```html
<div class="bg-surface text-base border border-line rounded-card shadow-card">…</div>
<p class="text-muted">…</p>  <a class="text-primary">…</a>
```

### Tokens (all flip light↔dark automatically)
| Utility | Meaning |
|---|---|
| `bg-app` | page background |
| `bg-surface` / `bg-surface-2` | card / subtle (rows, hovers, fields) |
| `border-line` | hairlines & input borders |
| `text-heading` / `text-base` / `text-muted` | headings / body / captions |
| `bg-primary` `text-primary` `bg-primary-soft` `text-primary-soft-fg` | accent + tint |
| `*-success` `*-info` `*-warning` `*-danger` (+ `-soft` / `-soft-fg`) | status |
| `rounded-field` (6px) `rounded-card` (8px) | radii |
| `shadow-card` `shadow-pop` `shadow-modal` | elevation (the Osen glow) |

### Component classes shipped
`btn` (+ `btn-primary/secondary/success/danger/soft-primary/outline-primary/ghost/link`,
`btn-sm/lg/icon`) · `card` (+ `card-header/body/footer/title/subtitle`, `card-link`) ·
`form-label/control/select/textarea/hint`, `form-check`, `form-switch` (`.track`+`.thumb`),
`is-invalid` · `badge` (+ family, `.dot`) · `alert` (+ family) · `table` (+ `cell-strong`,
`tabular`) · `nav-section`, `menu-item`(`.active`) · `page-title`, `brand-mark`, `divider`.

### Change the accent
One line in `app.css` (`:root { --primary: … }`). Hover/soft/soft-fg shades are
derived via `color-mix`, so the whole app re-skins. Osen-friendly options:
`#727cf5` (Coderthemes blue, default) · `#4b6bff` (royal) · `#7c5cfc` (violet).

### Toggle the theme
```html
<button onclick="DueMapTheme.toggle()">…</button>
```
`DueMapTheme.set('light'|'dark'|'system')`, `.current()`, `.choice()`. A
`duemap:themechange` event fires on `document` (re-render ApexCharts / Blazor interop here).

---

## 3. Screen-by-screen mapping

Apply to the attached Razor pages. Currency is **$**.

### Login / `Account/Login`
- Centered column on `bg-app`, max-width ~420px. `brand-mark` + app name + subtitle.
- The whole form sits in a `card` (`card-body`, generous padding).
- “Sign in with Intuit” = `btn btn-primary w-full`; “Sign in with Xero” =
  full-width `btn` with the Xero brand colour (keep its own blue, `rounded-full`).
- “OR SIGN IN WITH EMAIL” divider = `text-muted` 12px uppercase between two `divider` lines.
- Email/password = `form-label` + `form-control`. “Forgot?” = `btn-link`, right-aligned.
- “Keep me signed in” = `form-check`. Primary submit = `btn btn-primary w-full` with trailing arrow.
- Footer links (Terms · Privacy · Status) = `text-muted` with `divider`-coloured separators.

### App shell (every signed-in page)
- Left **sidebar** `bg-surface`, `border-line` right edge, width ~240px. Group headers =
  `nav-section`; items = `menu-item`, active route gets `menu-item active`.
- Bottom of sidebar: avatar (`brand-mark`-style circle) + email, in a `menu-item`.
- **Topbar** h-14, `bg-surface`, `border-line` bottom: page context left; email +
  `btn btn-sm btn-secondary` “Sign out” + theme-toggle `btn btn-icon btn-ghost` right.

### Dashboard / `Index` (Portfolio overview)
- `page-title` + a `badge badge-success` “Connected to QuickBooks” + muted realm/last-sync.
- Action row: `btn btn-secondary` “Take a tour”, “Manage connection”.
- 4 KPI cards = `card card-body`: 11px uppercase muted label + 2xl `text-heading` number +
  muted sub-line. Overdue number uses `text-danger`.
- “Recent invoices” = `card` with header (title + `btn-link` “View all”) over a `table`.
  Status column = `badge badge-danger` (Overdue) / `badge badge-success` (Paid).
- “Setup” / “Portfolio” = `card` lists of `menu-item`-style rows with a trailing chevron.

### Accounting / `Accounting`
- Intro `page-title` + muted description.
- Connection `card`: provider mark, name + `badge badge-success` “Connected”, muted meta.
  A `divider`, then a 3-col stat grid (LAST SYNC / TOKEN EXPIRES / CONNECTED ON) =
  uppercase muted labels + `text-heading` values.
- Footer actions: `btn btn-secondary` “Sync now” & “Reconnect”; “Disconnect” = `btn-link`
  in `text-danger`.

### Notice preferences / `NoticePreferences`
- `page-title` + description (inline `text-primary` link to Leases).
- Each notice type = a `card`: header row with an icon tile (`bg-primary-soft text-primary-soft-fg`
  rounded-field), title + muted sub, and a `form-switch` master toggle on the right.
- Body (`card-body`, `border-line` top): number `form-control` (“Days before due date”),
  a `form-select` (“Mode”), and `card`-inset “Edit template” rows (`btn btn-secondary btn-sm`).
- Legal callout = `alert alert-warning` (the scales icon).
- Bottom-right `btn btn-primary` “Save preferences” with a check icon.

### Templates list / `Templates`
- `page-title` + muted description (layering note).
- `card`: header “Existing overrides” + `btn btn-primary` “New override”.
- `table`: NOTICE TYPE / STATE / SUBJECT (subject in `tabular`/mono is fine) / UPDATED /
  actions (`btn btn-secondary btn-sm` Edit, `btn-link` text-danger Remove).

### Edit template modal
- Overlay `bg-[rgb(49_58_70/.35)] backdrop-blur` + centered `card max-w-lg`,
  `shadow-modal`. `card-header` (title + sub + close `btn btn-icon btn-ghost`),
  `card-body`, `card-footer` (Cancel `btn-ghost` / Save `btn-primary`).
- Notice-type / State = `form-select`. Info note (“No system default…”) = `alert alert-primary`
  with a `btn-link` “Reset to default”.
- “Email design” chips = pill `badge`s coloured per tone (Standard `badge-primary`,
  Friendly `badge-success`, Warning `badge-warning`, Urgent `badge-danger`); selected gets a
  `ring-2 ring-primary`.
- Design/Preview tabs = underline tabs (active: `text-primary` + 2px primary bottom border).
- Placeholder chips (`tenant_name`, `amount_owed`, …) = `badge badge-primary` (clickable).

### Leases / `Leases`
- `page-title` + a `badge badge-neutral` “4 active” + muted description.
- `btn btn-secondary` “Import rent roll” + `btn btn-primary` “New lease”.
- Search `form-control` + filter chips (`badge`-style toggle buttons; active = `btn-primary`,
  rest = `btn-secondary btn-sm`, with a count).
- `card` + `table`: checkbox column (`form-check`), TENANT/LEASE, STATUS (`badge-success` Current /
  `badge-warning` In grace / `badge-danger` Overdue), RENT/BALANCE (`tabular`, `$`),
  LAST NOTICE, and a SETTINGS cell of three `btn btn-icon btn-ghost btn-sm` (bell/calendar/file)
  in `text-primary`.

### Invoices / `Invoices`
- Same header pattern; description links to Notice preferences (`text-primary`).
- Filter chips: All / Open / Overdue / Paid / Unlinked (active `btn-primary`).
- `table`: INVOICE # (`cell-strong`), CUSTOMER, DUE (sortable — chevron in header), AMOUNT &
  BALANCE (`tabular`, right-aligned, `$`), STATUS badge, LEASE (“unlinked” = `text-warning`).

### Link customers / `LinkCustomers`
- `page-title` (“Customer ↔ Lease mapping”) + muted description.
- Stat bar `card`: Total leases / Linked (`text-success`) / Unmapped (`text-warning`) /
  Customers synced — labels muted, numbers `text-heading`.
- `card` + `table`: LEASE, RENT (`tabular`, `$`), START, CURRENT CUSTOMER
  (`badge badge-warning` “Unmapped”), REASSIGN (`form-select`), and `btn btn-primary btn-sm` “Link”.

---

## 4. Notes & assumptions
- The palette reproduces Osen’s **visual language** (Coderthemes’ signature
  `#727cf5 / #0acf97 / #39afd1 / #ffbc00 / #fa5c7c`, soft slate neutrals, the
  `0 0 35px rgba(154,161,171,.15)` card glow). If you have the licensed Osen build, you can
  paste its exact `--bs-primary` etc. over the `:root` values in `app.css` — nothing else changes.
- Icons in the screens are simple line icons; the style guide inlines a few. Osen ships the
  **Tabler** icon font — `npm i @tabler/icons-webfont` and use `<i class="ti ti-bell"></i>`,
  or keep your inline SVGs (they inherit `currentColor`, so they already theme correctly).
- The style guide preview uses the Tailwind Play CDN to render in a browser; your real app
  compiles `app.css`. Class names are identical between the two.
```

# CLAUDE.md — implement the DueMap Osen theme

You are restyling an existing **.NET Core Blazor** app (Razor pages/components) to
match the **Osen** admin look. Everything you need is in this folder. Do **not**
redesign features or change behaviour — only apply the visual system.

## What's here
- `theme/app.css` — Tailwind v4 entry. Design tokens (`@theme`), base styles, and
  component classes (`.btn`, `.card`, `.form-control`, `.badge`, `.table`, `.menu-item`…).
- `theme/theme.js` — light/dark toggle (no-flash, persisted). Default = light.
- `StyleGuide.html` — open in a browser. This is the **target look** for every
  component. Match it.
- `README.md` — full token reference + **screen-by-screen mapping** for each page.

## Do this, in order
1. **Wire up the build.** Confirm the project is Tailwind v4. Make `theme/app.css`
   the Tailwind input (it already `@import "tailwindcss"`). Add the built CSS and
   `theme/theme.js` to the host page (`App.razor` or `_Host.cshtml` / `index.html`):
   ```html
   <script src="theme/theme.js"></script>          <!-- in <head>, before the CSS -->
   <link rel="stylesheet" href="css/app.css" />
   ```
   Build command (CLI): `npx @tailwindcss/cli -i ./theme/app.css -o ./wwwroot/css/app.css --watch`.
   Place `theme/app.css` and `theme/theme.js` wherever the project keeps front-end
   assets (commonly `wwwroot/`); fix the paths above to match.

2. **Build the shared layout first** — the sidebar + topbar shell described in
   README §3 ("App shell"). Use `.menu-item` / `.menu-item.active`, `.nav-section`,
   `.brand-mark`. Add a theme-toggle button: `onclick="DueMapTheme.toggle()"`.

3. **Then each page**, following README §3 exactly (Login, Dashboard, Accounting,
   Notice preferences, Templates + Edit-template modal, Leases, Invoices,
   Link customers). Currency is **$**.

4. **Verify** against `StyleGuide.html` in both light and dark mode.

## Rules
- Prefer the shipped component classes; use token utilities (`bg-surface`,
  `text-muted`, `bg-primary`, `border-line`, `shadow-card`, `rounded-card`…) for
  layout. Tokens auto-flip for dark mode — don't hand-write `dark:` overrides for color.
- **Never hard-code hex colours.** Always go through a token. The accent is a single
  line (`--primary` in `app.css`).
- Keep existing inline SVG icons — they use `currentColor` and already theme correctly.
  (Osen ships the Tabler icon font if you'd rather: `npm i @tabler/icons-webfont`.)
- Don't change routes, form logic, validation, or data binding. Visual only.
- Inter is loaded from Google Fonts in `app.css`; for offline/production, see README §1
  (`@fontsource-variable/inter`).

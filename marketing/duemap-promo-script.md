# DueMap — SaaS promo script & production kit

Master film: **~90 seconds**, 1920×1080 (16:9), with cutdown maps for 30s and 15s,
plus a 10-slide sales-deck adaptation. Everything below is grounded in the shipped
product — every feature named exists, every color is a real app token, every UI
shown can be captured from the running app in dark mode.

---

## 1 · Positioning brief

| | |
|---|---|
| **Audience** | Independent property managers and small PM firms (5–500 doors) already on QuickBooks Online or Xero |
| **Core promise** | Rent follow-ups on autopilot — reminders, grace tracking, and state-compliant late-fee warnings, run daily against your real books |
| **Differentiators** | 1. Sits **on top of** QBO/Xero — no migration, books stay the source of truth · 2. **State rules built in** (AZ, CA, FL, GA, NC, NY, TX at launch) · 3. **Never touches money** — notify-and-document only · 4. Promise-to-pay pauses the machine like a human would · 5. Append-only, court-ready notice history |
| **Tone** | Calm, dry, confident. The ad never shouts; the product's whole point is that the shouting stops. |
| **Emotional arc** | Cluttered anxiety (rose) → control (indigo) → resolution (green). The palette literally makes this arc. |

---

## 2 · Ad brand kit

### Color (straight from the product's Osen tokens)

| Role | Hex | Usage in the ad |
|---|---|---|
| Ink (bg) | `#16191f` | Every frame background — the ad lives in the app's dark mode |
| Panel | `#232734` | Cards, mockup chrome |
| Hairline | `#333a49` | Dividers, card borders |
| **Indigo (brand)** | `#727cf5` | Logo, CTAs, one highlighted word per headline — never more |
| Green (resolution) | `#0acf97` | "sent / paid / done" states; owns the last third of the film |
| Amber (attention) | `#ffbc00` | due-today, warning badges |
| Rose (pain) | `#fa5c7c` | Frames 1–2 **only**, then retired for the rest of the film |
| Cyan (sync) | `#39afd1` | Integration/sync moments |
| Text on dark | `#f0f2f7` heading · `#aeb6c4` body · `#7e899b` muted | Matches app dark tokens |

### Typography (Inter, same as the product)

| Style | Spec @1080p | Notes |
|---|---|---|
| Headline | **Inter Tight 800**, 96px, line 1.05, tracking −2% | Fallback Inter 800. One indigo word max per headline |
| Logo lockup | Inter Tight 800, 120px | Spark mark at cap height, 24px gap |
| Sub-line | Inter 500, 40px, `#aeb6c4` | Max 2 lines |
| Eyebrow | Inter 600, 28px, UPPERCASE, tracking +8%, `#7e899b` | Mirrors the app's section labels |
| UI text in mockups | App-native sizes | Money always tabular numerals |
| CTA button | Inter 600, 36px white on `#727cf5`, 12px radius | Same radius family as the app |

### Motion rules

- Standard transition **200ms ease-out** (the app's own timing); hero moves 400ms.
- Elements enter by 24px rise + fade. Badges pop with a 1.06 overshoot.
- Product mockups drift at 2–3% parallax; never zoom past 105% (keeps UI text crisp).
- Safe margins 8% all sides. Also deliver 9:16 and 1:1 crops — headlines stack, mockups center-crop.
- Music: sparse ticking/clutter percussion in frames 1–2, opens into warm minimal synth at the reveal; soft UI ticks under badge pops at low level.

---

## 3 · Master script — frame by frame (~90s)

### F1 · 0:00–0:06 — The chase
- **Visual:** Ink background. A date chip ticks **AUG 1 → AUG 5 → AUG 12**. Clutter piles in around it: a sticky note ("call unit 4B"), a spreadsheet row, two unanswered texts — "Hey, rent this week?". A rose glow builds at the edges.
- **Type:** Eyebrow `EVERY MONTH`. Headline: **"Rent day comes. Then the chasing starts."** — *chasing* in rose `#fa5c7c`.
- **VO:** "Rent day comes every month. So does the chasing — the texts, the spreadsheets, the awkward late-fee math."
- **SFX:** clock ticks, message pings.

### F2 · 0:06–0:12 — Three questions
- **Visual:** Three panels slide in left-to-right: a spreadsheet fragment, a calendar fragment, a blurred statute page.
- **Type:** Three cards, Inter 600 48px: **"Who's paid?" · "Who's still in grace?" · "What can I legally charge?"** — third card amber.
- **VO:** "Who's paid, who's still in grace — and what are you actually allowed to charge?"

### F3 · 0:12–0:17 — Brand reveal
- **Visual:** The clutter collapses inward into a single indigo **spark mark** (the app's logo glyph); wordmark types on beside it. Rose is retired from the film here.
- **Type:** Logo + **DueMap** (Inter Tight 800, 120px, `#f0f2f7`). Tagline below: "Rent follow-ups, **on autopilot**." — highlighted phrase indigo.
- **VO:** "Meet DueMap."
- **SFX:** single warm hit; music opens up.

### F4 · 0:17–0:24 — Plugs into your books
- **Visual:** Official QuickBooks and Xero marks connect to the DueMap node by drawn cyan lines. A pill appears beneath: `● Connected · Your Company` — mirroring the real Connections page.
- **Type:** Eyebrow `WORKS WITH YOUR BOOKS`. Headline: **"Connects to QuickBooks Online & Xero in one click."**
- **VO:** "It plugs into QuickBooks Online or Xero in one click. No migration. Your books stay the source of truth."

### F5 · 0:24–0:30 — Sync
- **Visual:** Rows of tenants and invoices stream into the dark invoices table (real capture). Chip: `Last sync: 2 minutes ago` in cyan.
- **Type:** Headline: **"Tenants & invoices sync themselves."** Sub: "Every day. Automatically."
- **VO:** "Tenants, leases, and invoices sync automatically — every single day."

### F6 · 0:30–0:38 — The Daily Close *(hero feature)*
- **Visual:** A clock ticks to **6:00 PM** with chip `your local time`. A sweep line passes down the lease list; badges pop per row: green `Reminder sent`, amber `Due today`, indigo `Grace — day 2`, amber `Late-fee warning sent`.
- **Type:** Eyebrow `THE DAILY CLOSE`. Headline: **"Every lease. Every due date. Handled."**
- **VO:** "Then, every evening, DueMap closes your day: friendly reminders before rent is due, a nudge on the day, and compliant late-fee warnings after grace."

### F7 · 0:38–0:45 — State compliance
- **Visual:** State chips **AZ · CA · FL · GA · NC · NY · TX** arranged in a row; TX flips into a rule card with three green ticks: `Grace window` · `Fee cap` · `Required wording`.
- **Type:** Eyebrow `BUILT-IN COMPLIANCE`. Headline: **"State rules, baked in."** Sub: "Grace windows · fee caps · required wording."
- **VO:** "Grace windows, fee caps, required wording — state rules are built in, and templates won't let you drop the legal lines."

### F8 · 0:45–0:51 — Never touches money *(trust frame — near-empty on purpose)*
- **Visual:** A lone dollar glyph in a thin circle. A calm indigo line crosses it — then resolves into a checkmark. Nothing else on screen.
- **Type:** Headline: **"DueMap never touches the money."** Sub: "It notifies and documents. Your books do the charging."
- **VO:** "And DueMap never moves money. It reminds, warns, and documents — the charging stays in your books, under your control."

### F9 · 0:51–0:58 — The tenant side
- **Visual:** Phone mockup. A branded notice email opens: invoice **PDF attachment chip**, then a big **Pay now →** button; tap flashes to the provider payment page.
- **Type:** Headline: **"Tenants get one tap to pay."** Sub: "Real invoice attached · Pay Now links to your books."
- **VO:** "Tenants get a clean, branded notice with the real invoice attached — and a Pay Now button straight into your payment page."

### F10 · 0:58–1:06 — Human control (promise-to-pay + Today)
- **Visual:** The **Today** queue (real capture). Click `Log promise` on a tenant card → modal `Promised: Fri, Aug 15` → the lease shows `Notices paused` chip; a timeline entry stamps in.
- **Type:** Headline: **"“I'll pay Friday.” Logged. Paused. Tracked."** — the quote in indigo.
- **VO:** "When a tenant says 'I'll pay Friday,' log it once. DueMap pauses the notices — and resumes automatically if the promise breaks."

### F11 · 1:06–1:12 — Paper trail
- **Visual:** Lease activity timeline scrolls: `Reminder · Aug 1, 6:02 PM CT` … `Late-fee warning · Aug 12`. A small lock glyph and the word `append-only`.
- **Type:** Headline: **"A paper trail you can stand on."** Sub: "Every notice, stamped and kept."
- **VO:** "Every notice is time-stamped and kept for good — documentation that holds up when it matters."

### F12 · 1:12–1:18 — At a glance
- **Visual:** Dark dashboard capture: donut (outstanding by age), grouped bars (invoiced vs collected), Today count tile.
- **Type:** Headline: **"Your whole month, at a glance."**
- **VO:** "The dashboard shows exactly where the month stands — and what needs you today."

### F13 · 1:18–1:24 — Setup
- **Visual:** The real 3-step onboarding stepper animates: `1 Connect` → `2 Set your rules` → `3 Go live`, green ticks landing on each.
- **Type:** Eyebrow `SETUP`. Headline: **"Live in three steps."** Sub: *[proof point — insert real average setup time once measured]*.
- **VO:** "Setup is three steps: connect your books, set your rules, go live."

### F14 · 1:24–1:32 — CTA (hold end card 2s)
- **Visual:** Ink background, spark mark centered. CTA button: **Connect QuickBooks or Xero →**. URL line beneath.
- **Type:** Headline swap: **"Stop chasing rent."** → **"Start closing days."** (*closing* in green). Sub: `duemap.dev` *(confirm final domain before shipping)*.
- **VO:** "DueMap. Rent follow-ups on autopilot. Connect your books — and let the chasing stop."

**VO total ≈ 190 words — comfortable at a calm 2.2 words/sec.**

---

## 4 · Cutdowns

| Cut | Frames (trimmed) | Runtime |
|---|---|---|
| **30s performance ad** | F1 (4s) → F3 (3s) → F4 (4s) → F6 (6s) → F8 (4s) → F9 (4s) → F14 (5s) | ~30s |
| **15s teaser** | F3 (3s) → F6 (5s) → F8 (3s) → F14 (4s) | ~15s |

The 15s teaser is pure differentiator: reveal → daily close → "never touches the money" → CTA.

---

## 5 · Sales-deck adaptation (10 slides)

Same palette and type; headlines become slide titles, VO lines become speaker notes.

| # | Slide | Source frames | On-slide line |
|---|---|---|---|
| 1 | Cover | F3 | DueMap — Rent follow-ups, on autopilot |
| 2 | The problem | F1+F2 | Rent day comes every month. So does the chasing. |
| 3 | How it works | F4+F5+F6 | Connect → Sync → Daily Close (3-node diagram) |
| 4 | The Daily Close | F6 | Every lease. Every due date. Handled. |
| 5 | Compliance | F7 | State rules, baked in (AZ CA FL GA NC NY TX) |
| 6 | Trust | F8 | DueMap never touches the money. |
| 7 | Tenant experience | F9 | One tap to pay — against your real invoice |
| 8 | Human control | F10+F11 | Promises pause the machine. Everything is on the record. |
| 9 | Visibility | F12 | Your whole month, at a glance |
| 10 | Setup + CTA | F13+F14 | Live in three steps — connect your books |

---

## 6 · Real-screen shot list (capture in dark mode, 1280px wide)

1. `/` dashboard — charts populated (use the Xero-connected workspace, 300 invoices)
2. `/today` — queue with mixed priorities
3. `/invoices` — full table + pager ("Showing 1–25 of 300")
4. `/leases` — status chips row (Upcoming/Current/Overdue/Ended)
5. `/leases/{id}` — activity timeline + promise-to-pay modal open
6. Template editor — preview tab showing a rendered notice
7. `/connections` — provider mark, org name, "Last sync" chip
8. Onboarding stepper (any step page header)
9. Tenant notice email — render the HTML from the dev-fallback log (blank the SendGrid key via the temporary launch override, trigger a notice, copy the logged body)
10. `/t/invoices` tenant portal — Pay Now + PDF buttons

---

## 7 · Claims & brand guardrails

- **Never** imply DueMap moves, holds, or collects money — the product is notify-and-document only. No "collections rate" claims.
- **No invented numbers.** Proof-point slots are marked `[proof point]`; fill them with measured data or a named testimonial, or cut the line.
- **State claim discipline:** say "state rules built in" for the **7 launch states** by name. The DB lists all 50 states but only 7 have rule versions — don't claim nationwide coverage.
- **QuickBooks / Xero marks:** use official brand assets under their brand guidelines. Xero assets exist in `wwwroot/img/xero/`; the official QuickBooks logo is still pending drop-in at `wwwroot/img/intuit/` — the ad needs the same licensed asset, from Intuit's brand portal.
- Compliance language: "state-compliant late-fee **warnings**" — never "legal advice."
- Domain `duemap.dev` appears in dev config; confirm the public domain before rendering the end card.

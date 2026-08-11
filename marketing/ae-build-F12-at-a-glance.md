# After Effects build guide — F12 "At a glance" (1:12–1:18)

**1920×1080 · 30 fps · 6 s (180 frames)**. Centred frame, like F8.

**The content area is built natively.** Only the top bar and left menu come from the
capture. Every number, bar, arc, card and label in the content area is an AE layer, so the
dashboard populates instead of sitting there.

---

## 1 · The capture — two roles

Handbook §9: seed, run on port 63055, sign in as **`demo2@duemap.dev` / `DemoUser1!`**,
go to **`/`** in dark mode. **Capture at 2× DPR** (method in `ae-build-F5-sync.md` §0).
Save as `assets/shot-dashboard.png`. **Never press Sync on this workspace.**

The same PNG gets used twice, for different reasons:

| Layer | Role | Renders? |
|---|---|---|
| `CHROME` | The top bar and left menu, alpha-matted to an L-shape | **Yes** |
| `REF_CAPTURE` | The whole dashboard, marked **Guide Layer** | **No** |

`REF_CAPTURE` is how you align everything. Guide layers render in their own comp's viewer
but are excluded from output *and* from the parent comp — the same mechanism as
`BG_PREVIEW`. So you build on top of a pixel-accurate reference that never appears in the
film.

**Every coordinate in §5–§8 is approximate.** Read the exact ones off `REF_CAPTURE` by
toggling your built layer's opacity — that's faster and more accurate than any table I can
write from a mockup.

---

## 2 · Comp setup

1. **Ctrl+N** → `F12_CONTENT`, 1920 × 1080, 30 fps, **`0:00:06:00`** (180 frames)
2. **Ctrl+click the time display** — work in frames
3. Solid `#16191F` named `BG_PREVIEW` at the bottom → **Layer ▸ Guide Layer**
4. **Ctrl+N** → `SHOT_DASH`, **1116 × 704**, 30 fps, 6 s
5. Make a **`Frame-12/`** folder

Building `SHOT_DASH` at artwork size means its anchor comes out correct automatically.

---

## 3 · Layer stacks

```
F12_CONTENT
├── HEADLINE
├── SHOT_DASH                    (precomp, 1116 × 704)
└── BG_PREVIEW                   (Guide Layer)

SHOT_DASH
├── REF_CAPTURE                  full capture · GUIDE LAYER · top of stack
├── SHOT_FRAME                   1116 × 704 R16, stroke only
├── CARD_RECENT                  built
├── CARD_DONUT                   built
├── CARD_BARS                    built
├── TILE_4  TILE_3  TILE_2  TILE_1   built
├── HDR_ROW                      built
├── CHROME                       capture — alpha matte ↓
├── CHROME_MATTE                 two rectangles
└── PANEL_BG                     page-background solid
```

**`REF_CAPTURE` goes at the top, not the bottom.** `PANEL_BG` is an opaque solid covering
the full 1116 × 704, so a reference underneath it is invisible — useless for the one job it
has. On top, you **toggle its eye** to flick between reference and build, which is how you
catch a 3 px offset on 11 px text. Ghosting it at 50% opacity is worse for this; alternating
two clean views is unambiguous.

| | Anchor Point | Position |
|---|---|---|
| `SHOT_DASH` in `F12_CONTENT` | 558, 352 *(automatic)* | 960, 619 |

Panel spans frame x 402 → 1518, y 267 → 971. Add **Drop Shadow** → Opacity 50%,
Distance 12, Size 40, Angle 120°.

---

## 4 · Headline

`Your whole month, at a glance.`

**Inter Tight ExtraBold (800) 96 px**, Tracking −20, `#F0F2F7`, **centre aligned**,
**Anchor Point 0, 0**, Position **960, 200**. No Ctrl+Alt+Home — positioned by baseline,
same as F8.

Recolour `at a glance`, period excluded:

1. **Text ▸ Animate ▸ Fill Color ▸ RGB**
2. `Range Selector 1 ▸ Advanced` → **Units → Index**, Based On stays **Characters**
3. **Start 18**, **End 29** · Fill Color `#727CF5`
4. **Amount** 0% until §9

---

## 5 · Base and chrome

All coordinates below are **local to `SHOT_DASH`** (1116 × 704, origin top-left).

### REF_CAPTURE

Import the PNG, scale to **fill the width** — 1116 ÷ 2560 = **43.6%**. The dashboard page
is taller than the panel, so the bottom crops just under the `Recent Invoices` table
header, matching the mockup.

Position **558, 352** with the top edge flush to y 0. Then **Layer ▸ Guide Layer**.

### PANEL_BG

Solid **1116 × 704** at 558, 352. Colour **sampled with the eyedropper** from the page
background in `REF_CAPTURE` — not typed, because the capture has been scaled and its
values shift a fraction.

### CHROME + CHROME_MATTE

A second copy of the PNG at the same scale and position as `REF_CAPTURE`, **not** a guide
layer.

`CHROME_MATTE` — one shape layer, two rectangles:

| Rectangle | Size | Position | Covers |
|---|---|---|---|
| Top bar | 1116 × **69** | 558, 34 | Logo, admin label, org, Sign out |
| Sidebar | **204** × 635 | 102, 386 | WORKSPACE + PORTFOLIO menus, footer |

Set `CHROME`'s **Track Matte → Alpha Matte "CHROME_MATTE"**.

That L-shape is everything imported. The content area — **x 204 → 1116, y 69 → 704** — is
built from here down.

---

## 6 · Stat tiles

Four precomps. Tile **195 × 98**, Roundness 8, Fill `#232734`, Stroke `#333A49` 1 px.

| Layer | Centre | Label | Value | Sub-line |
|---|---|---|---|---|
| `TILE_1` | **336, 213** | `CUSTOMERS` | `48` | `37 linked to leases` |
| `TILE_2` | **547, 213** | `OPEN INVOICES` | `25` | `$51,750.00 outstanding` |
| `TILE_3` | **758, 213** | `OVERDUE` | `17` | `may trigger a late-fee warning` |
| `TILE_4` | **969, 213** | `ACTIVE LEASES` | `37` | `in your portfolio` |

Pitch **211**. Inside each tile, all left aligned at the tile's left edge + 11:

| Element | Font | Colour | Baseline Y |
|---|---|---|---|
| Label | Inter SemiBold ~11, Tracking +80 | `#7E899B` | 187 |
| Value | Inter Tight ExtraBold ~28 | `#F0F2F7` (Tile 3: **`#FA5C7C`**) | 224 |
| Sub-line | Inter Medium ~11 | `#7E899B` | 244 |

Build `TILE_1` completely, precompose, then duplicate the **comp** three times and edit
the strings. All four instances then take **Anchor Point 336, 213** with Position varying —
the anchor points at the artwork inside the comp, which never moves.

### Making the values count

Per value layer:

1. **Effect ▸ Expression Controls ▸ Slider Control**
2. Alt+click the **Source Text** stopwatch:

```
Math.round(effect("Slider Control")("Slider")).toString()
```

3. Keyframe the **Slider** 0 → final value across the frames in §9

The numbers are left aligned, matching the app, so the digit count changing at 10 nudges
the right edge once. At 28 px that's a couple of pixels, early in the count, and invisible.
Don't centre them to avoid it — that would diverge from the real UI.

---

## 7 · CARD_BARS

Card **497 × 272** at **482, 415**, Roundness 12, Fill `#232734`, Stroke `#333A49` 1 px.

Coordinates below are **local to `CARD_BARS`** (origin at the card's top-left, which is
`SHOT_DASH` 233, 279).

| Element | Spec | Position |
|---|---|---|
| Title | `Collections — last 6 months` — Inter SemiBold ~14, `#F0F2F7` | 16, 28 |
| Subtitle | `Invoiced vs collected, by invoice due month.` — Inter Medium ~11, `#7E899B` | 16, 45 |
| Legend | `Invoiced` `#727CF5` · `Collected` `#0ACF97` — swatch 8 × 8 + ~10 px text | ~361, 72 |
| Y labels | `$80,000` `$60,000` `$40,000` `$20,000` `$0` — ~10, `#7E899B`, **right aligned** at x 82 | 100 · 130 · 160 · 189 · 219 |
| Month labels | `Mar` `Apr` `May` `Jun` `Jul` `Aug` — ~10, `#7E899B`, centred | y 233 |

### The plot

- **Baseline (`$0`) at y 219**
- **`$80,000` at y 100** — so the plot is **119 px tall for $80,000**

```
bar height = (value ÷ 80000) × 119
```

- Six group centres at x **102 · 170 · 237 · 305 · 373 · 440**
- Two bars per group, width **16**, straddling the group centre at **∓9**
- Invoiced `#727CF5` left, Collected `#0ACF97` right

**Read the twelve values off `REF_CAPTURE`** — or better, off the running app. The script's
guardrails forbid invented numbers, and this frame's whole claim is that it's the real
dashboard. When your bars are right they'll sit exactly on top of the reference's; that's
your check.

### Growing them

Each bar is a Rectangle of height *h*. Set:

- **Anchor Point 0, h/2** — the bar's *bottom* edge
- **Position (x, 219)** — sitting on the axis

Then **Scale Y 0 → 100%** grows it upward. Anchored at centre it would grow from the middle
and punch through the axis line. Same mechanic as F5's `ROWS_COVER`, inverted.

Scale is linked by default — **unlink it** and key Y only.

---

## 8 · CARD_DONUT

Card **328 × 272** at **908, 415**, same fill and stroke as `CARD_BARS`.

Coordinates local to the card (origin = `SHOT_DASH` 744, 279).

| Element | Spec | Position |
|---|---|---|
| Title | `Outstanding balance` — Inter SemiBold ~14, `#F0F2F7` | 16, 28 |
| Subtitle | `Where the open money sits by age.` — Inter Medium ~11, `#7E899B` | 16, 45 |
| Centre value | `$51,750` — Inter Tight ExtraBold ~20, `#F0F2F7`, centred + Ctrl+Alt+Home | 166, 144 |
| Centre label | `outstanding` — Inter Medium ~9, `#7E899B`, centred + Ctrl+Alt+Home | 166, 160 |
| Legend | Three dots + labels, ~9 | y ~243 |

### The ring

Three layers, each **Ellipse 127 × 127 at 166, 147**, **no Fill**, **Stroke ~19 px**,
**Add ▸ Trim Paths**:

| Layer | Segment | Colour | Trim Start | Trim End |
|---|---|---|---|---|
| `ARC_NOTDUE` | Not yet due | `#0ACF97` | 0% | 53% |
| `ARC_OVER30` | Overdue < 30d | `#FFBC00` | 53% | 85% |
| `ARC_OVER30PLUS` | Overdue 30+ d | `#FA5C7C` | 85% | 100% |

**Verify those percentages against the app.** Only `15%` and `32%` are legible in the
mockup; 53% is inferred from the remainder.

> AE's ellipse path starts at the top and trims clockwise. If a segment begins in the wrong
> place, **rotate the layer** rather than fighting the trim numbers.

To animate, keyframe each arc's **Trim End** from its Start value up to its End value.
Staggered, the three draw as one continuous sweep.

---

## 9 · CARD_RECENT

Card **839 × 135** at **652, 636**, cropped by the panel's bottom edge — build only what's
visible above y 704.

| Element | Spec |
|---|---|
| Title | `Recent Invoices` — Inter SemiBold ~14, `#F0F2F7` |
| Subtitle | `Latest 6 of 234 synced` — Inter Medium ~11, `#7E899B` |
| `View all →` | Inter Medium ~11, `#727CF5`, right aligned |
| Table header | `INVOICE #` `CUSTOMER` `DUE` `BALANCE` `STATUS` — ~9, `#7E899B`, Tracking +80 |
| One row | `INV-28170` · `Sofia Whitfield` · `2026-08-19` · `$1,650.00` · `● Open` |

This card is half off the bottom of the frame and never animates — it's set dressing that
proves the dashboard continues. Don't spend time on it.

---

## 10 · Timing sheet

Six seconds, against the VO:
*"The dashboard shows exactly where the month stands — and what needs you today."*

| Start | End | Layer | Move |
|---|---|---|---|
| 6 | 18 | `HEADLINE` | Rise + fade — **hero**, 12 frames |
| 24 | 30 | `HEADLINE` animator | "at a glance" turns indigo |
| 30 | 45 | `SHOT_DASH` | Rise + fade from Y **+36**, 15 frames |
| 54 | 72 | `TILE_1` value | Slider 0 → 48 |
| 58 | 76 | `TILE_2` value | Slider 0 → 25 |
| 62 | 80 | `TILE_3` value | Slider 0 → 17 |
| 66 | 84 | `TILE_4` value | Slider 0 → 37 |
| 66 | 84 | Mar bar pair | Scale Y 0 → 100% |
| 70 | 88 | Apr pair | |
| 74 | 92 | May pair | |
| 78 | 96 | Jun pair | |
| 82 | 100 | Jul pair | |
| 86 | 104 | Aug pair | |
| 90 | 108 | `ARC_NOTDUE` | Trim End 0 → 53% |
| 102 | 114 | `ARC_OVER30` | Trim End 53 → 85% |
| 111 | 120 | `ARC_OVER30PLUS` | Trim End 85 → 100% |
| **126** | **135** | `TILE_FLASH` | Pulse on OVERDUE · *"what needs you today"* |
| 135 | 180 | — | **Hold** |

Everything inside `SHOT_DASH` is at Opacity 0 until the panel lands at 45 — except the
cards, tile bodies, labels and axes, which arrive **with** the panel. Only the *data*
animates: values, bars, arcs.

### The three groups overlap on purpose

A dashboard populating should feel like several things resolving at once. Sequencing them
would take twelve seconds and read as a loading screen.

### TILE_FLASH

Rectangle matching Tile 3 — **195 × 98 at 758, 213** in `SHOT_DASH`, Roundness 8, **no
Fill**, Stroke `#FA5C7C` 2 px. Opacity 0 → 60 → 0 at frames 126 / 130 / 135.

It's the only element that says *this one needs you*, which is the VO's closing clause.
Without it the frame ends on a chart finishing rather than on a call to action.

---

## 11 · Motion recipes

### Rise + fade

| Layer | Position start | Position end |
|---|---|---|
| `HEADLINE` | 960, 224 | 960, 200 |
| `SHOT_DASH` | 960, **655** | 960, 619 |

+36 rather than +24 — it's a 1116 px panel.

### Values, bars, arcs

Ease the **last** keyframe only (**Shift+F9**, Incoming Influence 80%). **No overshoot
anywhere in this frame** — a bar that overshoots its value is briefly showing a number the
product never displayed.

---

## 12 · Polish

- **Toggle `REF_CAPTURE` on at frame 135.** Every built element should sit exactly on its
  reference: bars flush, values matching, arcs aligned. This is what the guide layer is
  for — use it as the final check, then leave it on (it never renders).
- **Check at 100%.** Most of this frame is 9–14 px text. If anything is illegible at full
  size it will be mush on a phone.
- **Motion blur off.**
- **Hold the last 45 frames.**

---

## 13 · Finishing and notes

1. Drop `F12_CONTENT` into **MASTER** after `F11_CONTENT`
2. Confirm both `BG_PREVIEW` **and** `REF_CAPTURE` are still Guide Layers
3. **File ▸ Increment and Save**

- **Rose stays in this frame.** `#FA5C7C` on the OVERDUE tile and the 30+ day arc is the
  *product's* colour, not the ad's retired pain colour. F12 claims to be the real
  dashboard; recolouring its UI to respect the film's palette would misrepresent it. The
  opposite of F9's PDF badge, which was an element we invented and got to choose.
- **F12 is in neither cutdown** — no trim constraint, no 9:16 version.
- **This frame does not reframe to 9:16.** A dashboard is the least croppable thing in the
  film — but note that because the content area is now built rather than captured, a
  vertical version is at least *possible*: restack the tiles two-by-two and put the charts
  underneath. That wasn't true when it was a flat screenshot.

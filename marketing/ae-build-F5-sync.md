# After Effects build guide — F5 "Sync" (0:24–0:30)

**1920×1080 · 30 fps · 6 s (180 frames)**. All coordinates are comp-space at 1920×1080
and match the approved mockup.

This is the film's first **real product capture** frame. Most of the work is getting the
screenshot right; the AE build on top of it is small.

---

## 0 · The capture — do this before opening AE

Per the handbook §9, run the seed, start the app on port 63055, sign in as
**`demo2@duemap.dev` / `DemoUser1!`**, and go to **`/invoices`** in dark mode.

- **Capture at 2× device pixel ratio** (2560 px wide), not 1280. The panel displays at
  1030 px wide, so a 1280 capture lands at ~80% scale — an awkward non-integer downsample
  that softens UI text. From 2560 you're at ~40%, which stays crisp.

### How to capture at 2× — Chrome DevTools

1. Open `http://localhost:63055/invoices`, signed in
2. **F12** → DevTools
3. **Ctrl+Shift+M** → Device Toolbar
4. Device dropdown → **Responsive**
5. Width **1280**, height **832**
6. **⋮** at the right end of the device toolbar → **Add device pixel ratio**
7. Set the new **DPR** dropdown to **2**
8. **⋮** → **Capture screenshot** → a 2560 × 1664 PNG

If the theme comes out light: Ctrl+Shift+P → `Show Rendering` → **Emulate CSS media
feature prefers-color-scheme** → `dark`.

> **Don't substitute browser zoom.** Ctrl+ to 200% renders a **640 px CSS layout** at
> 1280 px; DPR 2 renders a **1280 px CSS layout** at 2560 px. Since the app is now
> responsive down to 375 px, a 640 px viewport trips the breakpoints and collapses the
> sidebar — you'd capture the tablet layout in high resolution instead of the desktop
> layout this frame is built around. Windows display scaling has the same problem and
> affects every app on the machine.
- Include the sidebar and top bar. The mockup's panel is the **whole app window**, not
  just the table.
- **Never press Sync on this workspace.** The accounting connection is a display-only row
  with dummy tokens that can't decrypt.

> **Fix the customer names before capturing.** Every row in the mockup reads
> `… Whitfield` — Sofia, Yuki, Curtis, Rina, Tobias, Nora, Jamal, Elena. Varied first
> names, one surname, eight times in a row. That's a seed-data artifact and it reads as
> fake instantly, on the one frame whose whole job is "this is the real product." The
> workspace has 48 customers; either page to a section with varied surnames or adjust the
> seed. This is the single highest-value fix in the frame.

Save as `assets/shot-invoices.png`.

---

## 1 · Comp setup

1. **Ctrl+N** → `F5_CONTENT`, 1920 × 1080, 30 fps, duration **`0:00:06:00`** (180 frames)
2. **Ctrl+click the time display** — work in frames
3. Solid `#16191F` named `BG_PREVIEW` at the bottom → **Layer ▸ Guide Layer**
4. Make a **`Frame-5/`** folder in the Project panel

**Convention:** shapes via **Layer ▸ New ▸ Shape Layer** then **Add ▸ Rectangle/Ellipse** —
path at 0,0, coordinate in the *layer* Position.

**This frame has no eyebrow.** The script gives F5 a headline and sub-line only.

---

## 2 · Layer stack

```
F5_CONTENT
├── PILL_SYNC                    (precomp)
├── SHOT_PANEL                   (precomp)
│   ├── SHOT_FRAME               1030×670 R16, stroke only
│   ├── SHOT_MASK                1030×670 R16 — Alpha Matte for ↓
│   └── SHOT_INNER               (precomp)
│       ├── ROWS_COVER
│       └── SHOT_INVOICES        the PNG
├── SUBLINE
├── HEADLINE
└── BG_PREVIEW                   (Guide Layer)
```

`SHOT_INNER` exists because a track matte only affects the **one layer directly below it**.
The rows-reveal cover has to be clipped by the same rounded corners as the screenshot, so
the two get precomposed and matted together.

---

## 3 · Text block

Both **left aligned**, **Anchor Point 0, 0**, no Ctrl+Alt+Home.

| Layer | Text | Font | Size | Leading | Tracking | Colour | Position |
|---|---|---|---|---|---|---|---|
| `HEADLINE` | `Tenants & ⏎ invoices sync ⏎ themselves.` | Inter Tight ExtraBold | 96 | **100** | −20 | `#F0F2F7` | 160, 436 |
| `SUBLINE` | `Every day. Automatically.` | Inter Medium | 40 | — | 0 | `#AEB6C4` | 160, 727 |

**Three lines, one text layer, two manual breaks.** Leading 100 puts the baselines at
436 / 536 / 636.

> **No indigo word in this headline.** F5 is the only frame so far without one, and that's
> correct — the accent here is the cyan sync pill, and adding an indigo word would put two
> competing highlights on screen. Leave all three lines `#F0F2F7`.

> **Sub-line colour.** The mockup renders it with a faint lavender cast rather than the
> flat `#AEB6C4` in the brand kit. `#AEB6C4` is the documented token and what F7 uses — go
> with it. If you want the comp matched exactly, `#9BA3E8` is the closest read.

---

## 4 · The screenshot panel

### SHOT_INNER (build this first)

New comp, **1030 × 670**, 30 fps, 6 s, named `SHOT_INNER`.

1. Import `shot-invoices.png`, drop it in, **Ctrl+Alt+F** (Fit to Comp) — or scale
   manually to ~40% if you captured at 2×. Name it `SHOT_INVOICES`.
2. Shape Layer → Rectangle **830 × 420**, Fill **sampled with the eyedropper from an empty
   area of the table**, not typed. Name it `ROWS_COVER`.
   - **Anchor Point 0, 210** — the rectangle's *bottom* edge in layer space
   - **Position 625, 670** — sitting flush on the comp's bottom edge

   That covers x 210 → 1040 and y 250 → 670. Overhanging the comp's right edge is fine;
   the comp clips it.

   The offset anchor is the whole trick: animating Scale Y collapses the cover downward
   onto its own bottom edge, revealing rows top-to-bottom. Anchored at centre it would
   shrink from both directions at once and look like a closing shutter.

   > **These are `SHOT_INNER` coordinates (1030 × 670), not frame coordinates.** Verify at
   > 100% zoom rather than trusting the numbers: the left edge must sit at or left of the
   > content panel's edge (or the INVOICE # column peeks out), the top edge just below the
   > table header (or row 1 is only half hidden), and the bottom flush on y 670.
   >
   > If you change the height, **Anchor Point Y must stay at half of it** and Position Y at
   > 670 — otherwise the cover stops resting on the comp floor and the reveal drifts.

### SHOT_PANEL

New comp, **1920 × 1080**, named `SHOT_PANEL`.

| Layer | Spec | Anchor Point | Position |
|---|---|---|---|
| `SHOT_FRAME` | Shape layer — Rectangle 1030 × 670, R16, **no Fill**, Stroke `#333A49` **1.5 px** | 0, 0 | 1284, 562 |
| `SHOT_MASK` | Shape layer — Rectangle 1030 × 670, R16, any Fill | 0, 0 | 1284, 562 |
| `SHOT_INNER` | Drag the comp in from the Project panel | **515, 335** | 1284, 562 |

`SHOT_INNER`'s anchor of 515, 335 is the centre of its own 1030 × 670 comp — **AE sets it
automatically, leave it alone.** With that anchor, Position 1284, 562 lands its centre in
the same place as the two shape layers, whose rectangles are centred by Position from an
anchor of 0, 0.

> **No Ctrl+Alt+Home on this one** — the exception to handbook §4.4. That warning is about
> precomps made with **Ctrl+Shift+C**, which inherit the parent's 1920 × 1080 dimensions;
> their bounds are the whole frame, so centring the anchor lands on 960, 540 and achieves
> nothing. `SHOT_INNER` was created as a *new comp* at 1030 × 670, so its bounds are
> exactly the artwork. A precomp's anchor is only wrong when the comp is bigger than the
> thing inside it.

Set `SHOT_INNER`'s **Track Matte → Alpha Matte "SHOT_MASK"**. AE auto-hides the matte
layer; leave it hidden.

### Placing it in F5_CONTENT

| | Anchor Point | Position |
|---|---|---|
| `SHOT_PANEL` | 1284, 562 | 1284, 562 |

Add **Layer ▸ Layer Styles ▸ Drop Shadow** → Opacity 50%, Distance 12, Size 40, Angle 120°.

**The panel's right edge lands at 1797, past the usual 1760 margin.** That's deliberate and
matches the mockup — the capture reads as "the app is big" rather than as a tidy inset
card. Don't pull it back to 1760.

---

## 5 · Sync pill

Precompose as `PILL_SYNC`.

| Layer | Spec | Position |
|---|---|---|
| `PILL_BODY` | Rectangle **366 × 54**, **Roundness 27** (full pill), Fill `#232734`, Stroke `#39AFD1` 1.5 px | 992, 233 |
| `PILL_DOT` | Ellipse **12 × 12**, Fill `#39AFD1` | 842, 233 |
| `PILL_TEXT` | `Last sync: 2 minutes ago` — Inter SemiBold 24, `#39AFD1`, **left aligned**, Anchor 0,0 | 864, 243 |

| | Anchor Point | Position |
|---|---|---|
| `PILL_SYNC` | 992, 233 | 992, 233 |

It sits **above** `SHOT_PANEL` and deliberately overlaps the panel's top-left corner —
that overlap is what makes it read as an annotation on the product rather than a UI
element inside it. Cyan is the sync colour and this is its only appearance in the frame.

---

## 6 · Resting states

| Layer | Value |
|---|---|
| `HEADLINE` `SUBLINE` | Opacity 0 |
| `SHOT_PANEL` | Opacity 0 |
| `PILL_SYNC` | Opacity 0 |
| `ROWS_COVER` | **Scale 100, 100** (covering) |

---

## 7 · Timing sheet

Six seconds, against the VO:
*"Tenants, leases, and invoices sync automatically — every single day."*

**Start** is the frame the first keyframe goes on; **End** is the last.

| Start | End | Layer | Move |
|---|---|---|---|
| 6 | 18 | `HEADLINE` | Rise + fade — **hero**, 12 frames · *"tenants, leases, and invoices"* |
| 24 | 30 | `SUBLINE` | Rise + fade |
| 36 | 48 | `SHOT_PANEL` | Slide in — Position X **+60** → final, fade |
| **60** | **108** | `ROWS_COVER` | **Scale Y 100 → 0%** · *"sync automatically"* |
| 114 | 123 | `PILL_SYNC` | Pop · *"every single day"* |
| 123 | 180 | — | **Hold.** No new movement |

Only nine words of VO across six seconds — the sparsest frame in the film. The 48-frame
row reveal is deliberately slow to fill it; don't speed it up to match the pace of F6.

### The row reveal

`ROWS_COVER` Scale is linked by default — **click the chain icon to unlink it**, then key
Y only. X stays at 100%.

| Frame | Scale |
|---|---|
| 60 | 100, **100** |
| 108 | 100, **0** |

Ease the **last** keyframe (**Shift+F9**) so the rows settle rather than stopping dead.

If rows appear from the bottom up, your anchor is at the rectangle's top instead of its
bottom — set Anchor Point Y to **+200**, not −200.

---

## 8 · Motion recipes

### Rise + fade

| Layer | Position start | Position end |
|---|---|---|
| `HEADLINE` | 160, 460 | 160, 436 |
| `SUBLINE` | 160, 751 | 160, 727 |

### Slide in — SHOT_PANEL

Position **1344, 562 → 1284, 562**, Opacity 0 → 100%, 12 frames. The handbook's slide-in
is +80; this uses **+60** because the panel is 1030 px wide and a larger object needs less
travel to read as moving.

### Pop — PILL_SYNC

| Property | Offset | Value |
|---|---|---|
| Scale | 0 | 88% |
| Scale | +5 | **106%** |
| Scale | +9 | 100% |
| Opacity | 0 → +4 | 0 → 100% |

### Easing

**Shift+F9** on the **last** keyframe of every entrance, Incoming Influence **80%**. First
keyframe linear. **F9** the middle keyframe of the pop.

---

## 9 · Polish

- **Mockup parallax.** The brand rules call for 2–3% drift on product mockups. Use a slow
  *linear* Position drift on `SHOT_PANEL` — 1284, 562 at frame 0 → 1278, 558 at frame 180.
  **Not `wiggle()`** — a screenshot that jitters looks like a compression fault, and the
  handbook reserves wiggle for clutter.
- **Dot pulse.** Loop `PILL_DOT` Opacity 100 → 55 → 100 over ~24 frames from 123. Sells
  "live connection" during the hold.
- **Check the capture at 100%.** Zoom the Composition panel to 100% and read the invoice
  numbers. If they're soft, the capture was 1× — recapture at 2×. This is the one frame
  where soft UI text is fatal.
- **Motion blur off.** Nothing travels far enough, and it would soften the screenshot.

---

## 10 · Finishing

1. Drop `F5_CONTENT` into **MASTER**, between `F4_CONTENT` and `F6_CONTENT`
2. Confirm `BG_PREVIEW` is still a Guide Layer
3. **File ▸ Increment and Save**

---

## 11 · Notes for the final pass

- **Cyan's only appearance outside F4.** `#39AFD1` is the sync colour; it's used on the
  pill here and on F4's connector wires, nowhere else. Keep it that way.
- **F5 is in neither cutdown.** The 30 s runs F1·F3·F4·F6·F8·F9·F14 and the 15 s runs
  F3·F6·F8·F14 — so this frame only plays in the 90 s master.
- **Date consistency.** The capture shows invoices due 2026-08-09 → 08-19. If you took F6's
  optional fix, that frame is anchored to today = Aug 5, which is consistent (these are
  upcoming invoices). If you kept F6's mockup dates, nothing here contradicts them either.
  Worth a glance when both frames are cut together.
- **This frame does not reframe to 9:16.** Product-screenshot frames need content
  substitution, not cropping (handbook §8) — the sidebar and table would have to be
  recaptured at a vertical layout. Since F5 isn't in either cutdown, that work never comes
  up unless the frame gets promoted.

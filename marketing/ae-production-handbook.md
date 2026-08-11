# DueMap promo film — After Effects production handbook

Everything needed to pick this up cold. Read §1–§4 before touching the project;
they explain decisions that aren't obvious from the timeline and that cost real
time to work out.

**Software:** After Effects 2026 (Windows)
**Project:** `D:\Projects\DueMap\marketing\Main Project N.aep` — open the
highest-numbered file. We use **File ▸ Increment and Save** at milestones, so
older numbers are working history, not junk.

---

## 1. Where things stand

| Frame | Comp | Static | Animated | Notes |
|---|---|---|---|---|
| F1 — The chase | `F1_CONTENT` | done | done | Rendered once; render settings were wrong first time (see §8) |
| F2 — Three questions | `F2_CONTENT` | done | done | Placed in MASTER at frame 168 |
| F3 — Brand reveal | `F3_CONTENT` | done | done | Uses `assets/duemap-spark.svg` |
| F4 — Plugs into your books | `F4_CONTENT` | done | **spec'd — verify** | Animation spec in §6; confirm it was applied |
| F5 – F14 | not started | — | — | Mockups exist in the script; see §7 |

**Deferred work lives in `ae-final-pass-checklist.md`.** Don't re-invent it —
transitions, the colour arc, motion blur, audio and export are all logged there
with reasoning.

---

## 2. Project structure

```
Project panel
├── assets/
│   └── duemap-spark.svg        the logo glyph, extracted from the app's MainLayout.razor
├── Frame-1/  F1_CONTENT, CHIP_AUG1, CHIP_AUG5, CHIP_AUG_12
├── Frame-2/  F2_CONTENT, CARD_1, CARD_2, CARD_3
├── Frame-3/  F3_CONTENT
├── Frame-4/  F4_CONTENT, GROUP_QBO, GROUP_XERO, GROUP_NODE, GROUP_PILL
├── Solids/
└── MASTER
```

Keep the folder-per-frame discipline. By F10 the panel is unusable without it.

---

## 3. The architecture — atmosphere vs content

This is the single most important structural decision in the project.

```
MASTER  (1920×1080, 30 fps, 0:01:40:00)
├── GRAIN            ─┐
├── VIGNETTE          │  full length, never cut
├── F4_CONTENT        │
├── F3_CONTENT        │  frame comps, laid end to end
├── F2_CONTENT        │
├── F1_CONTENT        │
├── GLOW_ROSE_BL      │
├── GLOW_ROSE_R       │  full length
└── BG_INK           ─┘
```

**Background, glow, vignette and grain live only in MASTER.** Frame comps
contain content on transparency and nothing else.

*Why:* if each frame carried its own background, overlapping two of them for a
transition would composite two ink solids, two sets of glows and two
independent grain layers. The one element that must never change becomes the
one most likely to flicker at every join. Keeping atmosphere continuous also
makes the film's colour arc (rose → indigo → green) a handful of keyframes in
one place instead of a per-frame fudge.

### Frames are self-contained modules

Each frame comp does **entrance + hold**. No exits, no animation reaching
across a boundary. Transitions get designed as dedicated screens in the final
pass, once the whole set exists and the rhythm is visible.

*(F1→F2 has a leftover crossover built before this decision — four keyframes on
`F1_CONTENT` at frames 168/178. Remove them when transitions are designed.
Logged in the checklist.)*

### Reviewing a frame in isolation

Each content comp has a `BG_PREVIEW` ink solid at the bottom of its stack,
marked **Layer ▸ Guide Layer**. Guide layers render in their own comp's viewer
but are excluded from output **and** from the comp when nested in MASTER. So
you get a proper dark background while working, and it disappears
automatically. Every new frame comp needs one.

---

## 4. House rules

These are the things that went wrong at least once. Following them prevents
about 90% of the debugging in this project.

### 4.1 Shape-layer coordinates

```
comp position = parent anchor + layer Position + path Position
```

Three places a coordinate can hide, and they add up. Pick **one** convention
per layer and stick to it:

- **Preferred:** build with **Layer ▸ New ▸ Shape Layer** then **Add ▸
  Rectangle/Ellipse**. The path is created at 0,0, so you set only the *layer*
  Position. Anchor is automatically dead centre.
- **Alternative:** put absolute comp coordinates in the *path* Position, and
  then the layer must be unparented with Position **0, 0**.

Mixing the two is what produces "my element is 400px off and I can't see why."

### 4.2 Never draw shapes with the Pen tool unless you want beziers

Drawing with **G** (Pen) produces a `Path` with editable vertices and **no Size
property**. `Add ▸ Rectangle` produces an `Ellipse Path`/`Rectangle Path` with
numeric **Size**, **Position** and **Roundness**. If your timeline shows
`Path 1 ▸ Path`, you have a bezier; if it shows `Rectangle Path 1 ▸ Size`, you
have what you want.

Conversion is one-way — there's no "convert back to parametric".

### 4.3 Rectangle Position is the CENTRE

For a row of bars sharing a left edge:

```
centre X = left edge + (width ÷ 2)
```

Bars of different widths need different centre values. Getting this wrong by a
few pixels each makes the left edge look ragged at full size even though it
reads fine in a thumbnail.

### 4.4 Ctrl+Alt+Home does not work on precomps

**Layer ▸ Transform ▸ Center Anchor Point in Layer Content** reads *layer
bounds*. For shape and text layers that's the artwork, so it works. For a
**precomp or solid** the bounds are the whole 1920×1080 rectangle, so it
centres on 960,540 — which is already the default and achieves nothing.

**For a precomp, set Anchor Point manually to wherever the artwork sits inside
it**, then set Position to the same value:

| Layer | Anchor Point | Position |
|---|---|---|
| CARD_1 | 395, 535 | 395, 535 |

Symptom of getting this wrong: the element renders roughly 700px up and left of
where you expect, and scale animations swing it across the frame instead of
growing it in place.

### 4.5 Opacity does not inherit through parenting

Position, Scale and Rotation pass from parent to child. **Opacity and blending
modes do not.**

So a group built as parent + children needs opacity keyframed on *every* layer —
or precompose the group first and keyframe once. Precomposing is almost always
the right answer once a group exceeds two layers.

### 4.6 Order of operations when parenting

- **Position the child *before* parenting it.** AE preserves visual position
  when you assign a parent, recalculating the child's Position into the
  parent's space. Type the coordinate first, pickwhip second.
- **Parent *before* rotating the parent.** Assign the parent while rotation is
  still 0, then rotate. Do it the other way and AE compensates the child's
  rotation to keep it visually level — leaving straight text on a tilted note.
- **Never parent a layer that already has Position keyframes.** Those keyframes
  get reinterpreted in the parent's coordinate space and the animation
  scatters. (This is why idle drift uses a `wiggle()` expression rather than a
  parented null.)

### 4.7 Text

- **Left-aligned** text anchors at the **baseline's left edge** by default.
  That's usually the reference point you want — **do not** run Ctrl+Alt+Home on
  it, and set Position directly.
- **Centre-aligned** text anchors at the baseline's centre. Run Ctrl+Alt+Home,
  then position.
- Getting this backwards clips long headlines off the left of the frame.
- Multi-line headlines: **one text layer with a manual line break**, and set
  **Leading** to control the gap. Splitting into two layers loses kerning
  across the seam and turns every copy tweak into a re-alignment job.

### 4.8 Colouring part of a text layer

Use a **Text Animator**, not separate layers:

1. Twirl open the layer → **Text** → **Animate: ▶** → **Fill Color ▸ RGB**
2. `Range Selector 1 ▸ Advanced` → **Units → Index** *(do this first, or the
   next two numbers are read as percentages)*
3. Set **Start** / **End** to the character range
4. Set the animator's **Fill Color**
5. Keyframe **Amount** 0 → 100% to animate the recolour

**Prefer `Based On → Words`** where possible — word indices survive
punctuation, spacing and re-breaking the line, character indices don't.

### 4.9 Motion vocabulary

Brand rule: **200 ms ease-out = 6 frames at 30 fps.** Hero moves get 400 ms.

| Move | Recipe |
|---|---|
| Rise + fade | Position Y **+24** → final, Opacity 0 → 100, 6 frames |
| Pop | Scale **88 → 106 → 100** over 0 / +5 / +9 frames; Opacity 0→100 over first 4 |
| Line draw | `Add ▸ Trim Paths`, keyframe **End** 0% → 100% |
| Slide in | Position X **+80** → final, 8 frames |

**Easing direction matters:**

- **Entrance** — ease the **last** keyframe: **Shift+F9** (Easy Ease In).
  Fast start, soft landing.
- **Exit** — ease the **first** keyframe: **Ctrl+Shift+F9** (Easy Ease Out).
  Slow start, accelerating away.
- Pressing F9 on both ends gives ease-in-out, which reads as sluggish at 200 ms.

For Trim Paths draws: if a line draws from the wrong end, don't redraw it —
leave End at 100% and animate **Start** from 100% → 0% instead.

### 4.10 Working in frames, not timecode

**Ctrl+click the time display** to toggle. Every spec in this project is in
frames, so work in frames. In frames mode, typing a timecode string into a
duration field silently collapses it to 1 frame.

`6 s × 30 fps = 180 frames.`

---

## 5. Brand kit

| Token | Hex | Use |
|---|---|---|
| Ink | `#16191F` | Background |
| Panel | `#232734` | Cards, chips, boxes |
| Hairline | `#333A49` | Borders, dividers |
| Skeleton | `#3A4152` | Placeholder bars, grid cells |
| Rose | `#FA5C7C` | F1–F2 only, then retired |
| Indigo | `#727CF5` | Brand — logo, CTAs, one word per headline |
| Amber | `#FFBC00` | Attention, sticky note, F2 card 3 |
| Cyan | `#39AFD1` | Sync, connector wires |
| Green | `#0ACF97` | Resolution — connected states, final third |
| Heading | `#F0F2F7` | Headlines |
| Body | `#AEB6C4` | Sub-lines, cell labels |
| Muted | `#7E899B` | Eyebrows, struck-out text |

**Type:** Inter Tight ExtraBold (800) for headlines at 96 px / Leading 100 /
Tracking −20. Inter SemiBold (600) 28 px Tracking +80 for eyebrows. Inter
Medium (500) for UI-ish text. Install both families from Google Fonts.

**Layout:** left margin **160 px**. Safe margins 8% all sides.

**Project settings:** **16 bits per channel** (File ▸ Project Settings ▸ Color).
Not optional — large soft glows on near-black band visibly at 8 bpc and it
looks like a compression fault. The 2.5% grain layer also exists to dither
those gradients.

---

## 6. Built frames — reference coordinates

Everything below is comp-space at 1920×1080. Useful for verifying, rebuilding,
or adapting to the 9:16 versions later.

### F1 — The chase (180 frames)

| Element | Spec | Position |
|---|---|---|
| CHIP_AUG1 / AUG5 | 172×66 R10, fill `#232734`, stroke `#333A49` 1px, text 30px `#7E899B` | 249,190 / 443,190 |
| CHIP_AUG12 | 268×104 R12, stroke `#FA5C7C` 2px, text 52px `#F0F2F7`, Outer Glow | 686,190 |
| STICKY | 220×86 R2, fill `#FFBC00`, rotation −4°, drop shadow | 1533,174 |
| ROW (precomp) | 500×62 R6, rotation −2°, 3 cells + 2 dividers | 1506,372 |
| BUBBLE_1 / 2 | 330×62 R31, fill `#727CF5`; #2 at 78% / text 60% | 1426,515 / 1424,600 |
| EYEBROW | `EVERY MONTH` | 160,692 |
| HEADLINE_L1 | `Rent day comes. Then the` | 160,820 |
| HEADLINE_L2 | `chasing starts.` — "chasing" rose via animator | 160,920 |

**Timing:** chips 6/27/48 · eyebrow 57 · headline 63/72 · "chasing" recolour 84
· bubbles 96/105 · row 120 · sticky 138 · OVERDUE flash 147 · glow build 150→180.

### F2 — Three questions (180 frames)

Cards **520 × 596**, Roundness 14, fill `#232734`.

| Card | Centre | Stroke | Text (Inter SemiBold 48, Leading 61) |
|---|---|---|---|
| CARD_1 | 395, 535 | `#333A49` 1px | `Who's paid?` @ 184,768 |
| CARD_2 | 960, 535 | `#333A49` 1px | `Who's still in` ⏎ `grace?` @ 749,707 |
| CARD_3 | 1525, 535 | **`#FFBC00` 2px** | `What can I legally` ⏎ `charge?` @ 1314,707 |

Card 1 skeleton bars — height 13, R6, left edge 184: 381@302 · 297@328 ·
**337@354 rose** · 276@380.

Card 3 skeleton bars — 5 bars, left edge 1314: 405@302 · 386@328 · 330@354 ·
322@380 · 360@406.

Card 2 grid — cell 55×38 R6 at 778,314; **Repeater 1** ×7 offset (61,0),
**Repeater 2** ×2 offset (0,52). Highlight cell at 961,314 with `#727CF5` 2px
stroke.

**Timing:** cards enter at 6 / 15 / 24, rise+fade from Y 559.

### F3 — Brand reveal (180 frames)

| Element | Spec | Position |
|---|---|---|
| SPARK | `duemap-spark.svg`, Scale ~12% (≈96px) | 718, 494 |
| WORDMARK | `DueMap`, Inter Tight ExtraBold 124px | 790, 539 |
| TAGLINE | `Rent follow-ups, on autopilot.` 40px; indices 17–29 indigo | 960, 659 |

**Timing:** spark 6→20 (Scale 0→14→12, Rotation −90→0) · wordmark type-on
20→34 via Animator Opacity 0 + Range Selector **Start** 0→100% · tagline 40→46.

### F4 — Plugs into your books (210 frames)

| Element | Spec | Position |
|---|---|---|
| EYEBROW | `WORKS WITH YOUR BOOKS` | 160, 155 |
| HEADLINE | `Connects to QuickBooks Online &` ⏎ `Xero in one click.` | 160, 270 |
| BOX_QBO / XERO | 422×106 R14, `#232734` + `#333A49` | 672,669 / 672,806 |
| Badges | circle 48, `#2B3040`, text `qb` / `x` | 523,669 / 523,806 |
| Labels | Inter SemiBold 32 `#F0F2F7` | 570,682 / 570,819 |
| NODE_BOX | 314×105 R14, stroke `#727CF5` 1.5px + Outer Glow | 1301, 738 |
| NODE_SPARK / TEXT | scale 4.5% / `DueMap` 34px | 1216,738 / 1256,752 |
| WIRE_QBO / XERO | Pen path, stroke `#39AFD1` 4px round cap | (883,669)→(1144,738) · (883,806)→(1144,738) |
| PILL | 430×53 R26 + dot 14 `#0ACF97` + text 26px | 959,937 · dot 786,937 · text 812,947 |

**Wire handles must be horizontal at both ends** — that's what makes them read
as circuit traces rather than hand-drawn arrows.

**Timing:** eyebrow 6 · headline 12 · QBO 36 · Xero 66 · wires 84 / 88 ·
node pop 100 · pill 138. Group into four precomps first (§4.5).

---

## 7. Building the remaining frames

Frames 5–14 are specified in **`duemap-promo-script.md`** — copy, VO, visual
direction and timing for each.

**Workflow per frame:**

1. **Composition ▸ New Composition** → `F{n}_CONTENT`, 1920×1080, 30 fps,
   duration per the script
2. Add the `BG_PREVIEW` ink solid, mark it **Guide Layer**
3. Build statics — menu-created shapes, type the Positions
4. Precompose any group over two layers
5. Animate entrances only; **Shift+F9** on end keyframes
6. Drop into MASTER, laid after the previous frame
7. **File ▸ Increment and Save**

**Frames 5, 6, 11 and 12 need real product screenshots.** A populated demo
workspace exists for exactly this — see §9.

**Consider building the 30-second cutdown's frames first** — F1, F3, F4, F6,
F8, F9, F14. That's a complete, shippable performance ad at half the work, and
it's the version that actually runs as paid media.

---

## 8. Export

**Render Settings**

- **Time Span → Length of Comp.** Not "Work Area". A stray work-area bar
  produced a render starting one frame *after* the comp ended — six seconds of
  nothing. Setting this once makes every future render immune.
- Quality Best, Resolution Full, Motion Blur "On for Checked Layers"

**Output Module**

- **QuickTime ▸ Apple ProRes 422 HQ.** AE on Windows exports ProRes natively.
- **Do not render H.264 as the master.** It's 8-bit 4:2:0 — the exact thing
  that bands this film's dark gradients — and every platform version would then
  be a re-encode of an already-lossy file.
- Deliver H.264 from the ProRes via Media Encoder, 12–16 Mbps for 1080p.

**Naming:** `DueMap_F1_TheChase_1920x1080_ProResHQ_v01.mov`
**Location:** `D:\Projects\DueMap\marketing\renders\`

**Aspect ratios:** 16:9 is the master and the only thing the QuickBooks and
Xero app stores can use — both take a hosted video link (YouTube/Vimeo), so the
embed is always landscape. Add 9:16 only for the 30s and 15s cutdowns, for
Reels/TikTok/Shorts. Note that product-screenshot frames don't reframe — they
need content substitution, not cropping.

---

## 9. Related files

| File | What it is |
|---|---|
| `duemap-promo-script.md` | The 14-frame script, brand kit, cutdowns, deck adaptation, shot list, claim guardrails |
| `ae-build-F1-the-chase.md` | The original detailed F1 walkthrough — most useful as a worked example |
| `ae-final-pass-checklist.md` | Everything deliberately deferred: transitions, colour arc, motion blur, audio, asset blockers |
| `duemap-spark.svg` | The logo glyph, pulled from the app's `MainLayout.razor` so ad and product can't drift |
| `seed-demo-workspace.sql` | Creates the screenshot workspace (§below) |
| `unseed-demo-workspace.sql` | Removes it |

### The demo workspace

Run the seed, start the app (`dotnet run` on `DueMap.Web`, port 63055), sign in
as **`demo2@duemap.dev` / `DemoUser1!`**.

Workspace "Northgate Residential": 48 customers, 44 leases across all six
statuses, 234 invoices over 7 months, 68 notice deliveries, 14 processing runs.

Best screens: `/` (charts), `/today` (6 work items, 4 types), `/leases` (all
status chips populated), **`/leases/2051`** (11-event escalation timeline — the
strongest single shot in the set), `/invoices` (234 rows + pager).

**Never press Sync on this workspace** — the accounting connection is a
display-only row with dummy tokens that can't decrypt.

---

## 10. Outstanding blockers

- **QuickBooks logo.** The repo has Intuit *UI buttons* (`Connect to
  QuickBooks`) and the *Intuit corporate* lockup — neither is a marketing mark.
  F4 currently uses a placeholder `qb` lettermark. Source the product logo and
  check Intuit's partner brand guidelines **for advertising specifically**;
  permission to show their button inside your product doesn't extend to using
  their logo in a paid ad. Same question applies to Xero's mark
  (`img/xero/Xero logo 1x1.svg` is available and is a proper standalone mark).
- **Public domain** — confirm before rendering F14's end card. Dev config says
  `duemap.dev`.
- **Proof points** — the script has marked slots (e.g. F13 setup time). Fill
  with measured data or cut the line. No invented numbers.
- **State claims** — name only the 7 launch states (AZ CA FL GA NC NY TX). The
  database lists all 50 but only those 7 have rule versions.

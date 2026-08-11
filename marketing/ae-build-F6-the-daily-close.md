# After Effects build guide — F6 "The Daily Close" (0:30–0:38)

The hero frame, and the longest one in the film: **1920×1080 · 30 fps · 8 s (240 frames)**.
All coordinates are comp-space at 1920×1080 and match the approved mockup.

Read §4 of `ae-production-handbook.md` first if you haven't — the shape-coordinate and
anchor-point rules below assume it.

---

## 0 · Before you open AE

Fonts and project settings are the same as every other frame — **Inter** and **Inter
Tight** installed as static weights, **16 bits per channel**, sRGB working space. If the
project is already open from F4, none of this needs touching.

**Colour reference for this frame:**

| Token | Hex | Used for |
|---|---|---|
| Panel | `#232734` | Clock chip, list panel |
| Chip inner | `#2B3040` | "your local time" pill |
| Hairline | `#333A49` | Row dividers |
| Indigo | `#727CF5` | "Handled.", sweep line, Grace badge |
| Green | `#0ACF97` | Reminder-sent badge |
| Amber | `#FFBC00` | Due-today and late-fee badges |
| Heading | `#F0F2F7` | Headline, clock, tenant names, money |
| Body | `#AEB6C4` | "your local time" |
| Muted | `#7E899B` | Eyebrow, due-status column |

---

## 1 · Comp setup

1. **Composition ▸ New Composition** (Ctrl+N):
   - Name `F6_CONTENT`
   - 1920 × 1080, Square Pixels
   - Frame Rate **30**
   - Duration **`0:00:08:00`** (240 frames)
2. **Ctrl+click the time display** so you're working in frames, not timecode. Every
   number in §10 is a frame number.
3. **Layer ▸ New ▸ Solid** (Ctrl+Y) → 1920×1080, colour `#16191F`, name `BG_PREVIEW`.
   Send it to the bottom, then **Layer ▸ Guide Layer**. It gives you a dark background
   while building and vanishes when nested in MASTER.
4. Make a **`Frame-6/`** folder in the Project panel. This frame produces eleven comps.

**Convention for every shape below:** build with **Layer ▸ New ▸ Shape Layer** then
**Add ▸ Rectangle**, so the path stays at 0,0 and you set only the *layer* Position.

**One exception — `LIST_DIVIDERS` (§5).** It holds three rectangles at three different Y
values in a single layer, so they can't all sit at path 0,0. That layer uses the handbook
§4.1 *alternative* convention instead: absolute coordinates in the paths, and the layer
transform neutralised. §5 gives the exact values. Every other layer in this frame uses the
preferred convention above — never mix the two on the same layer, that's what produces
"my element is 400 px off and I can't see why."

**No background, glow, vignette or grain in here.** Those live in MASTER only
(handbook §3). `F6_CONTENT` is content on transparency.

---

## 2 · Layer stack

Final order, top to bottom:

```
F6_CONTENT
├── SWEEP_LINE
├── BADGE_4  BADGE_3  BADGE_2  BADGE_1     (precomps)
├── ROW_4    ROW_3    ROW_2    ROW_1       (precomps)
├── FLASH_4  FLASH_3  FLASH_2  FLASH_1     (optional — §9)
├── LIST_DIVIDERS
├── LIST_PANEL
├── CLOCK_CHIP                             (precomp)
├── HEADLINE
├── EYEBROW
└── BG_PREVIEW                             (Guide Layer)
```

The sweep sits on top — it passes *over* the rows, not under them.

**Margins are symmetric in this frame.** Left edge 160, right edge 1760; the clock chip
and the lease panel both end at 1760. That alignment is the frame's spine — if something
looks off later, check it before anything else.

---

## 3 · Eyebrow and headline

### EYEBROW

- Type tool (Ctrl+T) → `THE DAILY CLOSE`
- **Inter SemiBold (600)**, 28 px, **Tracking +80**, colour `#7E899B`
- **Left aligned** — so the anchor is already at the baseline's left edge.
  **Do not** run Ctrl+Alt+Home on it.
- Position **160, 145**

### HEADLINE

- One text layer, with a manual line break:

```
Every lease. Every due date.
Handled.
```

- **Inter Tight ExtraBold (800)**, **96 px**, **Leading 100**, **Tracking −20**
- Colour `#F0F2F7`, **left aligned**, Position **160, 258**

> One layer, not two. Splitting it loses kerning across the seam and turns every copy
> tweak into a re-alignment job. Leading 100 puts `Handled.` exactly where the mockup has
> it.
>
> The mockup renders the headline slightly smaller than 96 px. 96 is the brand spec and
> what F4 uses, and `Every lease. Every due date.` still ends around x 1500 — well clear
> of the 1760 margin. Drop to 88 only if you want the comp matched pixel-for-pixel.

### Recolouring "Handled."

1. Twirl open the layer → **Text** → **Animate ▸** → **Fill Color ▸ RGB**
2. `Range Selector 1 ▸ Advanced` → **Based On → Words**, *then* **Units → Index**
   (that order — set Units after Based On, or the next two numbers are read as
   percentages)
3. **Start 5**, **End 6**. Words index from zero:
   `Every`(0) `lease.`(1) `Every`(2) `due`(3) `date.`(4) `Handled.`(5)
4. Set the animator's **Fill Color** to `#727CF5`
5. Leave **Amount** at 0% for now — it gets keyframed in §10

Word-based indices survive the line break and any copy edit. Character indices don't.

---

## 4 · Clock chip

Two rounded rects and four text layers, precomposed. The clock ticks
`5:58 → 5:59 → 6:00` in §10, and it's built as **three stacked text layers cross-cut on
opacity** rather than one layer with Source Text keyframes. Reasoning after the steps.

### Build

1. Shape Layer → **Add ▸ Rectangle** → Size **448 × 88**, **Roundness 14**.
   **Add ▸ Fill** → `#232734`. Name it `CHIP_BODY`. Layer Position **1536, 411**.
2. Shape Layer → Rectangle **182 × 40**, **Roundness 10**, Fill `#2B3040`.
   Name `CHIP_PILL`. Position **1634, 411**.
3. Text `6:00 PM` — **Inter Tight ExtraBold (800)**, 40 px, `#F0F2F7`, **left aligned**,
   Position **1338, 425**. Name it **`TIME_600`**.
   Left-aligned text anchors at the baseline's left edge — **do not** run Ctrl+Alt+Home.
4. Select `TIME_600` → **Ctrl+D** twice. Rename the copies **`TIME_559`** and
   **`TIME_558`**, and change their text to `5:59 PM` and `5:58 PM`.
   **Duplicating is the point.** The copies inherit Position 1338, 425 exactly, so all
   three strings share one left edge with nothing re-typed and no stray pixel.
5. Text `your local time` — **Inter Medium (500)**, 20 px, `#AEB6C4`, **centre aligned**
   → **Ctrl+Alt+Home**, then Position **1634, 418**. Name `CLOCK_LABEL`.
6. Stack order, top to bottom:
   `TIME_600` · `TIME_559` · `TIME_558` · `CLOCK_LABEL` · `CHIP_PILL` · `CHIP_BODY`
7. Set `TIME_559` and `TIME_600` to **Opacity 0**. `TIME_558` stays at 100 — it's what
   shows when the chip pops in at frame 34.
8. Select all six → **Ctrl+Shift+C** → precompose as `CLOCK_CHIP`.

### The tabular-figures correction

An earlier draft of this guide said to enable **Tabular Figures** in the Character panel,
via "the `0 0` icon." **That control does not exist in After Effects.** AE has no
OpenType feature panel and no `tnum` toggle — the `0 0` icon is InDesign / Illustrator /
Photoshop. AE's Character panel gives you Faux Bold, Faux Italic, All Caps, Small Caps,
Super/Subscript, and a Ligatures toggle in the flyout menu. That is the whole set.

*(The same wrong instruction sits in `ae-build-F1-the-chase.md` for F1's money cell.
Worth correcting there too.)*

So digit width can't be fixed with a font setting here. Three real options, cheapest
first:

1. **Left-align and leave clearance.** Already done above — all three times anchor at
   x 1338, `6:00 PM` ends around x 1492, and `CHIP_PILL` starts at 1543. The left edge is
   pinned by the anchor and any width difference disappears into ~50 px of empty space.
   `CHIP_BODY` is a fixed-size rectangle and never resizes regardless of the text.
2. **Stacked layers** — what's built above. Each string is positioned independently, so
   reflow is impossible by construction instead of by luck.
3. **Swap to a font with fixed-width digits.** Unnecessary here and it would break the
   type system.

Option 1 alone would almost certainly survive. The actual argument for stacked layers is
§10: **6:00 PM is the frame's causal trigger** — it's what starts the sweep — and putting
it on its own layer lets you give that one string a scale or glow flourish as it lands
without disturbing the two states before it. One layer with Source Text keyframes can't
do that.

**To keep it to one layer instead:** delete `TIME_558` and `TIME_559`, rename `TIME_600`
to `CLOCK_TIME`, set its text to `5:58 PM`, and keyframe **Source Text** at frames
34 / 48 / 64. Source Text keyframes are *automatically* Hold keyframes — AE can't
interpolate between strings, so there's nothing to toggle and no interpolation to fix.

### Precomp anchor and glow

**Ctrl+Alt+Home does nothing useful on a precomp** — it reads the full 1920×1080 bounds
and lands on 960,540 (handbook §4.4). Type both values:

| Layer | Anchor Point | Position |
|---|---|---|
| `CLOCK_CHIP` | 1536, 411 | 1536, 411 |

Then add **Layer ▸ Layer Styles ▸ Outer Glow** to `CLOCK_CHIP` → colour `#727CF5`,
Size 30, **Opacity 0**. It stays at zero until the clock hits 6:00.

---

## 5 · Lease list — panel and dividers

### LIST_PANEL

Shape Layer → Rectangle **1600 × 400**, **Roundness 16**, Fill `#232734`.
Position **960, 773**.

1600 is `1920 − 160 − 160`. Four rows of exactly 100 px.

### LIST_DIVIDERS

**One** shape layer with three rectangles inside it, each **1600 × 1**, Fill `#333A49`.

This is the one layer in the frame using absolute **path** coordinates (see §1). Set the
three rectangle paths:

| Rectangle | Path Position |
|---|---|
| Divider 1 | 960, 673 |
| Divider 2 | 960, 773 |
| Divider 3 | 960, 873 |

Then — **this part is not optional** — neutralise the layer transform, or the path
coordinates get added on top of it:

| `LIST_DIVIDERS` layer | Value |
|---|---|
| Anchor Point | **960, 773** |
| Position | **960, 773** |

Anchor and Position matching puts the layer origin at the divider group's centre, so each
path coordinate resolves to exactly itself:

```
comp position = layer Position + (path Position − layer Anchor Point)

Divider 1 → (960,773) + (0,−100) = 960, 673 ✓
Divider 2 → (960,773) + (0,   0) = 960, 773 ✓
Divider 3 → (960,773) + (0,+100) = 960, 873 ✓
```

> **If the dividers land off-screen, this is why.** Drawing with the Rectangle *tool*
> instead of **Add ▸ Rectangle** leaves a non-zero layer Position, and typing absolute
> coordinates into the paths on top of that stacks the two. A layer sitting at Position
> 1920, 1213 with Anchor 960, 873 throws Divider 1 out to comp (1920, 1013) — a 1600-wide
> bar centred on the frame's right edge with 800 px hanging off — and the others below the
> bottom edge. Fix the layer transform; never compensate in the paths.

Three, not four — no rule above row 1 or below row 4, the panel edge does that.

### Row geometry

Everything below references these:

| Row | Centre Y | Text baseline Y |
|---|---|---|
| 1 | 623 | 637 |
| 2 | 723 | 737 |
| 3 | 823 | 837 |
| 4 | 923 | 937 |

---

## 6 · Row content

Twelve text layers — three per row. All **left aligned**.

**Leave Anchor Point at 0, 0 on every one of these.** Left-aligned text already anchors at
the baseline's left edge, so Position *is* the coordinate in the table. Don't run
Ctrl+Alt+Home, and don't type the row-centre values from §5 into a text layer — those
belong to the `ROW_n` precomps at the bottom of this section.

Each layer's **name is its text** (AE names a text layer after its content automatically).

| Row | Text | Font | Size | Colour | Position |
|---|---|---|---|---|---|
| 1 | `M. Alvarez · Unit 4B` | Inter SemiBold (600) | 26 | `#F0F2F7` | **200, 637** |
| 1 | `$1,450.00` | Inter Medium (500) | 26 | `#F0F2F7` | **575, 637** |
| 1 | `Due Aug 1` | Inter Medium (500) | 24 | `#7E899B` | **825, 637** |
| 2 | `T. Okafor · Unit 12` | Inter SemiBold (600) | 26 | `#F0F2F7` | **200, 737** |
| 2 | `$2,100.00` | Inter Medium (500) | 26 | `#F0F2F7` | **575, 737** |
| 2 | `Due today` | Inter Medium (500) | 24 | `#7E899B` | **825, 737** |
| 3 | `R. Chen · Unit 7A` | Inter SemiBold (600) | 26 | `#F0F2F7` | **200, 837** |
| 3 | `$1,780.00` | Inter Medium (500) | 26 | `#F0F2F7` | **575, 837** |
| 3 | `Due Aug 1` | Inter Medium (500) | 24 | `#7E899B` | **825, 837** |
| 4 | `D. Whitfield · Unit 22` | Inter SemiBold (600) | 26 | `#F0F2F7` | **200, 937** |
| 4 | `$1,325.00` | Inter Medium (500) | 26 | `#F0F2F7` | **575, 937** |
| 4 | `Due Aug 1 · grace ended` | Inter Medium (500) | 24 | `#7E899B` | **825, 937** |

X is the column left edge (200 / 575 / 825); Y is that row's text baseline from §5.
`Tenant · unit`, `Amount` and `Due status` are column *descriptions* — they are never
typed into the comp.

**Character panel check on every one:** Tracking **0**, Vertical Scale **100%**,
Horizontal Scale **100%**. Building these by duplicating the headline carries its
Tracking −20 across, which reads as slightly cramped rather than obviously wrong.

There is nothing to do about tabular figures — see §4, AE has no such control. All four
amounts have the same digit count and exactly one `1`, so they're equal-width anyway, and
they're left-aligned at 575 so the fixed edge is the left one regardless.

> **If a row's text lands in the frame's top-left corner,** that layer has a non-zero
> Anchor Point. Left-aligned text puts its glyph origin at layer-space (0,0), so a layer
> at Anchor 960, 623 and Position 960, 623 renders at comp
> `(960,623) + ((0,0) − (960,623))` = **(0, 0)**. Set Anchor Point back to **0, 0** and
> Position to the table value. Don't try to correct it by moving Position.

Precompose each row's three text layers (**Ctrl+Shift+C**), then set both anchor values:

| Layer | Anchor Point | Position |
|---|---|---|
| `ROW_1` | 960, 623 | 960, 623 |
| `ROW_2` | 960, 723 | 960, 723 |
| `ROW_3` | 960, 823 | 960, 823 |
| `ROW_4` | 960, 923 | 960, 923 |

**Badges stay out of the row precomps.** They pop on the sweep, not with the row, and you
want those keyframes sitting next to `SWEEP_LINE` in the timeline when you tune the pass.

> **Optional copy fix — the due dates don't survive scrutiny.** Rows 1, 3 and 4 all read
> `Due Aug 1` while sitting in three different lifecycle stages: reminder-sent (before
> due), grace day 2, and grace ended. Those can't be the same evening, and a PM is exactly
> the person who'd notice. Anchoring to one **today = Aug 5** fixes it without touching
> anything else:
>
> | Row | Due status |
> |---|---|
> | `ROW_1` | `Due Aug 8` |
> | `ROW_2` | `Due today` |
> | `ROW_3` | `Due Aug 3` |
> | `ROW_4` | `Due Jul 28 · grace ended` |
>
> Four text edits, no geometry change. Your call — the mockup's version is what's spec'd
> above.

---

## 7 · Badges

Pill + text, precomposed as `BADGE_1`–`BADGE_4`. All **48 px tall, Roundness 24**
(= a true pill at that height), all **right-aligned at x 1720** — the panel's right edge
minus 40 px of padding, matching the 40 on the left.

Each pill: **Add ▸ Fill** = its colour with **Fill Opacity 14%**, **Add ▸ Stroke** = the
same colour at **1.5 px**. One colour value per badge gives you tinted body and rim.

| Layer | Text | Colour | Width | Position |
|---|---|---|---|---|
| `BADGE_1` | `Reminder sent` | `#0ACF97` | 216 | **1612**, 623 |
| `BADGE_2` | `Due today` | `#FFBC00` | 168 | **1636**, 723 |
| `BADGE_3` | `Grace — day 2` | `#727CF5` | 224 | **1608**, 823 |
| `BADGE_4` | `Late-fee warning sent` | `#FFBC00` | 312 | **1564**, 923 |

**Those four X values are different on purpose.** Rectangle Position is the *centre*
(handbook §4.3), so a shared right edge means:

```
centre X = 1720 − (width ÷ 2)
```

Typing 1720 into all four staggers the right edge by up to 70 px — it reads fine in a
thumbnail and looks broken at full size. This is the single most likely mistake in the
frame.

Badge text: **Inter SemiBold (600) 24 px**, colour = the badge's own colour, **centre
aligned → Ctrl+Alt+Home**, same Position as its pill. The widths above assume ~26 px of
padding each side — check your render and adjust the *pill width*, not the text.

> **The §6 anchor rule is inverted here — this is the opposite case.** Row text is left
> aligned, so it anchors at the baseline's left edge and keeps Anchor Point 0, 0. Badge
> text is centre aligned, so it must be Ctrl+Alt+Home'd onto its own bounding-box centre
> before you position it (handbook §4.7). Leave a badge's text at Anchor 0, 0 and it
> anchors bottom-left, running out past the right edge of the pill instead of sitting
> inside it.
>
> Order matters: set **centre alignment first**, *then* Ctrl+Alt+Home, *then* Position.
> If a string sits a pixel or two high afterwards, nudge Position Y — text bounds include
> ascender space, so strings with no descenders centre slightly high.

Precompose each pair, then set anchors:

| Layer | Anchor Point | Position |
|---|---|---|
| `BADGE_1` | 1612, 623 | 1612, 623 |
| `BADGE_2` | 1636, 723 | 1636, 723 |
| `BADGE_3` | 1608, 823 | 1608, 823 |
| `BADGE_4` | 1564, 923 | 1564, 923 |

Anchor at the pill's own centre is what makes the pop scale in place instead of swinging
in from the middle of the frame.

Set all four to **Opacity 0** — they don't exist until the sweep reaches them.

> **Amber carries two rows.** `Due today` and `Late-fee warning sent` are both `#FFBC00`,
> which flattens the escalation now that rose is retired. To get the ladder back, give
> `BADGE_4` a filled treatment — Fill Opacity **100%**, text `#16191F` — and leave
> `BADGE_2` outlined. Same colour, unmistakably more severe.

---

## 8 · Sweep line

Shape Layer → Rectangle **1600 × 3**, Roundness 0, Fill `#727CF5`.
Name `SWEEP_LINE`. Position **960, 573**.

Add **Layer ▸ Layer Styles ▸ Outer Glow** → colour `#727CF5`, Opacity **60%**, Size **30**.

Set the layer to **Opacity 0**. Like the badges, its resting state is invisible — left at
100% it sits on the panel's top edge at frame 0 and reads as a stray border.

573 is the panel's top edge — the start of the travel. It ends at **973**, the bottom
edge. Full panel width, so it reads as a pass over the whole book rather than a cursor
picking through it.

Top of the stack, above the badges — it passes *over* the rows, not under them.

> **The line is never visible at 573 or 973.** `LIST_PANEL` has Roundness 16, so at the
> exact top and bottom edges a straight 1600-wide line overhangs the corner curves and its
> tips hang in empty space. The opacity ramps in §11 are timed so it only becomes visible
> once it's ~24 px inside the panel, clear of the radius at both ends. Don't shorten the
> line to compensate — that leaves it too narrow through the middle, where it does the
> actual work.

---

## 9 · Row flash (optional, recommended)

This is what makes the sweep feel causal rather than decorative — each row lights briefly
as the line crosses it, then settles.

Four shape layers, each Rectangle **1600 × 100**, Roundness 0, Fill `#727CF5`,
**layer Opacity 0**:

| Layer | Position |
|---|---|
| `FLASH_1` | 960, 623 |
| `FLASH_2` | 960, 723 |
| `FLASH_3` | 960, 823 |
| `FLASH_4` | 960, 923 |

They sit **below** the rows and above `LIST_DIVIDERS`, so text stays crisp on top.
Keyframing is in §10.

Skip these if you're short on time — the frame works without them. They're the difference
between "a line moved" and "the system worked through the list."

---

## 10 · Timing sheet

Eight seconds, choreographed against the VO:
*"Then, every evening, DueMap closes your day: friendly reminders before rent is due, a
nudge on the day, and compliant late-fee warnings after grace."*

**Start** is the frame the first keyframe goes on; **End** is the frame the last keyframe
goes on. Both are absolute frame numbers in `F6_CONTENT` — nothing here is a duration to
add up yourself.

| Start | End | Layer | Move |
|---|---|---|---|
| 6 | 12 | `EYEBROW` | Rise + fade |
| 12 | 24 | `HEADLINE` | Rise + fade — **hero move**, 12 frames not 6 |
| 30 | 40 | `LIST_PANEL` | Fade + Scale 96 → 100 |
| 34 | 43 | `CLOCK_CHIP` | Pop — `TIME_558` showing, reads **`5:58 PM`** |
| 42 | 48 | `HEADLINE` animator | Amount 0 → 100% — "Handled." turns indigo |
| 42 | 48 | `ROW_1` | Rise + fade |
| 48 | 54 | `ROW_2` | Rise + fade |
| 48 | — | `TIME_558` → `TIME_559` | Instant cut → **`5:59 PM`** |
| 54 | 60 | `ROW_3` | Rise + fade |
| 60 | 66 | `ROW_4` | Rise + fade |
| **64** | — | `TIME_559` → `TIME_600` | Instant cut → **`6:00 PM`** |
| 64 | 82 | `CLOCK_CHIP` Outer Glow | Opacity 0 → 50 → 0% (mid keyframe at 70) |
| **70** | **202** | `SWEEP_LINE` | Travel Y **573 → 973**, linear both ends |
| 70 | 78 | `SWEEP_LINE` | Opacity 0 → 100% |
| 86 | 95 | `BADGE_1` | Pop — sweep crosses row 1 · *"reminders"* |
| 120 | 129 | `BADGE_2` | Pop — row 2 · *"a nudge on the day"* |
| 152 | 161 | `BADGE_3` | Pop — row 3 |
| 186 | 195 | `BADGE_4` | Pop — row 4 · *"after grace"* |
| 194 | 202 | `SWEEP_LINE` | Opacity 100 → 0% as it reaches the bottom edge |
| 202 | 240 | — | **Hold.** No new movement |

Where the End values come from (§11 has the recipes):

- **Rise + fade** = 6 frames. Two keyframes, start and end.
- **Pop** = 9 frames, but **three** keyframes — start, **+5**, **+9**. So `BADGE_1` at
  86 gets keyframes at 86, 91 and 95.
- **Instant cuts** have no end. They're single hold keyframes; nothing interpolates.

### The causal chain

The three beats that make this frame work, in order:

1. **The clock reaches 6:00 PM at frame 64.** Chip glow pulses.
2. **The sweep starts at frame 70** — six frames later, so it reads as *caused by* the
   clock, not coincident with it.
3. **Each badge pops as the line crosses its row.** The product is visibly doing the work.

If you change nothing else about the timing, keep that 64 → 70 gap. It's the whole
argument of the frame in six frames.

### Where the crossings come from

The sweep covers 400 px in 132 frames = **3.03 px/frame**. Row centres are at 623 / 723 /
823 / 923, so it crosses them at frames **86 / 120 / 152 / 186**. If you retime the sweep,
recompute these — a badge popping before or after the line touches its row is immediately
visible and destroys the causality.

### Row flash keyframes (if built)

Each flash fires on its row's crossing frame and decays:

| Layer | Opacity 0% | → 10% | → 0% |
|---|---|---|---|
| `FLASH_1` | 84 | 88 | 102 |
| `FLASH_2` | 118 | 122 | 136 |
| `FLASH_3` | 150 | 154 | 168 |
| `FLASH_4` | 184 | 188 | 202 |

Two frames ahead of the badge pop, so the row lights and *then* the badge lands.

### Falling back to Option B

If the sweep ends up reading as too slow, the alternative is sweep-as-lighting: badges
enter with their rows (pop at 45 / 51 / 57 / 63), and the sweep becomes a single fast pass
at frames 70 → 100 with no causal role. Simpler to time, but it leaves roughly four
seconds of dead hold in an eight-second frame, and it throws away the best beat in the
sequence. Try A first.

---

## 11 · Motion recipes

Brand rule: **200 ms ease-out = 6 frames at 30 fps.** Hero moves get 400 ms (12 frames).

### Rise + fade — eyebrow, headline, rows

Position starts **+24 px on Y** and ends at the final value; Opacity 0 → 100% across the
same span.

| Layer | Position start | Position end |
|---|---|---|
| `EYEBROW` | 160, 169 | 160, 145 |
| `HEADLINE` | 160, 282 | 160, 258 |
| `ROW_1` | 960, 647 | 960, 623 |
| `ROW_2` | 960, 747 | 960, 723 |
| `ROW_3` | 960, 847 | 960, 823 |
| `ROW_4` | 960, 947 | 960, 923 |

### Pop with overshoot — clock chip, badges

Three Scale keyframes, Opacity 0 → 100% across the first 4 frames:

| Frame offset | Scale |
|---|---|
| 0 | 88% |
| +5 | **106%** |
| +9 | 100% |

### Panel entrance

`LIST_PANEL`: Scale **96 → 100%** and Opacity **0 → 100%** over 10 frames. No overshoot —
it's a container, and a container that bounces looks like a mistake.

### Sweep travel

Position Y **573 → 973**, frames 70 → 202. **Leave both keyframes linear.** A scan runs at
constant speed; easing it makes the badge crossings drift off their computed frames and
the whole causal chain stops lining up.

Clean up the ends with opacity instead:

| Frame | Opacity |
|---|---|
| 70 | **0%** |
| 78 | **100%** |
| 194 | **100%** |
| 202 | **0%** |

Four keyframes, not two. The line is at y 597 by frame 78 — past the panel's 16 px corner
curve — and back to y 949 at frame 194, before the bottom curve begins at 957. So it's
only ever visible across the panel's full-width region. All four badge crossings
(86 / 120 / 152 / 186) sit inside that window.

### Clock ticks — cross-cutting the three time layers

Work *inside* the `CLOCK_CHIP` precomp. Frame numbers map 1:1 — precomposing puts the
layer at frame 0 in `F6_CONTENT`, so frame 48 there is frame 48 in here.

Keyframe Opacity (**T**) on all three layers:

| Frame | `TIME_558` | `TIME_559` | `TIME_600` |
|---|---|---|---|
| 34 | **100%** | 0% | 0% |
| 48 | **0%** | **100%** | 0% |
| 64 | 0% | **0%** | **100%** |

Then select every one of those keyframes and **right-click ▸ Toggle Hold Keyframe**
(or Ctrl+Alt+click each one).

**This is the one place in the frame you must convert keyframes by hand.** Opacity
keyframes are linear by default, so without the hold the minutes cross-dissolve — you get
a frame or two where `5:58` and `5:59` are both half-visible on top of each other, which
reads as a render fault rather than a clock. Source Text keyframes would have been hold
automatically; opacity ones are not.

### Clock glow pulse

On `CLOCK_CHIP`, keyframe the **Outer Glow ▸ Opacity**: 0% at frame 64 → **50%** at 70 →
0% at 82.

### Applying the ease correctly

CSS `ease-out` = fast start, soft settle. In AE that means easing the **ending** keyframe:

1. Select the **last** keyframe of the move
2. **Shift+F9** (Easy Ease In)
3. Right-click ▸ **Keyframe Velocity** ▸ Incoming Influence **80%**
4. Leave the first keyframe linear

For the pops, F9 the middle keyframe too, or shape it in the **Graph Editor (Shift+F3)** —
the overshoot should decelerate into 100%, not snap. Never F9 both ends of a 6-frame
move; ease-in-out at that length reads as sluggish.

**Exception:** the sweep travel, per above. Linear both ends.

---

## 12 · Polish pass

- **Motion blur.** Enable the comp master switch and tick the per-layer switch on
  `SWEEP_LINE` — it's the only thing travelling far enough to need it. Leave it off the
  text layers; at 26 px it softens them visibly.
- **Check the badge right edge.** Zoom to 100% and drag the playhead to frame 200. All
  four badges should end on exactly the same vertical. If one is off, it's the §7
  centre-X calculation.
- **Check it small.** Set the Composition panel to 25% and squint. The headline should
  still read, and the sweep should still be legible as a moving line. If the sweep
  disappears at 25%, raise the Outer Glow size rather than the line thickness — a thicker
  line starts looking like a divider.
- **Hold the last 26 frames.** Nothing new after 214, so the editor gets clean handles
  into F7.
- **No idle drift in this frame.** F1 uses `wiggle()` on its clutter because clutter
  should feel unstable. A lease list is the opposite claim — it should feel systemic.
  Leave it perfectly still.

---

## 13 · Finishing

1. Drop `F6_CONTENT` into **MASTER**, laid after `F5_CONTENT` (or after `F4_CONTENT`
   while F5 is outstanding — leave the gap rather than closing it, so F5 drops in later
   without re-timing everything downstream).
2. Confirm `BG_PREVIEW` is still flagged as a Guide Layer — it should vanish the moment
   the comp is nested.
3. **File ▸ Increment and Save.**

Transitions, the colour arc, audio and export are all deferred to
`ae-final-pass-checklist.md`. Don't build an exit on this frame.

---

## 14 · Notes for the final pass

- **Green debuts here.** `Reminder sent` is the film's first `#0ACF97`. The checklist has
  green owning F10 onward as an atmospheric shift — this is a state colour, not the start
  of that ramp. Don't let the MASTER glow arc begin early because of it.
- **Unit 4B is F1's tenant.** The sticky note in F1 said "call unit 4B"; here that same
  lease is row 1, handled automatically, no sticky note needed. It's the strongest callback
  in the film — protect it through any copy edit.
- **F6 appears in both cutdowns** — 6 s in the 30 s performance ad, 5 s in the 15 s
  teaser. The trim point is the tail: everything essential has landed by frame 202, so
  cutting at 150 (5 s) still delivers the clock, the sweep and three of four badges. If
  the 15 s teaser needs all four, speed the sweep to 70 → 150 and recompute the crossings.
- **9:16 reframe.** Because this is a native build rather than a screenshot composite, it
  reframes by restacking rather than content substitution: headline top, clock chip below,
  rows full-width with the badge dropping to a second line per row. The sweep still works
  unchanged. That was the whole reason not to composite over a real capture.

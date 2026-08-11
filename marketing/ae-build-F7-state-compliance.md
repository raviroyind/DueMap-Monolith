# After Effects build guide — F7 "State compliance" (0:38–0:45)

**1920×1080 · 30 fps · 7 s (210 frames)**. All coordinates are comp-space at 1920×1080
and match the approved mockup.

Assumes F6 is built — the conventions are identical and are not re-explained here. Read
§4 of `ae-production-handbook.md` if anything below is unfamiliar.

---

## 1 · Comp setup

1. **Ctrl+N** → `F7_CONTENT`, 1920 × 1080, 30 fps, duration **`0:00:07:00`** (210 frames)
2. **Ctrl+click the time display** — work in frames
3. Solid `#16191F` named `BG_PREVIEW` at the bottom → **Layer ▸ Guide Layer**
4. Make a **`Frame-7/`** folder in the Project panel

**Convention:** build shapes with **Layer ▸ New ▸ Shape Layer** then **Add ▸ Rectangle /
Ellipse**, so the path stays at 0,0 and you set only the *layer* Position. No absolute
path coordinates in this frame — unlike F6, nothing here needs the alternative convention.

**Margins:** left edge 160, right edge 1760. The chip row starts at 160 and the rule card
ends at 1760.

No background, glow, vignette or grain — those live in MASTER only.

---

## 2 · Layer stack

```
F7_CONTENT
├── CARD_TX                          (precomp — contains the card and its ticks)
│   ├── label 3 · TICK_CHECK_3 · TICK_CIRCLE_3
│   ├── label 2 · TICK_CHECK_2 · TICK_CIRCLE_2
│   ├── label 1 · TICK_CHECK_1 · TICK_CIRCLE_1
│   ├── CARD_TITLE
│   └── CARD_BODY                    (the 720 × 292 rectangle)
├── CHIP_TX                          (precomp)
├── CHIP_NY  CHIP_NC  CHIP_GA
├── CHIP_FL  CHIP_CA  CHIP_AZ        (precomps)
├── SUBLINE
├── HEADLINE
├── EYEBROW
└── BG_PREVIEW                       (Guide Layer)
```

`CHIP_TX` sits above the other six so its glow isn't clipped by a neighbour.

The ticks live **inside** `CARD_TX` rather than as sibling comps — they belong to the card,
enter after it, and never move independently of it. That keeps `F7_CONTENT` to ten layers
and means the card can be repositioned later without re-laying three separate elements.

---

## 3 · Text block

All three **left aligned**, **Anchor Point 0, 0**, no Ctrl+Alt+Home.

| Layer | Text | Font | Size | Tracking | Colour | Position |
|---|---|---|---|---|---|---|
| `EYEBROW` | `BUILT-IN COMPLIANCE` | Inter SemiBold | 28 | **+80** | `#7E899B` | 160, 155 |
| `HEADLINE` | `State rules, baked in.` | Inter Tight ExtraBold | 96 | −20 | `#F0F2F7` | 160, 265 |
| `SUBLINE` | `Grace windows · fee caps · required wording.` | Inter Medium | 40 | 0 | `#AEB6C4` | 160, 345 |

Single-line headline — no Leading to set this time.

> As in F6, the mockup's headline renders a little under 96 px. 96 is the brand spec;
> `State rules, baked in.` ends around x 1160, nowhere near the margin. Drop to 88 only if
> you want the comp matched exactly.

### Recolouring "baked in"

**This one uses character indices, not words** — the exception to handbook §4.8.

On `HEADLINE`: **Text ▸ Animate ▸ Fill Color ▸ RGB**, then

1. `Range Selector 1 ▸ Advanced` → **Units → Index** (leave Based On at **Characters**)
2. **Start 13**, **End 21**
3. Animator **Fill Color** → `#727CF5`
4. Leave **Amount** at 0% — keyframed in §8

Characters index from 0: `State rules, ` occupies 0–12, so `baked in` is 13–20 and the
**terminal period at 21 stays white**. Word-based selection can't do that — `in.` is one
word, so it would drag the period into the highlight. This matches F3's tagline, which
excludes its period the same way.

---

## 4 · State chips

Seven chips, **120 × 82, Roundness 12**, evenly pitched **140 px** apart on a shared
centre line at **y 522**.

**Build the chip at comp centre — 960, 540 — not at its final position.** Seven chips
differ only by two letters and an X coordinate, so building one at centre means you
duplicate the comp, change the letters, and set one number per instance. Building each in
place would give you seven different sets of internal coordinates to keep straight.

1. Shape Layer → **Add ▸ Rectangle** 120 × 82, **Roundness 12** → **Add ▸ Fill** →
   **Add ▸ Stroke**. Position **960, 540**.
2. Text layer, **centre aligned → Ctrl+Alt+Home**, Position **960, 540**.
3. Select both → **Ctrl+Shift+C** → `CHIP_AZ`.
4. Duplicate the comp in the Project panel for the other six; change the two letters.

The **Position** column below is where each *instance* goes in `F7_CONTENT` — it is not
an internal coordinate.

| Layer | Text | Fill | Stroke | Text colour | Position |
|---|---|---|---|---|---|
| `CHIP_AZ` | `AZ` | `#232734` | `#333A49` 1px | `#AEB6C4` | 220, 522 |
| `CHIP_CA` | `CA` | `#232734` | `#333A49` 1px | `#AEB6C4` | 360, 522 |
| `CHIP_FL` | `FL` | `#232734` | `#333A49` 1px | `#AEB6C4` | 500, 522 |
| `CHIP_GA` | `GA` | `#232734` | `#333A49` 1px | `#AEB6C4` | 640, 522 |
| `CHIP_NC` | `NC` | `#232734` | `#333A49` 1px | `#AEB6C4` | 780, 522 |
| `CHIP_NY` | `NY` | `#232734` | `#333A49` 1px | `#AEB6C4` | 920, 522 |
| `CHIP_TX` | `TX` | **`#2B3040`** | **`#727CF5` 2px** | **`#F0F2F7`** | 1060, 522 |

Chip text: **Inter SemiBold (600) 32 px, Tracking +20**, **centre aligned →
Ctrl+Alt+Home**, then Position **960, 540** — the same as its rectangle.

> **Centre-aligned text must be Ctrl+Alt+Home'd.** Left at Anchor Point 0, 0 its origin is
> the baseline's *centre*: horizontally correct, but the baseline lands on 540 and the caps
> rise above it, so the letters sit high in the chip. Same rule as F6's badge text, and the
> opposite of F6's row text, which is left aligned and keeps 0, 0.
>
> **If Ctrl+Alt+Home appears to do nothing, check which tool is active.** With the Type
> tool selected the shortcut goes to the text cursor, not the layer. Press **V** for the
> Selection tool, select the layer in the timeline, and use **Layer ▸ Transform ▸ Center
> Anchor Point in Layer Content** from the menu — it's the same command and it can't be
> swallowed by an active text cursor.
>
> Manual fallback: type **Anchor Point 0, −11.5**. Only Y is ever wrong on centre-aligned
> text, and the offset is half the cap height (Inter SemiBold 32 px → cap ≈ 23 px).

On `CHIP_TX` add **Layer ▸ Layer Styles ▸ Outer Glow** → `#727CF5`, Size 25,
**Opacity 0**. It comes up in §8 when TX is selected.

### Placing the seven instances

Because the artwork sits at 960, 540 *inside* each precomp, **Anchor Point is 960, 540 on
all seven** — it points at the artwork, not at the destination. Position is the value from
the table above.

| | Anchor Point | Position |
|---|---|---|
| `CHIP_AZ` | 960, 540 | 220, 522 |
| `CHIP_CA` | 960, 540 | 360, 522 |
| `CHIP_FL` | 960, 540 | 500, 522 |
| `CHIP_GA` | 960, 540 | 640, 522 |
| `CHIP_NC` | 960, 540 | 780, 522 |
| `CHIP_NY` | 960, 540 | 920, 522 |
| `CHIP_TX` | 960, 540 | 1060, 522 |

**Anchor and Position are not the same value here**, unlike F6's rows and badges. They
coincide only when a precomp's artwork was built at its final position; these were built
at centre so they don't. Anchor Point always points at the artwork inside the comp.

**AZ's left edge is 160 and TX's right edge is 1120.** If the row looks off-centre, it
isn't — it's left-aligned to the headline, not centred in the frame.

> **The seven states are load-bearing.** AZ CA FL GA NC NY TX are the launch states with
> real rule versions; the database lists all 50 but only these 7 are backed. Don't add a
> chip to balance the layout. (Script guardrails, §7.)

---

## 5 · Texas rule card

| Layer | Spec | Position |
|---|---|---|
| `CARD_TX` | **720 × 292**, Roundness 16, Fill `#232734`, Stroke `#727CF5` **1.5px** | 1400, 812 |
| `CARD_TITLE` | `Texas rule card` — Inter SemiBold 30, `#F0F2F7`, left aligned | 1072, 738 |

Card spans x 1040 → 1760 and y 666 → 958. Title sits 32 px in from the left edge.

`CARD_TITLE` is left aligned → **Anchor Point 0, 0**, no Ctrl+Alt+Home.

---

## 6 · The three ticks

Build these **inside `CARD_TX`**, alongside `CARD_TITLE` — the ticks belong to the card
and should travel with it. Nine layers, built as one row then duplicated twice.

### Row 1

1. Shape Layer → **Add ▸ Ellipse** → Size **30 × 30**, **Add ▸ Fill** `#0ACF97`.
   Name `TICK_CIRCLE_1`. Position **1087, 780**.
2. Shape Layer, name `TICK_CHECK_1`. **Pen tool (G)** — click three points forming a
   tick roughly 14 px wide and 11 px tall. Exact placement doesn't matter; step 3 fixes it.
   **Add ▸ Stroke** → `#16191F`, Width **3**, **Line Cap Round**, **Line Join Round**.
   No Fill.
3. Press **V**, then **Layer ▸ Transform ▸ Center Anchor Point in Layer Content**, then
   set Position to **1087, 780** — the circle's centre.

   > **Don't assume a pen-drawn layer starts at 0, 0.** AE creates it at **comp centre
   > (960, 540)** with the vertices stored relative to that, so any offset you type is
   > added to 960, 540 rather than to the origin. Centring the anchor on the artwork makes
   > Position mean "where the check appears", which is the only reading that doesn't drift.
   > After this the check and its circle share identical coordinates.
4. **Add ▸ Trim Paths** on the check — this is what animates. End goes to 0% in §7.
5. Text `Grace window` — **Inter Medium (500) 28 px, Tracking 0**, `#F0F2F7`,
   **left aligned**, **Anchor Point 0, 0**, Position **1123, 789**.

> The label is **Medium 28 / Tracking 0** — *not* `CARD_TITLE`'s SemiBold 30 / Tracking 20.
> Duplicating the title to make the first label carries those across and the rows come out
> too heavy.

> **Pen is correct here** — handbook §4.2 says don't draw with Pen *unless you want
> beziers*, and a checkmark is a genuine two-segment path with no parametric equivalent.
> Same exception as F4's connector wires. Everything else in this frame uses
> `Add ▸ Rectangle/Ellipse`.

### Rows 2 and 3 — duplicate, don't rebuild

Select the three row-1 layers, **Ctrl+D**, and change only Position. Because the check's
anchor is now centred, all three layer types take plain absolute coordinates — no offsets,
nothing relative:

| Layer | Text | Anchor Point | Position |
|---|---|---|---|
| `TICK_CIRCLE_1` | — | 0, 0 | 1087, 780 |
| `TICK_CHECK_1` | — | *centred (step 3)* | 1087, 780 |
| `LABEL_1` | `Grace window` | 0, 0 | 1123, 789 |
| `TICK_CIRCLE_2` | — | 0, 0 | 1087, **837** |
| `TICK_CHECK_2` | — | *centred* | 1087, **837** |
| `LABEL_2` | `Fee cap` | 0, 0 | 1123, **846** |
| `TICK_CIRCLE_3` | — | 0, 0 | 1087, **894** |
| `TICK_CHECK_3` | — | *centred* | 1087, **894** |
| `LABEL_3` | `Required wording` | 0, 0 | 1123, **903** |

Row pitch is **57 px**; the label sits **9 px** below its circle because text positions by
baseline while a circle positions by centre. Duplicated layers inherit the anchor
treatment, so the checks don't need re-centring.

### Placing CARD_TX in F7_CONTENT

All the artwork inside was built at its final frame position, so anchor and position match
here (unlike the chips):

| | Anchor Point | Position |
|---|---|---|
| `CARD_TX` | 1400, 812 | 1400, 812 |

---

## 7 · Resting states

Before animating, set these so frame 0 is empty:

| Layer | Resting value |
|---|---|
| `EYEBROW` `HEADLINE` `SUBLINE` | Opacity 0 |
| All seven chips | Opacity 0 |
| `CARD_TX` (the precomp layer in `F7_CONTENT`) | Opacity 0 |
| All six tick circles and labels (inside `CARD_TX`) | Opacity 0 |
| `CHIP_TX` Outer Glow | Opacity 0 |
| `TICK_CHECK` Trim Paths **End** | **0%** (inside each tick comp) |
| `HEADLINE` animator Amount | 0% |

---

## 8 · Timing sheet

Seven seconds, against the VO:
*"Grace windows, fee caps, required wording — state rules are built in, and templates
won't let you drop the legal lines."*

**Start** is the frame the first keyframe goes on; **End** is the last. Both absolute
frame numbers in `F7_CONTENT`.

| Start | End | Layer | Move |
|---|---|---|---|
| 6 | 12 | `EYEBROW` | Rise + fade |
| 12 | 24 | `HEADLINE` | Rise + fade — **hero**, 12 frames |
| 30 | 36 | `SUBLINE` | Rise + fade · *"grace windows, fee caps, required wording"* |
| 36 | 42 | `HEADLINE` animator | Amount 0 → 100% — "baked in" turns indigo |
| 66 | 72 | `CHIP_AZ` | Rise + fade |
| 70 | 76 | `CHIP_CA` | Rise + fade |
| 74 | 80 | `CHIP_FL` | Rise + fade |
| 78 | 84 | `CHIP_GA` | Rise + fade |
| 82 | 88 | `CHIP_NC` | Rise + fade |
| 86 | 92 | `CHIP_NY` | Rise + fade |
| 90 | 96 | `CHIP_TX` | Rise + fade · *"state rules are built in"* |
| **100** | **109** | `CHIP_TX` | **Pop** — TX selects |
| 100 | 106 | `CHIP_TX` Outer Glow | Opacity 0 → 55% |
| 100 | 112 | The other **six** chips | Opacity 100 → **45%** |
| 114 | 126 | `CARD_TX` + `CARD_TITLE` | Rise + fade from Y **+32**, 12 frames |
| 132 | 138 | Tick row 1 | Rise + fade — circle + label together |
| 136 | 141 | `TICK_CHECK_1` | Trim Paths **End 0 → 100%**, 5 frames |
| 144 | 150 | Tick row 2 | Rise + fade |
| 148 | 153 | `TICK_CHECK_2` | Trim Paths End 0 → 100% |
| 156 | 162 | Tick row 3 | Rise + fade |
| 160 | 165 | `TICK_CHECK_3` | Trim Paths End 0 → 100% |
| 165 | 210 | — | **Hold.** No new movement |

**The tick keyframes go inside `CARD_TX`**, not in `F7_CONTENT` — that's where the layers
live. Frame numbers map 1:1, because precomposing places the layer at frame 0. Select each
row's circle and label together so they rise as a unit; the check is keyed separately
because Trim Paths is a different property.

### The beat that carries the frame

Frames **100–112** are the whole idea: TX pops and lights while the other six drop to
45%. That's "your state, specifically" — and it's what earns the rule card that follows.
Without the dim, the card reads as unrelated furniture sitting in the corner.

The 45% dim is the same value F1 uses on its struck-out date chips. Deliberate rhyme:
in both frames it means *these are no longer the ones you're looking at*.

### Trim Paths direction

The checks draw from the short arm to the long one — that's the natural direction a hand
draws a tick. If yours runs backwards, **don't redraw the path**: leave End at 100% and
animate **Start** from 100% → 0% instead (handbook §4.9).

---

## 9 · Motion recipes

Identical to F6. Brand rule: **200 ms ease-out = 6 frames at 30 fps**, hero moves 400 ms.

### Rise + fade

Position starts **+24 px on Y**, Opacity 0 → 100% over the same span.

| Layer | Position start | Position end |
|---|---|---|
| `EYEBROW` | 160, 179 | 160, 155 |
| `HEADLINE` | 160, 289 | 160, 265 |
| `SUBLINE` | 160, 369 | 160, 345 |
| Chips | Y 546 | Y 522 |
| Tick row 1 (circle + label) | Y **+24** from final | 780 / 789 |
| Tick row 2 | Y **+24** from final | 837 / 846 |
| Tick row 3 | Y **+24** from final | 894 / 903 |

Circle and label have different Y values within a row (the label sits 9 px lower), so key
them by offset rather than typing absolutes — select both, set the end keyframe first, then
move back and raise each by 24.

`CARD_TX` uses **+32** rather than +24 — it's a larger object and 24 px reads as a twitch
on something 292 px tall. Position start **1400, 844** → end **1400, 812**.

### Pop — `CHIP_TX` only

Five keyframes across two properties:

| Property | Offset | Value |
|---|---|---|
| Scale | 0 | 88% |
| Scale | +5 | **106%** |
| Scale | +9 | 100% |
| Opacity | — | already at 100 from its entrance; **don't re-key it** |

`CHIP_TX` is the one pop in this frame that is *not* an entrance — it's already on screen
from frame 96. Scale only.

### Easing

**Shift+F9** on the **last** keyframe of every entrance, then Keyframe Velocity ▸ Incoming
Influence **80%**. First keyframe stays linear. On the TX pop, also **F9** the middle
keyframe so the overshoot decelerates into 100%.

The six-chip dim is an exit, not an entrance — ease the **first** keyframe with
**Ctrl+Shift+F9** so it starts slow and settles away.

---

## 10 · Polish

- **Check the chip row at 25% zoom.** Seven chips 140 px apart should read as an even
  rhythm. If one gap looks wrong it's a Position typo, not an optical illusion — the pitch
  is exactly 140 and the centres are 220 / 360 / 500 / 640 / 780 / 920 / 1060.
- **Motion blur off.** Nothing in this frame travels far enough to need it, and it softens
  the 28 px tick labels.
- **No idle drift.** Same reasoning as F6 — a compliance frame that wobbles undercuts its
  own claim.
- **Hold the last 45 frames.** Clean handles into F8.

---

## 11 · Finishing

1. Drop `F7_CONTENT` into **MASTER** after `F6_CONTENT`
2. Confirm `BG_PREVIEW` is still a Guide Layer
3. **File ▸ Increment and Save**

Entrance and hold only. No exit — transitions are designed in the final pass.

---

## 12 · Notes for the final pass

- **Green appears again**, on the three ticks. Second use after F6's `Reminder sent`.
  Still a state colour, not the atmospheric ramp — that starts at F10 per the checklist.
- **The compliance wording is legally load-bearing.** The script guardrails require
  "state-compliant late-fee **warnings**", never "legal advice", and the 7 launch states by
  name. `Texas rule card` / `Grace window` / `Fee cap` / `Required wording` are all safe as
  written — don't let a copy pass promote them into claims about compliance outcomes.
- **F7 is not in either cutdown.** The 30 s runs F1·F3·F4·F6·F8·F9·F14 and the 15 s runs
  F3·F6·F8·F14. So this frame only ever plays in the 90 s master — no trim constraint, and
  no 9:16 version needed.
- **9:16 reframe**, if it's ever wanted: the chip row is the problem, not the card. Seven
  chips at 140 pitch need 960 px of width; in a 1080-wide vertical they'd restack as
  two rows of four and three, with the card full-width beneath.

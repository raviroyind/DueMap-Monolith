# After Effects build guide — F8 "Never touches the money" (0:45–0:51)

**1920×1080 · 30 fps · 6 s (180 frames)**. All coordinates are comp-space at 1920×1080
and match the approved mockup.

The trust frame. The script marks it **near-empty on purpose** — six elements total, and
the long hold at the end is the point, not a gap to fill.

---

## 1 · Comp setup

1. **Ctrl+N** → `F8_CONTENT`, 1920 × 1080, 30 fps, duration **`0:00:06:00`** (180 frames)
2. **Ctrl+click the time display** — work in frames
3. Solid `#16191F` named `BG_PREVIEW` at the bottom → **Layer ▸ Guide Layer**
4. Make a **`Frame-8/`** folder in the Project panel

**This frame is centre-aligned throughout.** Every other frame so far hangs off the 160 px
left margin; F8 abandons it and centres on **x 960**. That's deliberate — the frame is a
single claim with nothing competing, and centring is what makes it read as "stop and
listen" rather than as another feature slide. Don't normalise it back to the left margin.

---

## 2 · Layer stack

```
F8_CONTENT
├── CHECK_BADGE          (precomp — disc + check)
├── LINE_CROSS
├── DOLLAR
├── CIRCLE_THIN
├── SUBLINE
├── HEADLINE
└── BG_PREVIEW           (Guide Layer)
```

`CHECK_BADGE` sits top so it lands over both the circle's edge and the line.

---

## 3 · The symbol

All three centre on **960, 421**.

### CIRCLE_THIN

Shape Layer → **Add ▸ Ellipse** → Size **220 × 220**. **Add ▸ Stroke** → `#3A4152`,
Width **2.5**. **No Fill.** Position **960, 421**.

> If it disappears at 25% zoom, lift the stroke to `#7E899B` rather than thickening it —
> a heavy ring starts reading as a button.

### DOLLAR

Text `$` — **Inter SemiBold (600) 96 px**, `#7E899B`, **centre aligned**.

This one **does** need **Ctrl+Alt+Home**, then Position **960, 421**. You're centring a
glyph inside a container whose centre you know, which is the case the handbook's rule is
for — unlike the headline below, which is positioned by baseline.

### LINE_CROSS

An open path, so this is a **Pen** layer — `Add ▸ Rectangle` has no parametric equivalent
for a line you can draw on with Trim Paths.

1. **Pen tool (G)** — click two points roughly horizontal and roughly 290 px apart.
   Exact placement doesn't matter; steps 4–5 fix it.
2. **Add ▸ Stroke** → `#727CF5`, Width **4**, **Line Cap Round**. No Fill.
3. **Add ▸ Trim Paths**. End stays 0% until §6.
4. Press **V**, then **Layer ▸ Transform ▸ Center Anchor Point in Layer Content**
5. **Rotation −38°**, **Position 960, 421**
6. Adjust **Scale** until the line overshoots the circle by roughly **35 px at each end** —
   that overshoot is what makes it read as struck *through* rather than as a chord

Same lesson as F7's checkmarks: a pen-drawn layer starts at comp centre with its vertices
baked in, so centre the anchor and let Position mean "where it appears."

---

## 4 · CHECK_BADGE

Same construction as F7 §6, scaled up. **Two layers, then one precomp:**

```
CHECK_BADGE        ← the precomp — this is what §6–§8 refer to
├── BADGE_TICK     ← the checkmark path
└── BADGE_DISC     ← the green circle
```

| Layer | Spec | Position |
|---|---|---|
| `BADGE_DISC` | Ellipse **66 × 66**, Fill `#0ACF97` | 1057, 511 |
| `BADGE_TICK` | Pen, 3 points, Stroke `#16191F` **5 px**, Round Cap + Join | 1057, 511 |

Draw the tick, **Ctrl+Alt+Home**, then Position **1057, 511** — the same coordinate as the
disc, so it centres itself on it.

Select both → **Ctrl+Shift+C** → `CHECK_BADGE`, then:

| | Anchor Point | Position |
|---|---|---|
| `CHECK_BADGE` | 1057, 511 | 1057, 511 |

All three share 1057, 511: the two inner layers are centred on the same point inside the
comp, and the comp is placed at that point in the frame.

The badge centre sits 132 px from the circle's centre, which has a 110 px radius — so the
disc straddles the ring's lower-right edge, overlapping it by about 11 px. That overlap is
load-bearing: floating clear of the ring it reads as an unrelated dot.

> **Check colour.** `#16191F` matches F7's ticks. The mockup renders it closer to white —
> use `#F0F2F7` if you want the comp matched exactly. Pick one and use it in both frames.

---

## 5 · Text

### HEADLINE

`DueMap never touches the money.` — **Inter Tight ExtraBold (800) 96 px**, Tracking −20,
`#F0F2F7`, **centre aligned**, **Anchor Point 0, 0**, Position **960, 672**.

> **No Ctrl+Alt+Home here, unlike `DOLLAR`.** The rule isn't "centre-aligned text always
> gets it" — it's:
>
> - **Centring inside a container** you know the middle of (badge, chip, the ring above) →
>   Ctrl+Alt+Home, so Position means the visual centre.
> - **Positioning by baseline**, as every headline in this film is → leave Anchor at 0, 0.
>   Centre alignment already puts the horizontal centre on Position X for free; Y is the
>   baseline, which is what the coordinate above is.
>
> Ctrl+Alt+Home'ing a headline moves the anchor vertically too, and then 672 is wrong.

**No indigo word in this headline** — same as F5. The accent in this frame is the indigo
line through the dollar. Two accents would split it.

### SUBLINE

`It notifies and documents. Your books do the charging.` — **Inter Medium (500) 40 px**,
`#AEB6C4`, **centre aligned**, **Anchor Point 0, 0**, Position **960, 756**.

**Two-tone**, per the mockup — the second sentence lifts to a light indigo:

1. **Text ▸ Animate ▸ Fill Color ▸ RGB**
2. `Range Selector 1 ▸ Advanced` → **Units → Index**, leave Based On at **Characters**
3. **Start 27**, **End 54**
4. Animator **Fill Color** → `#9BA3E8`
5. Leave **Amount** at 0% — keyframed in §6

Character 27 is the `Y` of `Your`. Characters again rather than words, for the same reason
as F7: the split falls mid-sentence and needs to include the terminal period.

> Flat `#AEB6C4` across the whole line is the brand-kit-safe fallback if the two-tone
> reads as a rendering fault rather than emphasis. Check it at 25% zoom before deciding.

---

## 6 · Resting states

| Layer | Value |
|---|---|
| `CIRCLE_THIN` `DOLLAR` `HEADLINE` `SUBLINE` | Opacity 0 |
| `CHECK_BADGE` | Opacity 0 |
| `LINE_CROSS` Trim Paths **End** | **0%** |
| `SUBLINE` animator **Amount** | 0% |

`LINE_CROSS` layer Opacity stays at **100** — Trim Paths handles its reveal, and keying
both makes the stroke fade in as it draws instead of drawing crisply.

---

## 7 · Timing sheet

Six seconds, against the VO:
*"And DueMap never moves money. It reminds, warns, and documents — the charging stays in
your books, under your control."*

| Start | End | Layer | Move |
|---|---|---|---|
| 6 | 18 | `CIRCLE_THIN` | Fade + Scale **92 → 100** |
| 12 | 24 | `DOLLAR` | Fade in |
| **24** | **36** | `LINE_CROSS` | **Trim Paths End 0 → 100%** · *"never moves money"* |
| 42 | 54 | `HEADLINE` | Rise + fade — **hero**, 12 frames |
| 60 | 66 | `SUBLINE` | Rise + fade · *"it reminds, warns, and documents"* |
| 72 | 78 | `SUBLINE` animator | Amount 0 → 100% — second sentence lifts |
| 96 | 105 | `CHECK_BADGE` | Pop · *"stays in your books"* |
| 105 | 180 | — | **Hold.** 75 frames, nothing moves |

### The 75-frame hold is the frame

Two and a half seconds of stillness at the end of a six-second frame would be a defect
anywhere else in this film. Here it's the whole argument: the frame claims the product
does *less* than you'd expect, and a busy frame contradicts that before the VO finishes.
Resist filling it.

### The line, then the check

The script's direction is that the line "resolves into a checkmark." It isn't a morph —
the line strikes through the dollar at frame 24–36, and the check lands 60 frames later at
the ring's lower-right. The gap is what makes it read as *consequence* rather than
decoration: crossed out, then confirmed.

### Trim Paths direction

The line should draw **lower-left → upper-right**. Upward is what makes the gesture read
as resolving rather than cancelling. If it draws the wrong way, don't redraw the path —
leave End at 100% and animate **Start** from 100% → 0% instead.

---

## 8 · Motion recipes

### Rise + fade

| Layer | Position start | Position end |
|---|---|---|
| `HEADLINE` | 960, 696 | 960, 672 |
| `SUBLINE` | 960, 780 | 960, 756 |

### Circle entrance

Scale **92 → 100%**, Opacity **0 → 100%**, 12 frames. No overshoot — it's the frame's
anchor object and a bounce undercuts the tone.

### Pop — `CHECK_BADGE`

| Property | Frame | Value |
|---|---|---|
| Scale | 96 | 88% |
| Scale | 101 | **106%** |
| Scale | 105 | 100% |
| Opacity | 96 → 100 | 0 → 100% |

### Easing

**Shift+F9** on the **last** keyframe of every entrance, Incoming Influence **80%**. First
keyframe linear. **F9** the middle keyframe of the badge pop.

---

## 9 · Polish

- **Nothing drifts in this frame.** No parallax, no wiggle. F8 is the one frame where
  absolute stillness is the message.
- **Check the symbol at 25%.** The ring, the dollar and the line all sit in a narrow
  tonal band. If the ring vanishes, lift its stroke colour — don't thicken it.
- **Motion blur off.** Nothing travels.
- **Hold the last 75 frames** with no new movement, so the editor has clean handles both
  sides.

---

## 10 · Finishing

1. Drop `F8_CONTENT` into **MASTER** after `F7_CONTENT`
2. Confirm `BG_PREVIEW` is still a Guide Layer
3. **File ▸ Increment and Save**

---

## 11 · Notes for the final pass

- **This frame is in both cutdowns** — 4 s in the 30 s performance ad, 3 s in the 15 s
  teaser. Everything lands by frame **105**, so a 4 s trim (120 frames) is a clean cut with
  no retime. The **3 s teaser (90 frames) needs the badge pulled forward** to roughly
  78 → 87; that's the only change either cut requires.
- **The copy is legally load-bearing — do not soften or embellish it.** The script's
  guardrails require that the ad never implies DueMap moves, holds, or collects money.
  `never touches the money` / `It notifies and documents.` / `Your books do the charging.`
  are the exact claims. A copy pass that tightens this into something punchier is the one
  edit that could cause real trouble.
- **Green's third appearance**, after F6's `Reminder sent` and F7's rule ticks. Still a
  state colour, not the atmospheric ramp — that starts at F10 per the checklist.
- **9:16 reframes cleanly.** Everything is already centred on x 960 and stacked
  vertically; the vertical version is a pure re-centre with the headline bumped to ~124 px.
  This is the easiest frame in the film to reframe, which is convenient given it's in the
  15 s teaser.

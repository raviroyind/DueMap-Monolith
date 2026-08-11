# After Effects build guide — F1 "The chase" (0:00–0:06)

Master comp: **1920×1080 · 30 fps · 6 s (180 frames)**
All coordinates below are for the 1920×1080 canvas and match the approved mockup.

---

## 0 · Before you open AE

**Install the fonts locally.** The app loads Inter from a CDN; AE needs them on the system.

- Download **Inter** and **Inter Tight** from Google Fonts → install the static weights
  (400, 500, 600, 700, 800). Variable fonts work in AE but static weights are safer for
  render-farm/round-trip consistency.
- Headlines use **Inter Tight ExtraBold (800)**. If you skip Inter Tight, Inter 800 is an
  acceptable fallback — just widen tracking slightly to compensate.

**Colour reference** (paste into a swatch set):

| Token | Hex | Used for |
|---|---|---|
| Ink | `#16191F` | Background |
| Panel | `#232734` | Chips, spreadsheet row |
| Hairline | `#333A49` | Borders, cell dividers |
| Rose | `#FA5C7C` | "chasing", OVERDUE, edge glow, active chip rim |
| Indigo | `#727CF5` | Chat bubbles |
| Amber | `#FFBC00` | Sticky note |
| Heading | `#F0F2F7` | Headline, AUG 12, money |
| Body | `#AEB6C4` | Cell labels |
| Muted | `#7E899B` | Eyebrow, struck-out dates |

---

## 1 · Project & comp setup

1. **File ▸ Project Settings ▸ Color** → Depth: **16 bits per channel**.
   *Not optional.* Large soft glows on a near-black background band visibly at 8 bpc,
   and it looks like a compression artifact in the final H.264.
2. Color Working Space: **sRGB IEC61966-2.1** (matches how the app's screenshots were captured).
3. **Composition ▸ New Composition** (Ctrl+N):
   - Name `F1_The_Chase`
   - Width 1920, Height 1080, Square Pixels
   - Frame Rate **30**
   - Duration `0:00:06:00`
   - Background Color: black (you'll cover it anyway)
4. Turn on **Motion Blur** for the comp (the master switch in the timeline header). Enable the
   per-layer motion-blur switch on anything that moves — the sticky note especially.

---

## 2 · Layer stack (build bottom-up)

Final order, top to bottom:

```
11  GRAIN                (adjustment)
10  VIGNETTE
 9  HEADLINE_L2          ("Then the chasing starts.")
 8  HEADLINE_L1          ("Rent day comes.")
 7  EYEBROW              ("EVERY MONTH")
 6  BUBBLE_2             ("Following up again…")
 5  BUBBLE_1             ("Hey, rent this week?")
 4  ROW_SPREADSHEET      (Unit 4B | $1,450.00 | OVERDUE)
 3  STICKY_NOTE          ("call unit 4B")
 2  CHIPS                (precomp: AUG 1 / AUG 5 / AUG 12)
 1  GLOW_ROSE_R + GLOW_ROSE_BL
 0  BG_INK
```

---

## 3 · Background and atmosphere

### BG_INK
- **Layer ▸ New ▸ Solid** (Ctrl+Y) → 1920×1080, colour `#16191F`, name `BG_INK`.

### GLOW_ROSE_R (right edge)
1. New Solid, colour `#FA5C7C`, full-comp size, name `GLOW_ROSE_R`.
2. Draw an **elliptical mask** (Q for ellipse tool, double-click to fill the layer, then scale)
   roughly 1100×900 px, positioned at **x 1750, y 400** — mostly off-canvas right.
3. Mask Feather: **500, 500**. Mask Expansion: 0.
4. Blending Mode: **Add**. Opacity: **18%** (it will animate up later).

### GLOW_ROSE_BL (bottom-left)
Duplicate `GLOW_ROSE_R`, rename, move to **x 150, y 1000**, mask ~900×700, Opacity **12%**.

> **Why Add and not Screen:** Add blows out hotter in the middle, which is what you want for a
> light source. Screen stays flatter. On a near-black background Add reads as a real glow.

### VIGNETTE
1. New Solid, colour `#000000`, name `VIGNETTE`.
2. Elliptical mask covering ~2400×1400 centred, **Inverted**, Feather **400**.
3. Opacity **35%**.

### GRAIN
1. **Layer ▸ New ▸ Adjustment Layer**, name `GRAIN`.
2. **Effect ▸ Noise & Grain ▸ Noise** → Amount of Noise **2.5%**, *uncheck* Use Color Noise.
3. Opacity **50%**.

> Grain is doing real work here, not styling: it dithers the glow gradients and kills the
> banding that H.264 would otherwise exaggerate.

---

## 4 · Date chips (precomp)

Build one chip, then duplicate twice.

### Chip geometry
| Chip | Centre (x, y) | Box size | Font size | Text colour | Border |
|---|---|---|---|---|---|
| AUG 1 | 249, 190 | 172×66 | 30 px | `#7E899B` | `#333A49` 1 px |
| AUG 5 | 443, 190 | 172×66 | 30 px | `#7E899B` | `#333A49` 1 px |
| AUG 12 | 686, 190 | 268×104 | 52 px | `#F0F2F7` | `#FA5C7C` 2 px |

### Build one chip
1. **Layer ▸ New ▸ Shape Layer**. Add ▸ **Rectangle Path** → set Size, and **Roundness 10**.
2. Add ▸ **Fill** → `#232734`. Add ▸ **Stroke** → `#333A49`, Width 1.
3. Type tool (Ctrl+T) → "AUG 1", **Inter Tight SemiBold (600)**, size 30, colour `#7E899B`,
   Tracking **+20**. Centre it on the rectangle using **Window ▸ Align**.
4. Select both layers → **Ctrl+Shift+C** → precompose as `CHIP_AUG1`.
5. **Pan Behind tool (Y)** → drag the anchor point to the chip's visual centre. Do this now;
   scale animations rotate around the anchor and off-centre anchors look broken.

Duplicate for AUG 5 and AUG 12. For **AUG 12**: bigger box + type, white text, rose 2 px stroke,
and add **Layer ▸ Layer Styles ▸ Outer Glow** → colour `#FA5C7C`, Opacity 45%, Size 25.

### Strikethrough (on AUG 1 and AUG 5 only)
1. Inside each chip precomp: **Layer ▸ New ▸ Shape Layer**, name `STRIKE`.
2. Pen tool (G) → click once at the left edge of the text, **Shift-click** at the right edge
   (Shift constrains it horizontal). Two points, one straight path.
3. Add ▸ **Stroke** → `#7E899B`, Width **3**, Line Cap **Round**.
4. Add ▸ **Trim Paths** → this is what animates. Keyframe **End: 0% → 100%** over 5 frames.

---

## 5 · Sticky note

1. Shape Layer → Rectangle Path **220×86**, Roundness 2, Fill `#FFBC00`.
2. Text: "call unit 4B ✱", **Inter SemiBold (600)**, 24 px, colour `#16191F`.
   (The asterisk is a glyph, not a separate layer — keeps it welded to the text.)
3. Select both → precompose as `STICKY_NOTE`. Anchor point to centre (Y tool).
4. **Position: 1533, 174. Rotation: −4°.**
5. **Layer ▸ Layer Styles ▸ Drop Shadow** → Opacity 45%, Distance 8, Size 20, Angle 120°.

---

## 6 · Spreadsheet row

Total row: **500×62 px**, centred at **1506, 372**.

1. Shape Layer, Rectangle Path 500×62, Roundness 6, Fill `#232734`.
2. Two divider lines: Pen tool vertical strokes, `#333A49`, 1 px, at local x −80 and +90.
3. Three text layers, all **Inter Medium (500)**, 24 px:
   - `Unit 4B` → `#AEB6C4`, left cell
   - `$1,450.00` → `#F0F2F7`, middle cell, **enable Tabular Figures** in the Character panel
   - `OVERDUE` → `#FA5C7C`, **Bold (700)**, Tracking +40, right cell
4. Precompose all as `ROW_SPREADSHEET`.

> **Tabular figures matter.** Proportional digits make money columns visibly ragged, and
> a PM's eye lands on numbers first. Character panel ▸ the `0 0` icon.

---

## 7 · Chat bubbles

| Layer | Text | Centre | Bubble | Text colour |
|---|---|---|---|---|
| BUBBLE_1 | "Hey, rent this week?" | 1426, 515 | 330×62, Roundness 31 | `#FFFFFF` |
| BUBBLE_2 | "Following up again…" | 1424, 600 | 330×62, Roundness 31 | `#FFFFFF` @ 55% |

Both: Fill `#727CF5`. Text **Inter Medium (500)**, 24 px.
Set **BUBBLE_2 layer opacity to 78%** so it recedes — that's the "unanswered, again" feeling.
Precompose each; anchor to centre.

---

## 8 · Eyebrow and headline

### EYEBROW
- Text: `EVERY MONTH`
- **Inter SemiBold (600)**, 28 px, **Tracking +80** (AE units = 1/1000 em, so +80 = +8%)
- Colour `#7E899B`, left-aligned
- Position: left edge at **x 160**, baseline **y 692**

### HEADLINE (two layers)
- **Inter Tight ExtraBold (800)**, **96 px**, **Leading 100 px**, **Tracking −20**
- Colour `#F0F2F7`, left-aligned, left edge **x 160**
- `HEADLINE_L1` — "Rent day comes." baseline **y 820**
- `HEADLINE_L2` — "Then the chasing starts." baseline **y 920**

### Recolouring "chasing" — do it with a Text Animator, not a split layer

On `HEADLINE_L2`:

1. Expand the layer ▸ **Text ▸ Animate ▸ Fill Color ▸ RGB**.
2. Set the new **Fill Color** to `#FA5C7C`.
3. Open **Range Selector 1 ▸ Advanced ▸ Units → Index**.
4. Set **Start = 9**, **End = 16** (character indices covering `chasing` — count from 0 and
   verify on screen; adjust by one if your spacing differs).
5. Animate **Range Selector 1 ▸ Amount: 0% → 100%** over 6 frames at 0:02.8.

> Splitting the headline into three text layers also "works", but you lose real kerning
> across the seams and every future copy tweak means re-aligning three layers. The animator
> keeps it one editable string.

Optionally add **Effect ▸ Stylize ▸ Glow** to `HEADLINE_L2` (Threshold 60%, Radius 30,
Intensity 0.4) so the rose word carries a faint bloom.

---

## 9 · Timing sheet

Six seconds, choreographed against the VO:
*"Rent day comes every month. So does the chasing — the texts, the spreadsheets, the awkward late-fee math."*

| Time | Frame | Layer | Move |
|---|---|---|---|
| 0:00.0 | 0 | BG, glows, vignette | Already on |
| 0:00.2 | 6 | CHIP_AUG1 | Rise + fade in |
| 0:00.6 | 18 | STRIKE (AUG 1) | Trim End 0→100%, chip → 45% opacity |
| 0:00.9 | 27 | CHIP_AUG5 | Rise + fade in |
| 0:01.3 | 39 | STRIKE (AUG 5) | Trim End 0→100%, chip → 45% opacity |
| 0:01.6 | 48 | CHIP_AUG12 | Scale 0.9 → **1.06** → 1.0, glow pulse |
| 0:01.9 | 57 | EYEBROW | Fade in |
| 0:02.1 | 63 | HEADLINE_L1 | Rise + fade |
| 0:02.4 | 72 | HEADLINE_L2 | Rise + fade (still all white) |
| **0:02.8** | **84** | **"chasing"** | **Recolour white → rose** — lands on the spoken word |
| 0:03.2 | 96 | BUBBLE_1 | Pop in ("the texts") |
| 0:03.5 | 105 | BUBBLE_2 | Pop in |
| 0:04.0 | 120 | ROW_SPREADSHEET | Slide in from right ("the spreadsheets") |
| 0:04.6 | 138 | STICKY_NOTE | Drop + rotate in |
| 0:04.9 | 147 | OVERDUE | Scale pop + flash ("late-fee math") |
| 0:05.0→0:06.0 | 150→180 | GLOW_ROSE_R / _BL | Opacity 18→32% and 12→24% |

**The headline lands at 2.1 s and holds for ~3.9 s.** That's deliberate — two lines of 96 px
type need roughly three seconds of read time, so it can't wait for the clutter to finish.
The clutter accumulates *around* an already-readable headline.

---

## 10 · Motion recipes

Brand rule: **200 ms ease-out = 6 frames at 30 fps.** Hero moves get 400 ms (12 frames).

### Rise + fade (headline, eyebrow, chips)
- Position: **start +24 px on Y**, end at final value. 6 frames.
- Opacity: 0% → 100%. Same 6 frames.

### Pop with overshoot (chat bubbles, AUG 12, OVERDUE)
Three Scale keyframes:
| Frame offset | Scale |
|---|---|
| 0 | 88% |
| +5 | **106%** |
| +9 | 100% |
Opacity 0→100% across the first 4 frames.

### Sticky note drop
- Position Y: **−90 px** offset → final, 10 frames
- Rotation: **−16°** → **−4°**, 10 frames
- Scale: 90% → 100%
- Opacity: 0 → 100% over the first 5 frames

### Slide in (spreadsheet row)
- Position X: **+80 px** → final, 8 frames. Opacity 0 → 100%.

### Applying the ease correctly
CSS `ease-out` = fast start, slow settle. In AE that means easing the **ending** keyframe:

1. Select the **last** keyframe of the move.
2. **Shift+F9** (Easy Ease In).
3. Right-click ▸ **Keyframe Velocity** ▸ Incoming Influence **80%**.
4. Leave the first keyframe linear.

For the overshoot pops, F9 the middle keyframe too, or shape it in the **Graph Editor
(Shift+F3)** — the overshoot should decelerate into 100%, not snap.

---

## 11 · Polish pass

- **Idle drift.** Parent every clutter layer to a Null (`CTRL_CLUTTER`) and give the null a
  slow 8-px sine wobble so the frame is never perfectly static:
  `wiggle(0.3, 8)` on the null's Position.
- **Glow pulse on AUG 12.** Keyframe the Outer Glow Opacity 45% → 60% → 45% over ~20 frames,
  looping. Sells "this is the date that matters."
- **Hold the last frame.** Leave the final 10 frames with no new movement so the editor has
  clean handles for the cut into F2.
- **Check it small.** Set the Composition panel to 25% and squint. If the headline still
  reads and the clutter still feels like clutter, the frame works on a phone.

---

## 12 · Export

**Master (edit-grade):**
1. **Composition ▸ Add to Render Queue** (Ctrl+M)
2. Output Module ▸ Format **QuickTime** ▸ Codec **Apple ProRes 422 HQ**
   (AE on Windows exports ProRes natively — no plugin needed)
3. Colour: leave at project working space. Render.

**Delivery:** take the ProRes into **Media Encoder** → H.264, VBR 2-pass, target 12–16 Mbps
for 1080p. Don't export H.264 straight from AE for the master — you want one high-quality
intermediate that every platform version is derived from.

**Naming:** `DueMap_F1_TheChase_1920x1080_ProResHQ_v01.mov`

---

## 13 · Setting up now for the vertical cut

F1 reframes to 9:16 more easily than most frames in this film, but only if you prepare:

- Keep the headline's left edge and both lines inside a **1080×1080 centred square**. Then
  1:1 and 4:5 are pure crops with zero rework.
- For **1080×1920**, the clutter must restack rather than reposition: chips go top, headline
  centre, clutter items stack vertically beneath. Because every element is already parented to
  a null and precomposed, that's one null move per group.
- Bump headline type to **~124 px** in the vertical — 96 px is small on a phone at arm's length.
- Do this *after* the 16:9 is approved. Don't build both in parallel and maintain two edits.

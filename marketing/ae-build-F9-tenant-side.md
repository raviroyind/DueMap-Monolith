# After Effects build guide — F9 "The tenant side" (0:51–0:58)

**1920×1080 · 30 fps · 7 s (210 frames)**. All coordinates are comp-space at 1920×1080
and match the approved mockup.

Back to the left-margin system after F8's centred frame: text on the 160 margin, phone
mockup on the right.

---

## 1 · Comp setup

1. **Ctrl+N** → `F9_CONTENT`, 1920 × 1080, 30 fps, duration **`0:00:07:00`** (210 frames)
2. **Ctrl+click the time display** — work in frames
3. Solid `#16191F` named `BG_PREVIEW` at the bottom → **Layer ▸ Guide Layer**
4. Make a **`Frame-9/`** folder in the Project panel

**Convention:** shapes via **Layer ▸ New ▸ Shape Layer** then **Add ▸ Rectangle/Ellipse** —
path at 0,0, coordinate in the *layer* Position.

---

## 2 · Layer stack

```
F9_CONTENT
├── CAPTION
├── BTN_PAY              (precomp)
├── ATTACH_CHIP          (precomp)
├── BAR_3  BAR_2  BAR_1
├── SUBJECT
├── EMAIL_HEADER         (precomp)
├── PHONE_FRAME          (precomp)
├── SUBLINE
├── HEADLINE
└── BG_PREVIEW           (Guide Layer)
```

Screen content sits **above** `PHONE_FRAME` as sibling layers rather than nested inside
it. The phone enters as one object, then the notice assembles on top of it — that
sequencing is the frame, and nesting the content would make each element's timing a
level removed from where you're working.

No mask needed: every content element sits well inside the screen bounds.

---

## 3 · Text

Both **left aligned**, **Anchor Point 0, 0**, no Ctrl+Alt+Home (positioned by baseline).

| Layer | Text | Font | Size | Leading | Tracking | Colour | Position |
|---|---|---|---|---|---|---|---|
| `HEADLINE` | `Tenants get one ⏎ tap to pay.` | Inter Tight ExtraBold | 96 | **100** | −20 | `#F0F2F7` | 160, 462 |
| `SUBLINE` | `Real invoice attached · Pay Now links ⏎ to your books.` | Inter Medium | 40 | **50** | 0 | `#AEB6C4` | 160, 653 |

### Recolouring "one tap"

**This is the frame where word-based selection finally pays off.** The highlighted phrase
straddles the line break — `one` ends line 1, `tap` begins line 2 — and word indices don't
care.

1. **Text ▸ Animate ▸ Fill Color ▸ RGB**
2. `Range Selector 1 ▸ Advanced` → **Based On → Words**, *then* **Units → Index**
3. **Start 2**, **End 4**
4. Animator **Fill Color** → `#727CF5`
5. Leave **Amount** at 0% — keyframed in §8

Words index from zero: `Tenants`(0) `get`(1) `one`(2) `tap`(3) `to`(4) `pay.`(5).

Character indices would work today and break the moment the headline re-breaks at a
different width — which is exactly what happens when this frame gets reframed for 9:16.

> **Keep the sub-line flat `#AEB6C4`.** The mockup renders `Pay Now` slightly lifted, but
> this headline already carries an indigo phrase. F8's sub-line could take an accent
> *because* its headline had none — that's the rule, one accent per frame, not one per
> text layer. If you do want it, use `#9BA3E8` and drop the headline's recolour.

---

## 4 · The phone

Three shapes, precomposed as `PHONE_FRAME`. All centre on **1401, 561**.

| Layer | Spec | Position |
|---|---|---|
| `PHONE_BODY` | Rectangle **480 × 936**, **Roundness 48**, Fill `#232734`, Stroke `#333A49` 2 px | 1401, 561 |
| `PHONE_SCREEN` | Rectangle **440 × 888**, **Roundness 40**, Fill `#16191F` | 1401, 561 |
| `PHONE_SPEAKER` | Rectangle **112 × 24**, **Roundness 12**, Fill `#333A49` | 1401, 142 |

| | Anchor Point | Position |
|---|---|---|
| `PHONE_FRAME` | 1401, 561 | 1401, 561 |

The 20 px bezel comes from `(480 − 440) ÷ 2`. If you change either width, change both or
the bezel goes uneven and the phone stops reading as a device.

---

## 5 · The notice

Everything below sits on the screen. Content left edge is **1211**, right edge **1589**.

### EMAIL_HEADER (precomp)

| Layer | Spec | Position |
|---|---|---|
| `HDR_SPARK` | `duemap-spark.svg`, Scale ~3.3% (≈26 px tall) | 1227, 210 |
| `HDR_WORDMARK` | `DueMap` — Inter Tight ExtraBold 28, `#F0F2F7`, left aligned | 1251, 219 |
| `HDR_DATE` | `Aug 12` — Inter Medium 22, `#7E899B`, **right aligned** | 1589, 218 |

`HDR_DATE` is right aligned, so its anchor is the baseline's **right** edge — Anchor 0, 0
and Position 1589 pins the right edge there. Don't Ctrl+Alt+Home it.

| | Anchor Point | Position |
|---|---|---|
| `EMAIL_HEADER` | 1401, 215 | 1401, 215 |

### SUBJECT

`Rent notice — Unit 4B` — **Inter SemiBold 28**, `#F0F2F7`, left aligned,
Anchor 0, 0, Position **1211, 277**.

### Skeleton bars

Height **14**, **Roundness 7**, Fill `#3A4152`, all sharing left edge **1211**:

| Layer | Width | Position |
|---|---|---|
| `BAR_1` | 378 | **1400**, 313 |
| `BAR_2` | 365 | **1393**, 337 |
| `BAR_3` | 293 | **1357**, 361 |

**Three different centre X values** — `centre = 1211 + (width ÷ 2)`. Same trap as F6's
badges and F2's card bars. Typing one value into all three staggers the left edge by up to
42 px, which is the edge the eye actually reads.

### ATTACH_CHIP (precomp)

| Layer | Spec | Position |
|---|---|---|
| `CHIP_BODY` | Rectangle **378 × 56**, Roundness 10, Fill `#2B3040` | 1400, 420 |
| `CHIP_PDF` | Rectangle **42 × 20**, Roundness 4 — see note | 1258, 420 |
| `CHIP_PDF_TEXT` | `PDF` — Inter SemiBold 14, centre aligned + Ctrl+Alt+Home | 1258, 420 |
| `CHIP_NAME` | `invoice-0142.pdf` — Inter Medium 22, `#F0F2F7`, left aligned | 1296, 427 |
| `CHIP_AMOUNT` | `$1,450.00` — Inter Medium 22, `#AEB6C4`, **right aligned** | 1568, 427 |

| | Anchor Point | Position |
|---|---|---|
| `ATTACH_CHIP` | 1400, 420 | 1400, 420 |

> **The PDF badge colour needs a decision.** The mockup uses a red that reads as
> `#FA5C7C` — but **rose is retired after F2**; it's the film's pain colour and this is a
> resolution frame. Reintroducing it 45 seconds later, even on a file-type chip, is a
> visible break in the colour story.
>
> Recommended: `#3A4152` fill with `#AEB6C4` text — neutral, and the word `PDF` carries
> the meaning without the colour. If you want the conventional red affordance, use a
> desaturated `#C4566B` so it reads as "file type" rather than as rose returning. Don't
> use `#FA5C7C`.

### BTN_PAY (precomp)

| Layer | Spec | Position |
|---|---|---|
| `BTN_BODY` | Rectangle **384 × 76**, **Roundness 12**, Fill `#727CF5` | 1400, 513 |
| `BTN_TEXT` | `Pay now →` — Inter SemiBold **32**, `#FFFFFF`, centre aligned + **Ctrl+Alt+Home** | 1400, 513 |

| | Anchor Point | Position |
|---|---|---|
| `BTN_PAY` | 1400, 513 | 1400, 513 |

12 px radius is the brand kit's CTA spec. The arrow is a glyph in the same string, not a
separate layer.

### CAPTION

`Opens your payment page` — **Inter Medium 20**, `#7E899B`, **centre aligned +
Ctrl+Alt+Home**, Position **1400, 587**.

---

## 6 · Continuity — protect these three

`Unit 4B` · `$1,450.00` · `Aug 12` are all callbacks:

| Frame | Where it appeared |
|---|---|
| F1 | Sticky note `call unit 4B`, spreadsheet row `$1,450.00`, chip `AUG 12` |
| F6 | Row 1 — `M. Alvarez · Unit 4B · $1,450.00 · Reminder sent` |
| **F9** | The notice that tenant actually receives |

One lease, tracked from "the chasing" through "handled" to "one tap to pay." It's the
strongest through-line in the film and it costs nothing to keep. Any copy pass that
changes a unit number or an amount in one frame has to change all three.

---

## 7 · Resting states

| Layer | Value |
|---|---|
| Every layer except `BG_PREVIEW` | Opacity 0 |
| `HEADLINE` animator **Amount** | 0% |

---

## 8 · Timing sheet

Seven seconds, against the VO:
*"Tenants get a clean, branded notice with the real invoice attached — and a Pay Now
button straight into your payment page."*

| Start | End | Layer | Move |
|---|---|---|---|
| 6 | 18 | `HEADLINE` | Rise + fade — **hero**, 12 frames |
| 24 | 30 | `HEADLINE` animator | Amount 0 → 100% — "one tap" turns indigo |
| 30 | 42 | `PHONE_FRAME` | Rise + fade from Y **+32**, 12 frames |
| 36 | 42 | `SUBLINE` | Rise + fade |
| 54 | 60 | `EMAIL_HEADER` | Fade in · *"a clean, branded notice"* |
| 60 | 66 | `SUBJECT` | Fade in |
| 66 | 72 | `BAR_1` | Fade in |
| 72 | 78 | `BAR_2` | Fade in |
| 78 | 84 | `BAR_3` | Fade in |
| **96** | **105** | `ATTACH_CHIP` | **Pop** · *"the real invoice attached"* |
| **120** | **129** | `BTN_PAY` | **Pop** · *"a Pay Now button"* |
| 138 | 144 | `CAPTION` | Fade in |
| 144 | 210 | — | **Hold** |

### Why the screen content only fades

Everything inside the phone uses **fade, no rise**. A notice assembling itself with things
sliding around reads as a UI being built; a notice that simply *is there*, element by
element, reads as an email arriving. The two pops are the exceptions, and they're the two
things the tenant actually acts on.

### Optional — the tap

The script says the button "flashes to the provider payment page." The approved mockup has
no payment-page state, so that beat isn't built. If you want to suggest it without a second
screen: on `BTN_PAY`, Scale **100 → 96 → 100** across frames 156 / 160 / 166, with the
`CAPTION` brightening to `#AEB6C4` over the same span. Reads as a press, needs no new
artwork.

---

## 9 · Motion recipes

### Rise + fade

| Layer | Position start | Position end |
|---|---|---|
| `HEADLINE` | 160, 486 | 160, 462 |
| `SUBLINE` | 160, 677 | 160, 653 |
| `PHONE_FRAME` | 1401, **593** | 1401, 561 |

`PHONE_FRAME` uses **+32** rather than +24 — same reasoning as F5's panel, a 936 px object
needs more travel than a text line to read as moving.

### Pop — `ATTACH_CHIP` and `BTN_PAY`

| Property | Offset | Value |
|---|---|---|
| Scale | 0 | 88% |
| Scale | +5 | **106%** |
| Scale | +9 | 100% |
| Opacity | 0 → +4 | 0 → 100% |

`ATTACH_CHIP` → keys at 96 / 101 / 105. `BTN_PAY` → 120 / 125 / 129.

### Easing

**Shift+F9** on the **last** keyframe of every entrance, Incoming Influence **80%**. First
keyframe linear. **F9** the middle keyframe of each pop. Plain fades need no easing.

---

## 10 · Polish

- **No drift on the phone.** The brand rules ask for parallax on product *mockups* — a
  screenshot of the real app, like F5. This phone is built artwork sitting still while
  content lands on it; drifting it would fight the content's timing.
- **Check the bezel at 100%.** 20 px all round. Uneven bezel is the one thing that makes a
  drawn phone look wrong, and it's invisible at 25%.
- **Motion blur off.** Nothing travels far.
- **Hold the last 66 frames.**

---

## 11 · Finishing

1. Drop `F9_CONTENT` into **MASTER** after `F8_CONTENT`
2. Confirm `BG_PREVIEW` is still a Guide Layer
3. **File ▸ Increment and Save**

---

## 12 · Notes for the final pass

- **F9 is in the 30 s cutdown at 4 s (120 frames)** — and `BTN_PAY` currently pops at
  120–129, so it would be cut mid-animation. The cut needs the chip pulled to ~78 and the
  button to ~96. Not in the 15 s teaser, so that's the only retime.
- **Rose must not return.** See the PDF badge note in §5. This is the first frame since F2
  where a red is even tempting.
- **Green does not appear in this frame** and shouldn't — F9 is about the tenant's action,
  not a resolved state. The next green is F10's `Notices paused`.
- **9:16 reframe** is straightforward and this frame wants it: phone centred, headline
  above it, sub-line below. The headline re-breaks at the narrower width, which is exactly
  why §3 uses word indices — the recolour survives the re-break with no rework.

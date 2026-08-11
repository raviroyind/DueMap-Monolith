# After Effects build guide — F10 "Human control" (0:58–1:06)

**1920×1080 · 30 fps · 8 s (240 frames)**. All coordinates are comp-space at 1920×1080
and match the approved mockup.

The film's second real capture, and its longest frame alongside F6. Headline top-left on
the 160 margin, `/today` capture bottom-left, modal and status chip as annotations on the
right.

---

## 0 · The capture — do this before opening AE

Handbook §9: run the seed, start the app on port 63055, sign in as
**`demo2@duemap.dev` / `DemoUser1!`**, go to **`/today`** in dark mode.

**Capture at 2× device pixel ratio** — DevTools ▸ Ctrl+Shift+M ▸ Responsive ▸ 1280 × 832
▸ ⋮ ▸ Add device pixel ratio ▸ **DPR 2** ▸ ⋮ ▸ Capture screenshot. Full method and the
reason not to substitute browser zoom is in `ae-build-F5-sync.md` §0.

The page should show **6 items need attention** across 4 types, which is what the seed
produces and what the mockup shows.

> **Check the surnames again, more lightly than F5.** The mockup has three Marchettis
> (Jamal, Nora, Devon) and two Devons (Halvorsen, Marchetti) in six rows. Not the
> all-Whitfield problem F5 had, but a viewer reading the queue will notice. Page or
> re-seed if it's easy; this one isn't a blocker.

Save as `assets/shot-today.png`.

**Never press Sync on this workspace.**

---

## 1 · Comp setup

1. **Ctrl+N** → `F10_CONTENT`, 1920 × 1080, 30 fps, duration **`0:00:08:00`** (240 frames)
2. **Ctrl+click the time display** — work in frames
3. Solid `#16191F` named `BG_PREVIEW` at the bottom → **Layer ▸ Guide Layer**
4. Make a **`Frame-10/`** folder in the Project panel

**No sub-line in this frame.** The script gives F10 a headline only — the three-word
cadence *Logged. Paused. Tracked.* is doing the sub-line's job.

---

## 2 · Layer stack

```
F10_CONTENT
├── CHIP_PAUSED          (precomp)
├── MODAL                (precomp — body, title, label, input, two buttons)
├── SHOT_TODAY           (precomp — 910 × 592, capture + matte + border)
├── HEADLINE
└── BG_PREVIEW           (Guide Layer)
```

Four layers. **Both the modal and the chip are annotations, not part of the capture** —
they're drawn in AE at roughly 2× the app's own UI scale. A real in-app modal captured at
this panel size would be unreadable at 1080p, and the point of the frame is that you can
read what the PM typed.

---

## 3 · Headline

`"I'll pay Friday." Logged. Paused. ⏎ Tracked.`

**Inter Tight ExtraBold (800) 96 px**, **Leading 100**, Tracking −20, `#F0F2F7`,
**left aligned**, **Anchor Point 0, 0**, Position **160, 179**.

Baselines land at 179 and 279. Line 1 ends around x 1645 — inside the 1760 margin, but
it's the longest headline in the film, so check it before moving on.

> The mockup renders this one noticeably under 96 px — closer to 83 — because it's long.
> 96 still fits, and a headline that's 96 in eight frames and 83 in one is visible when
> they play in sequence. Keep 96 unless it crowds; if it does, drop the whole film's
> headlines rather than this one alone.

### Recolouring the quote

**Words work here**, including the punctuation:

1. **Text ▸ Animate ▸ Fill Color ▸ RGB**
2. `Range Selector 1 ▸ Advanced` → **Based On → Words**, *then* **Units → Index**
3. **Start 0**, **End 3**
4. Animator **Fill Color** → `#727CF5`
5. Leave **Amount** at 0% — keyframed in §7

`"I'll`(0) `pay`(1) `Friday."`(2) `Logged.`(3) `Paused.`(4) `Tracked.`(5) — the opening
quote rides on word 0 and the closing quote plus period on word 2, so the whole spoken
phrase including its quotation marks selects cleanly. This is the F7 case in reverse:
there, punctuation had to be *excluded* and needed characters; here it belongs to the
phrase and words handle it.

---

## 4 · The Today capture

### SHOT_TODAY (precomp)

New comp at **910 × 592** — *artwork size, not 1920 × 1080*.

| Layer | Spec | Position |
|---|---|---|
| `SHOT_FRAME` | Rectangle 910 × 592, R14, **no Fill**, Stroke `#333A49` 1.5 px | 455, 296 |
| `SHOT_MASK` | Rectangle 910 × 592, R14, **Fill white** | 455, 296 |
| `SHOT_TODAY_PNG` | the capture, scaled to fit | 455, 296 |

Set `SHOT_TODAY_PNG`'s **Track Matte → Alpha Matte "SHOT_MASK"**.

> **`SHOT_MASK` needs an actual Fill.** `Add ▸ Rectangle` alone draws nothing, and a shape
> with no alpha used as an Alpha Matte hides everything below it. This is what went wrong
> in F5.

### Placing it

| | Anchor Point | Position |
|---|---|---|
| `SHOT_TODAY` | **455, 296** *(automatic)* | 615, 648 |

Building the comp at artwork size rather than 1920 × 1080 means AE's default anchor is
already the artwork centre — nothing to set by hand. F5's `SHOT_PANEL` was made full-frame
and needed a manual anchor; this is the better pattern, use it for F11 and F12 too.

Add **Layer ▸ Layer Styles ▸ Drop Shadow** → Opacity 50%, Distance 12, Size 40, Angle 120°.

Panel spans x 160 → 1070, y 352 → 944. Left edge on the margin.

---

## 5 · The modal

Build all nine layers at the coordinates below, select them, then **Ctrl+Shift+C** →
`MODAL`, **Move all attributes into the new composition**.

**The comp will be 1920 × 1080** — Ctrl+Shift+C inherits the parent's dimensions and
doesn't offer a size. That's why the placement table at the end of this section sets the
anchor by hand, unlike `SHOT_TODAY` in §4, which was created with Ctrl+N at artwork size
and needs no anchor work.

Everything sits on a shared vertical centre of **648** with the capture panel.

| Layer | Spec | Position |
|---|---|---|
| `MODAL_BODY` | Rectangle **630 × 340**, Roundness 16, Fill `#232734`, Stroke `#333A49` 1 px | 1390, 648 |
| `MODAL_TITLE` | `Log promise` — Inter SemiBold 32, `#F0F2F7`, left aligned | 1110, 551 |
| `MODAL_LABEL` | `Promised date` — Inter Medium 22, `#AEB6C4`, left aligned | 1110, 603 |
| `INPUT_BOX` | Rectangle **558 × 60**, Roundness 10, Fill `#16191F`, Stroke `#333A49` 1 px | 1389, 654 |
| `INPUT_TEXT` | `Fri, Aug 15` — Inter SemiBold 28, `#F0F2F7`, left aligned | 1136, 664 |
| `BTN_SAVE` | Rectangle **406 × 59**, Roundness 10, Fill `#727CF5` | 1313, 745 |
| `BTN_SAVE_TEXT` | `Save promise` — Inter SemiBold 26, `#FFFFFF`, centre + **Ctrl+Alt+Home** | 1313, 745 |
| `BTN_CANCEL` | Rectangle **139 × 59**, Roundness 10, Fill `#2B3040` | 1598, 745 |
| `BTN_CANCEL_TEXT` | `Cancel` — Inter SemiBold 26, `#F0F2F7`, centre + **Ctrl+Alt+Home** | 1598, 745 |

| | Anchor Point | Position |
|---|---|---|
| `MODAL` | 1390, 648 | **1365**, 648 |

**Anchor and Position differ here, deliberately.** The nine layers were built at 1390, so
that's where the anchor has to point; Position 1365 then slides the whole thing 25 px left
so its left edge lands at **1050 — 20 px over** the capture panel's right edge at 1070.
Changing the anchor to match would cancel the shift. The 25 px pivot offset costs ~2.5 px
of drift across the 92 → 103% pop, which is invisible.

Modal spans x 1050 → 1680. Nothing aligns to its right edge, so losing 25 px there is free.

Add **Layer ▸ Layer Styles ▸ Drop Shadow** → Opacity **55%**, Distance **10**, Size **44**,
Angle 120°.

> **The overlap needs the shadow to work.** Two panels meeting edge-to-edge read as a
> collision; overlapping *with* a shadow reads as depth, which is what makes the modal an
> annotation sitting on the app rather than a second window beside it. The shadow is
> deliberately softer and larger than the capture panel's (50% / 12 / 40) so the modal
> reads as the nearer object.

**Text positioned by baseline keeps Anchor 0, 0.** Only the two button labels are centred
in containers, so only those get Ctrl+Alt+Home.

---

## 6 · CHIP_PAUSED

```
CHIP_PAUSED         ← the precomp
├── PAUSED_TEXT
├── PAUSED_DOT
└── PAUSED_BODY
```

| Layer | Spec | Position |
|---|---|---|
| `PAUSED_BODY` | Rectangle **287 × 58**, **Roundness 29** (full pill), Fill `#232734`, Stroke `#727CF5` 1.5 px | 1564, 905 |
| `PAUSED_DOT` | Ellipse **14 × 14**, Fill `#727CF5` | 1457, 904 |
| `PAUSED_TEXT` | `Notices paused` — Inter SemiBold 28, `#727CF5`, left aligned, Anchor 0, 0 | 1484, 915 |

| | Anchor Point | Position |
|---|---|---|
| `CHIP_PAUSED` | 1564, 905 | 1564, 905 |

> **This chip is indigo, not green — and that's right.** The checklist has the film's
> colour arc turning green from F10 onward, so the instinct is to make this the first green
> chip. But *paused* isn't *resolved*. Indigo is the control colour, and "human control" is
> literally the frame's title. The green ramp is atmospheric and lives in MASTER; it
> doesn't oblige every chip from here on to turn green.

---

## 7 · Resting states

| Layer | Value |
|---|---|
| `HEADLINE` `SHOT_TODAY` `MODAL` `CHIP_PAUSED` | Opacity 0 |
| `HEADLINE` animator **Amount** | 0% |
| Every layer inside `MODAL` | Opacity 0 |

The modal's contents rest at 0 as well as the precomp itself — they stagger in
independently in §8.

---

## 8 · Timing sheet

Eight seconds, against the VO:
*"When a tenant says 'I'll pay Friday,' log it once. DueMap pauses the notices — and
resumes automatically if the promise breaks."*

| Start | End | Layer | Move |
|---|---|---|---|
| 6 | 18 | `HEADLINE` | Rise + fade — **hero**, 12 frames |
| **24** | **30** | `HEADLINE` animator | Amount 0 → 100% · *lands on the spoken quote* |
| 36 | 48 | `SHOT_TODAY` | Rise + fade from Y **+32** |
| 66 | 78 | `MODAL` | Pop — Scale 92 → 103 → 100 · *"log it once"* |
| 78 | 84 | `MODAL_TITLE` | Fade in |
| 84 | 90 | `MODAL_LABEL` | Fade in |
| 90 | 99 | `INPUT_BOX` + `INPUT_TEXT` | Pop — the date lands |
| 102 | 108 | `BTN_SAVE` `BTN_CANCEL` + labels | Fade in |
| 126 | 132 | `BTN_SAVE` | **Press** — Scale 100 → 96 → 100 (126 / 129 / 132) |
| **138** | **147** | `CHIP_PAUSED` | Pop · *"DueMap pauses the notices"* |
| 150 | 240 | `PAUSED_DOT` | Opacity pulse, looped |
| 147 | 240 | — | Hold |

### The three beats

1. **The quote turns indigo at 24** as the VO speaks it — the tenant's words.
2. **The save press at 126** is the PM's single action. It's the only "click" in the whole
   film, and the frame is about that click being the *only* thing a human has to do.
3. **The chip at 138** is the consequence. The gap between press and chip is what makes it
   read as caused rather than coincident — same six-frame logic as F6's clock.

### The dot pulse

Inside `CHIP_PAUSED`, on `PAUSED_DOT` Opacity: **150** → 100%, **162** → 55%,
**174** → 100%. **F9** all three, then Alt+click the stopwatch and add:

```
loopOut()
```

That carries it to 240. It's covering the VO's last phrase — *"and resumes automatically if
the promise breaks"* — which has no other visual. A pulsing dot is the only honest way to
say "this is still running" without inventing UI the product doesn't have.

---

## 9 · Motion recipes

### Rise + fade

| Layer | Position start | Position end |
|---|---|---|
| `HEADLINE` | 160, 203 | 160, 179 |
| `SHOT_TODAY` | 615, **680** | 615, 648 |

### Modal entrance — softer pop

| Property | Frame | Value |
|---|---|---|
| Scale | 66 | 92% |
| Scale | 72 | **103%** |
| Scale | 78 | 100% |
| Opacity | 66 → 72 | 0 → 100% |

**103%, not 106%.** A 630 × 340 panel overshooting by 6% travels ~38 px at its corners and
reads as a bounce. Small chips can take the full overshoot; panels can't.

### Pop — `INPUT_BOX` and `CHIP_PAUSED`

Standard: Scale **88 → 106 → 100** at 0 / +5 / +9, Opacity 0 → 100 over the first 4.

### Press — `BTN_SAVE`

Scale **100 → 96 → 100** at frames 126 / 129 / 132. No opacity change. Ease the middle
keyframe only.

### Easing

**Shift+F9** on the **last** keyframe of every entrance, Incoming Influence **80%**. First
keyframe linear. **F9** the middle keyframe of pops and the press. Plain fades need none.

---

## 10 · Polish

- **No drift on the capture.** The brand rules ask for parallax on product mockups, but
  this frame has three stacked annotations keyed to it; drifting the panel underneath makes
  their relationship look loose.
- **Check the capture at 100%.** If the `/today` item text is soft, the capture was 1×.
- **Motion blur off.**
- **Hold from 147** — only the dot moves after that.

---

## 11 · Finishing

1. Drop `F10_CONTENT` into **MASTER** after `F9_CONTENT`
2. Confirm `BG_PREVIEW` is still a Guide Layer
3. **File ▸ Increment and Save**

---

## 12 · Notes for the final pass

- **The green atmospheric ramp starts here.** Per the checklist, MASTER's colour arc
  shifts toward `#0ACF97` from F10 onward — a new glow layer, same technique as the indigo
  bloom at F3. That's a MASTER-level change, not something this frame comp carries.
- **F10 is in neither cutdown**, so no trim constraint and no 9:16 version needed.
- **`Fri, Aug 15` is consistent with the film's dates** — F1 and F9 use Aug 12, F6 (with
  the optional fix) anchors to Aug 5. A promise made for the 15th after a notice on the
  12th is the right sequence. Keep it that way if any date changes.
- **This frame does not reframe to 9:16** — real-capture frames need content substitution
  rather than cropping, and the modal-beside-panel layout has nowhere to go in a vertical
  format. Moot unless F10 gets promoted into a cutdown.

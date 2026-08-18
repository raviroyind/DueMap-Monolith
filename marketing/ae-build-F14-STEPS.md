# F14 "CTA end card" — build steps

**1920 × 1080 · 30 fps · 8 s (240 frames).** Centred frame — everything on x 960.
Final ~2 s is a pure hold.

> **Blocker:** confirm the public domain before rendering. `duemap.dev` is what dev config
> says; the guardrails require checking it. Section F.

> **The background glow is not built here.** The soft green bloom in the mockup is MASTER's
> colour-arc resolution, per `ae-final-pass-checklist.md` §3. `F14_CONTENT` is content on
> transparency — building a glow into it would double up.

---

## A · Comp

1. **Ctrl+N** → name `F14_CONTENT`, **1920 × 1080**, **30 fps**, duration **0:00:08:00**
2. **Ctrl+click the time display** to work in frames
3. **Ctrl+Y** → 1920 × 1080 solid, colour `#16191F`, name `BG_PREVIEW`
4. Send it to the bottom → **Layer ▸ Guide Layer**
5. Project panel → new folder `Frame-14`

---

## B · Spark

1. Drag `assets/duemap-spark.svg` into the timeline, name it `SPARK`
2. Press **S** → Scale **10%**, then adjust until it measures about **78 px** tall
3. **Ctrl+Alt+Home** → **Layer ▸ Transform ▸ Center Anchor Point in Layer Content**
4. Press **P** → Position **960, 289**

---

## C · Headlines

Both **Inter Tight ExtraBold 96**, Tracking **−20**, **centre aligned**. Do **not**
Ctrl+Alt+Home — they're positioned by baseline.

1. Type `Stop chasing rent.` — colour **`#7E899B`**, Position **960, 468**.
   Name it `HEADLINE_1`.
2. Type `Start closing days.` — colour `#F0F2F7`, Position **960, 581**.
   Name it `HEADLINE_2`.

`HEADLINE_1` is muted from the start — it never appears white. Line 2 is the message; line
1 is the thing being retired.

Now the green word:

3. On `HEADLINE_2`: **Text ▸ Animate ▸ Fill Color ▸ RGB**
4. `Range Selector 1 ▸ Advanced` → **Based On → Words**, then **Units → Index**
5. **Start 1**, **End 2**
6. Set the animator's **Fill Color** to **`#0ACF97`**
7. Set **Amount** to **0%**

Words index from zero: `Start`(0) `closing`(1) `days.`(2). Words work here because the
highlight is a whole word with no punctuation attached.

---

## D · Strikethrough

1. **Layer ▸ New ▸ Shape Layer**, name `STRIKE`
2. **Pen tool (G)** — click two points roughly horizontal, about **786 px** apart
3. **Add ▸ Stroke** → `#7E899B`, Width **3**, **Line Cap Round**. No Fill.
4. **Add ▸ Trim Paths** → set **End** to **0%**
5. Press **V** → **Layer ▸ Transform ▸ Center Anchor Point in Layer Content**
6. **Rotation 0**, Position **960, 433**
7. Adjust **Scale** until the line spans the full width of `Stop chasing rent.` —
   x 564 to 1350
8. Put `STRIKE` directly **above** `HEADLINE_1`

Layer Opacity stays at **100** — Trim Paths does the reveal.

---

## E · CTA button

1. **Layer ▸ New ▸ Shape Layer**, name `BTN_BODY`
2. **Add ▸ Rectangle** → Size **648, 92**, **Roundness 14**
3. **Add ▸ Fill** → **`#727CF5`**
4. Layer Position **960, 705**
5. Type `Connect QuickBooks or Xero →` — Inter SemiBold **36**, `#FFFFFF`,
   **centre aligned** + **Ctrl+Alt+Home**, Position **960, 705**
6. Select both → **Ctrl+Shift+C** → name `BTN_CTA`
7. On the `BTN_CTA` layer: Anchor Point **960, 705**, Position **960, 705**
8. **Layer ▸ Layer Styles ▸ Outer Glow** → Colour `#727CF5`, Opacity **0**, Size **45**

The arrow is a glyph in the same string, not a separate layer.

---

## F · URL

Type the domain — Inter Medium **32**, `#7E899B`, **centre aligned**, Position **960, 836**.
Name it `URL`.

**Confirm the public domain first.** If it isn't settled, build the layer with the text you
expect and note it — but don't render the master until it's confirmed. This is the one
string in the film that a viewer will type.

---

## G · Resting states

Set every layer to **Opacity 0** except `BG_PREVIEW` and `STRIKE`.

Also:

- `HEADLINE_2` animator **Amount** = 0%
- `STRIKE` **Trim Paths ▸ End** = 0%, layer Opacity **100**
- `BTN_CTA` **Outer Glow ▸ Opacity** = 0%

---

## H · Timing

VO: *"DueMap. Rent follow-ups on autopilot. Connect your books — and let the chasing stop."*

| Layer | Property | Frames | Value |
|---|---|---|---|
| `SPARK` | Scale | 6 / 14 / 20 | 0 / **12%** / 10% |
| `SPARK` | Rotation | 6 → 20 | **−90°** → 0° |
| `SPARK` | Opacity | 6 → 14 | 0 → 100 |
| `HEADLINE_1` | Position | 30 → 42 | 960, 492 → **960, 468** |
| `HEADLINE_1` | Opacity | 30 → 42 | 0 → 100 |
| `HEADLINE_2` | Position | 54 → 66 | 960, 605 → **960, 581** |
| `HEADLINE_2` | Opacity | 54 → 66 | 0 → 100 |
| `HEADLINE_2` animator | Amount | 72 → 78 | 0 → **100%** |
| `BTN_CTA` | Scale | 96 / 101 / 105 | 88 / **106** / 100 |
| `BTN_CTA` | Opacity | 96 → 100 | 0 → 100 |
| `BTN_CTA` Outer Glow | Opacity | 96 → 108 | 0 → **40%** |
| `URL` | Opacity | 120 → 126 | 0 → 100 |
| `STRIKE` Trim | End | **144 → 154** | 0 → **100%** |
| — | — | 154 → 240 | **Hold** |

The strike lands last, on *"and let the chasing stop"* — the line being struck out is
literally what the VO is saying. It's the film's closing gesture, so don't move it earlier
to fill the gap at 108–144.

---

## I · Easing

1. Select every **last** keyframe → **Shift+F9**
2. Right-click → **Keyframe Velocity** → Incoming Influence **80**
3. Leave every **first** keyframe linear
4. On the `SPARK` and `BTN_CTA` Scale pops, also press **F9** on the **middle** keyframe

---

## J · Check

1. If `STRIKE` draws from the wrong end, leave **End** at 100% and animate **Start**
   100% → 0% instead
2. At frame 200, all three centred elements — spark, headlines, button, URL — should share
   one vertical axis. Set the Composition panel to 25% and check nothing is off by a pixel
3. Confirm the last **60 frames** have no keyframes at all — that's the 2 s end-card hold
   the editor needs

---

## K · Finish

1. Drop `F14_CONTENT` into `MASTER` after `F13_CONTENT`
2. Confirm `BG_PREVIEW` still shows the Guide Layer icon
3. **File ▸ Increment and Save**

---

## L · After F14

All fourteen frames exist. What's outstanding is in `ae-final-pass-checklist.md`:

- Transition screens between frames
- The colour arc in MASTER — rose retiring at F3, indigo blooming, **green resolving from
  F10**, which is what makes this frame's background
- Undoing the F1 → F2 crossover keyframes
- Audio, then the cutdowns

**F14 is in both cutdowns** — 5 s in the 30 s ad, 4 s in the 15 s teaser. Everything here
lands by frame 154, so a 5 s trim (150 frames) just clips the strike; pull it to 132 → 142
for that cut. The 4 s version needs the URL at 96 and the strike at 108.

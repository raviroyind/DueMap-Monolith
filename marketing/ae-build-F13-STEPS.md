# F13 "Setup" — build steps

**1920 × 1080 · 30 fps · 6 s (180 frames).** Centred frame — everything sits on x 960.

> **Blocker:** the proof-point line is a placeholder. Fill it with measured data or cut it
> before rendering. Steps for both are in section G.

---

## A · Comp

1. **Ctrl+N** → name `F13_CONTENT`, **1920 × 1080**, **30 fps**, duration **0:00:06:00**
2. **Ctrl+click the time display** to work in frames
3. **Ctrl+Y** → 1920 × 1080 solid, colour `#16191F`, name `BG_PREVIEW`
4. Send it to the bottom → **Layer ▸ Guide Layer**
5. Project panel → new folder `Frame-13`, drag `F13_CONTENT` in

---

## B · Text

1. Type `SETUP` — Inter SemiBold **28**, Tracking **+80**, `#7E899B`,
   **centre aligned**, Position **960, 323**
2. Type `Live in three steps.` — Inter Tight ExtraBold **96**, Tracking **−20**,
   `#F0F2F7`, **centre aligned**, Position **960, 434**

Do **not** Ctrl+Alt+Home either — both are positioned by baseline, and centre alignment
already puts them on x 960.

3. On the headline: **Text ▸ Animate ▸ Fill Color ▸ RGB**
4. `Range Selector 1 ▸ Advanced` → **Units → Index** (leave Based On at Characters)
5. **Start 8**, **End 19**
6. Set the animator's **Fill Color** to `#727CF5`
7. Set **Amount** to **0%**

---

## C · Step 1 circle

1. **Layer ▸ New ▸ Shape Layer**, name `CIRCLE_1`
2. **Add ▸ Ellipse** → Size **94, 94**
3. **Add ▸ Fill** → `#0ACF97`, then set **Fill 1 ▸ Opacity** to **15%**
4. **Add ▸ Stroke** → `#0ACF97`, Width **2**
5. Layer Position **483, 571**

Now the check:

6. **Layer ▸ New ▸ Shape Layer**, name `CHECK_1`
7. **Pen tool (G)** — click three points making a tick about **26 px wide, 20 px tall**.
   Placement doesn't matter, step 10 fixes it.
8. **Add ▸ Stroke** → `#0ACF97`, Width **5**, **Line Cap Round**, **Line Join Round**.
   No Fill.
9. **Add ▸ Trim Paths**
10. Press **V** → **Layer ▸ Transform ▸ Center Anchor Point in Layer Content** →
    Position **483, 571**
11. Set **Trim Paths ▸ End** to **0%**
12. Select `CIRCLE_1` and `CHECK_1` → **Ctrl+Shift+C** → name `STEP_1`
13. On the `STEP_1` layer: Anchor Point **483, 571**, Position **483, 571**

---

## D · Steps 2 and 3

1. Project panel → select `STEP_1` → **Ctrl+D** → rename `STEP_2`
2. Drag `STEP_2` into `F13_CONTENT`
3. Set Anchor Point **483, 571** and Position **960, 571**

For step 3, build it fresh — it has a number, not a check:

4. **Layer ▸ New ▸ Shape Layer**, name `CIRCLE_3`
5. **Add ▸ Ellipse** → Size **94, 94**
6. **Add ▸ Fill** → `#727CF5`, **Fill 1 ▸ Opacity 15%**
7. **Add ▸ Stroke** → `#727CF5`, Width **2**
8. Position **1437, 571**
9. Type `3` — Inter Tight ExtraBold **38**, `#727CF5`, **centre aligned** +
   **Ctrl+Alt+Home**, Position **1437, 571**
10. Select both → **Ctrl+Shift+C** → name `STEP_3`
11. Anchor Point **1437, 571**, Position **1437, 571**

---

## E · Connector lines

1. **Layer ▸ New ▸ Shape Layer**, name `LINE_1`
2. **Pen tool (G)** — click two points roughly horizontal, about **160 px** apart
3. **Add ▸ Stroke** → `#0ACF97`, Width **2**, **Line Cap Round**. No Fill.
4. **Add ▸ Trim Paths** → set **End** to **0%**
5. Press **V** → **Layer ▸ Transform ▸ Center Anchor Point in Layer Content**
6. **Rotation 0**, Position **721, 571**
7. Adjust **Scale** until the line is 160 px wide
8. **Ctrl+D** → rename `LINE_2`
9. On `LINE_2`: change the Stroke colour to **`#333A49`**, Position **1198, 571**
10. Drag both line layers **below** the three `STEP_` layers

---

## F · Labels

Three text layers, **Inter SemiBold 28**, `#F0F2F7`, **centre aligned**, baseline **664**.
Do not Ctrl+Alt+Home.

| Text | Position |
|---|---|
| `1 · Connect` | 483, 664 |
| `2 · Set your rules` | 960, 664 |
| `3 · Go live` | 1437, 664 |

---

## G · Proof point — pick one

**If you have measured data:** type it as **Inter Medium Italic 32**, `#7E899B`,
**centre aligned**, Position **960, 774**. Name the layer `PROOF`.

**If you don't:** skip this section entirely. Don't build the placeholder — a rendered
frame containing `[proof point — …]` is the kind of thing that ships by accident. Nothing
else in the layout moves.

---

## H · Resting states

Set every layer to **Opacity 0** except `BG_PREVIEW`.

Also confirm:

- `HEADLINE` animator **Amount** = 0%
- `CHECK_1` and `CHECK_2` **Trim Paths ▸ End** = 0% (inside `STEP_1` / `STEP_2`)
- `LINE_1` and `LINE_2` **Trim Paths ▸ End** = 0%

`LINE_1` and `LINE_2` layer Opacity stays at **100** — Trim Paths does their reveal.

---

## I · Timing

VO: *"Setup is three steps: connect your books, set your rules, go live."*

| Layer | Property | Frames | Value |
|---|---|---|---|
| `EYEBROW` | Position | 6 → 12 | 960, 347 → **960, 323** |
| `EYEBROW` | Opacity | 6 → 12 | 0 → 100 |
| `HEADLINE` | Position | 12 → 24 | 960, 458 → **960, 434** |
| `HEADLINE` | Opacity | 12 → 24 | 0 → 100 |
| `HEADLINE` animator | Amount | 30 → 36 | 0 → **100%** |
| `STEP_1` | Scale | 54 / 59 / 63 | 88 / **106** / 100 |
| `STEP_1` | Opacity | 54 → 58 | 0 → 100 |
| `CHECK_1` Trim | End | 60 → 65 | 0 → **100%** |
| `LABEL_1` | Opacity | 60 → 66 | 0 → 100 |
| `LINE_1` Trim | End | 72 → 82 | 0 → **100%** |
| `STEP_2` | Scale | 90 / 95 / 99 | 88 / **106** / 100 |
| `STEP_2` | Opacity | 90 → 94 | 0 → 100 |
| `CHECK_2` Trim | End | 96 → 101 | 0 → **100%** |
| `LABEL_2` | Opacity | 96 → 102 | 0 → 100 |
| `LINE_2` Trim | End | 108 → 118 | 0 → **100%** |
| `STEP_3` | Scale | 123 / 128 / 132 | 88 / **106** / 100 |
| `STEP_3` | Opacity | 123 → 127 | 0 → 100 |
| `LABEL_3` | Opacity | 129 → 135 | 0 → 100 |
| `PROOF` | Opacity | 144 → 150 | 0 → 100 |

Nothing moves after 150.

The `CHECK_` and `LABEL_` keyframes for steps 1 and 2 go **inside** `STEP_1` / `STEP_2`
for the checks, and in `F13_CONTENT` for the labels. Frame numbers map 1:1.

---

## J · Easing

1. Select every **last** keyframe → **Shift+F9**
2. Right-click them → **Keyframe Velocity** → Incoming Influence **80**
3. Leave every **first** keyframe linear
4. On the three Scale pops, also press **F9** on the **middle** keyframe

Plain Opacity fades need no easing.

---

## K · Check

1. If a line or check draws from the wrong end, leave **End** at 100% and animate
   **Start** from 100% → 0% instead
2. Set the Composition panel to 25% — the three circles should read as an even row
3. Confirm circle centres are **483 / 960 / 1437**, evenly spaced 477 apart

---

## L · Finish

1. Drop `F13_CONTENT` into `MASTER` after `F12_CONTENT`
2. Confirm `BG_PREVIEW` still shows the Guide Layer icon
3. **File ▸ Increment and Save**

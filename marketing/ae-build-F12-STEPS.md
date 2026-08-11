# F12 — build steps

Do these in order. Numbers are exact unless marked *align*, which means nudge it against
`REF_CAPTURE` until it matches.

---

## A · Capture

1. Start the app, sign in as `demo2@duemap.dev` / `DemoUser1!`
2. Go to `http://localhost:63055/` in Chrome, dark mode
3. **F12** → **Ctrl+Shift+M** → device dropdown → **Responsive**
4. Width **1280**, height **832**
5. **⋮** → **Add device pixel ratio** → set **DPR 2**
6. **⋮** → **Capture screenshot**
7. Save to `assets/shot-dashboard.png`
8. Note down from the screen: the 12 bar values, and the 3 donut percentages

---

## B · Comps

1. **Ctrl+N** → name `F12_CONTENT`, **1920 × 1080**, **30 fps**, duration **0:00:06:00**
2. **Ctrl+N** → name `SHOT_DASH`, **1116 × 704**, **30 fps**, duration **0:00:06:00**
3. In the Project panel, make a folder `Frame-12`, drag both comps into it
4. **Ctrl+I** → import `shot-dashboard.png`

---

## C · SHOT_DASH — reference and base

Open `SHOT_DASH`.

1. Drag `shot-dashboard.png` into the timeline
2. Rename it `REF_CAPTURE`
3. Press **S** → Scale **43.6**
4. Press **P** → Position **558, 352**
5. Nudge Position Y until the top of the app touches y 0
6. With it selected: **Layer ▸ Guide Layer**
7. Keep it as layer 1 (top) for the whole build

Now the background:

8. **Ctrl+Y** → 1116 × 704 solid, name `PANEL_BG`
9. Set its colour with the eyedropper, picking the page background from `REF_CAPTURE`
10. Position **558, 352**
11. Drag `PANEL_BG` to the **bottom** of the stack

---

## D · SHOT_DASH — chrome

1. **Layer ▸ New ▸ Shape Layer**, name `CHROME_MATTE`
2. **Add ▸ Rectangle** → in **Contents ▸ Rectangle Path 1**:
   Size **1116, 70** · **path** Position **558, 35**
3. **Add ▸ Rectangle** again → in **Contents ▸ Rectangle Path 2**:
   Size **204, 636** · **path** Position **102, 386**
4. **Add ▸ Fill** → white
5. On the **layer** (not the paths): Anchor Point **0, 0** and Position **0, 0**

> These two go in the **path** Position, not the layer Position — one layer can't carry two
> rectangles at different places. That means the layer transform must be zeroed or the two
> coordinates add together. Only this layer works that way; every other shape in F12 keeps
> its path at 0,0 and puts the coordinate on the layer.
>
> Without the Fill in step 4 the shape has no alpha, and as an Alpha Matte it hides
> `CHROME` completely.
5. Drag `shot-dashboard.png` into the timeline again
6. Rename it `CHROME`
7. Scale **43.6**, Position **558, 352** — same as `REF_CAPTURE`
8. Put `CHROME` directly **above** `CHROME_MATTE`
9. On `CHROME`, set **Track Matte → Alpha Matte "CHROME_MATTE"**

Check: you should see the top bar and left menu only.

---

## E · SHOT_DASH — header row

Build these directly in `SHOT_DASH`, then precompose.

1. Type `Portfolio overview` — Inter SemiBold **20**, `#F0F2F7`, left aligned,
   Position **233, 112** *align*
2. Shape Layer → Rectangle **112 × 19**, Roundness 10, Fill `#0ACF97` at **Fill Opacity
   15%**, Stroke `#0ACF97` 1 px → Position **289, 132** *align*
3. Type `Xero connected` — Inter SemiBold **10**, `#0ACF97`, centred + **Ctrl+Alt+Home**,
   Position **293, 135** *align*
4. Ellipse **5 × 5**, Fill `#0ACF97`, Position **243, 132** *align*
5. Type `Northgate Residential` — Inter SemiBold **12**, `#F0F2F7`, Position **354, 137** *align*
6. Type `last sync Aug 5, 2026 · 4:16 AM CDT` — Inter Medium **11**, `#7E899B`,
   Position **476, 137** *align*
7. Shape Layer, name `BTN_TOUR_BG` → Rectangle **110 × 26**, Roundness 6,
   Fill `#2B3040` → Position **872, 131**
8. Type `Take a tour` — Inter Medium **11**, `#F0F2F7`, centred + **Ctrl+Alt+Home**,
   Position **872, 135**
9. Shape Layer, name `BTN_CONN_BG` → Rectangle **135 × 26**, Roundness 6,
   Fill `#2B3040` → Position **1004, 131**
10. Type `Manage connection ›` — Inter Medium **11**, `#F0F2F7`, centred + **Ctrl+Alt+Home**,
    Position **1004, 135**

> Both buttons end at x **1072**, the same right edge as the donut card below. `Manage
> connection` spans 937 → 1072, `Take a tour` spans 817 → 927.
>
> **Check the Fill on every shape you create in E–I.** `Add ▸ Rectangle` uses whatever
> colour is in the toolbar's Fill swatch, which changes whenever you use the eyedropper.
> Set **Contents ▸ Fill 1 ▸ Color** explicitly each time.
11. Select layers 1–10 → **Ctrl+Shift+C** → name `HDR_ROW`
12. On the `HDR_ROW` layer: Anchor Point **558, 352**, Position **558, 352**

---

## F · SHOT_DASH — stat tiles

Build tile 1 only.

1. Shape Layer → Rectangle **195 × 98**, **Roundness 8**, Fill `#232734`,
   Stroke `#333A49` 1 px → Position **336, 213**
2. Type `CUSTOMERS` — Inter SemiBold **11**, Tracking **+80**, `#7E899B`, left aligned,
   Position **249, 187** *align*
3. Type `48` — Inter Tight ExtraBold **28**, `#F0F2F7`, left aligned,
   Position **249, 224** *align*
4. Type `37 linked to leases` — Inter Medium **11**, `#7E899B`, left aligned,
   Position **249, 244** *align*
5. Select all four → **Ctrl+Shift+C** → name `TILE_1`
6. On the layer: Anchor Point **336, 213**, Position **336, 213**

Now the other three:

7. In the **Project panel**, select `TILE_1` → **Ctrl+D** three times
8. Rename the copies `TILE_2`, `TILE_3`, `TILE_4`
9. Open each and edit its three text layers:

| Comp | Label | Value | Sub-line |
|---|---|---|---|
| `TILE_2` | `OPEN INVOICES` | `25` | `$51,750.00 outstanding` |
| `TILE_3` | `OVERDUE` | `17` | `may trigger a late-fee warning` |
| `TILE_4` | `ACTIVE LEASES` | `37` | `in your portfolio` |

10. In `TILE_3`, set the value's colour to **`#FA5C7C`**
11. Drag `TILE_2`, `TILE_3`, `TILE_4` into `SHOT_DASH`
12. Set all three: **Anchor Point 336, 213**
13. Set Positions: `TILE_2` **547, 213** · `TILE_3` **758, 213** · `TILE_4` **969, 213**

---

## G · SHOT_DASH — bar chart

1. **Ctrl+N** → name `CARD_BARS`, **497 × 272**, 30 fps, 6 s

Inside `CARD_BARS`:

2. Shape Layer → Rectangle **497 × 272**, Roundness 12, Fill `#232734`,
   Stroke `#333A49` 1 px → Position **248, 136**
3. Type `Collections — last 6 months` — Inter SemiBold **14**, `#F0F2F7`, Position **16, 28** *align*
4. Type `Invoiced vs collected, by invoice due month.` — Inter Medium **11**, `#7E899B`,
   Position **16, 45** *align*
5. Rectangle **8 × 8**, Fill `#727CF5`, Position **361, 69** *align*
6. Type `Invoiced` — Inter Medium **10**, `#F0F2F7`, Position **372, 72** *align*
7. Rectangle **8 × 8**, Fill `#0ACF97`, Position **420, 69** *align*
8. Type `Collected` — Inter Medium **10**, `#F0F2F7`, Position **431, 72** *align*
9. Five text layers, Inter Medium **10**, `#7E899B`, **right aligned**, all at x **82**:

| Text | Y |
|---|---|
| `$80,000` | 100 |
| `$60,000` | 130 |
| `$40,000` | 160 |
| `$20,000` | 189 |
| `$0` | 219 |

10. Six text layers, Inter Medium **10**, `#7E899B`, centred + **Ctrl+Alt+Home**, all at y **233**:

| Text | X |
|---|---|
| `Mar` | 102 |
| `Apr` | 170 |
| `May` | 237 |
| `Jun` | 305 |
| `Jul` | 373 |
| `Aug` | 440 |

Now the bars. For each of the 12:

11. Work out its height: **`height = (value ÷ 80000) × 119`**
12. Shape Layer → Rectangle **16 × height**, Fill `#727CF5` (Invoiced) or `#0ACF97` (Collected)
13. Set **Anchor Point 0, height ÷ 2**
14. Set **Position (x, 219)** — X from the table below
15. Name it `BAR_MAR_INV`, `BAR_MAR_COL`, and so on

| Month | Invoiced X | Collected X |
|---|---|---|
| Mar | 93 | 111 |
| Apr | 161 | 179 |
| May | 228 | 246 |
| Jun | 296 | 314 |
| Jul | 364 | 382 |
| Aug | 431 | 449 |

16. Back in `SHOT_DASH`, drag `CARD_BARS` in
17. Anchor Point **248, 136**, Position **482, 415**

---

## H · SHOT_DASH — donut

1. **Ctrl+N** → name `CARD_DONUT`, **328 × 272**, 30 fps, 6 s

Inside `CARD_DONUT`:

2. Shape Layer → Rectangle **328 × 272**, Roundness 12, Fill `#232734`,
   Stroke `#333A49` 1 px → Position **164, 136**
3. Type `Outstanding balance` — Inter SemiBold **14**, `#F0F2F7`, Position **16, 28** *align*
4. Type `Where the open money sits by age.` — Inter Medium **11**, `#7E899B`,
   Position **16, 45** *align*
5. Shape Layer, name `ARC_NOTDUE` → **Add ▸ Ellipse** Size **127 × 127** →
   **Add ▸ Stroke** `#0ACF97` width **19** → **no Fill** → Position **166, 147**
6. **Add ▸ Trim Paths** → Start **0%**, End **53%**
7. **Ctrl+D** twice, rename `ARC_OVER30` and `ARC_OVER30PLUS`
8. `ARC_OVER30`: Stroke `#FFBC00`, Trim Start **53%**, End **85%**
9. `ARC_OVER30PLUS`: Stroke `#FA5C7C`, Trim Start **85%**, End **100%**
10. If the segments start in the wrong place, set the layer **Rotation** until they line up
11. Type `$51,750` — Inter Tight ExtraBold **20**, `#F0F2F7`, centred + **Ctrl+Alt+Home**,
    Position **166, 144**
12. Type `outstanding` — Inter Medium **9**, `#7E899B`, centred + **Ctrl+Alt+Home**,
    Position **166, 160**
13. Three legend dots (Ellipse 5 × 5) + three labels (Inter Medium 9) at y **243** *align*:
    `Not yet due` `#0ACF97` · `Overdue < 30d` `#FFBC00` · `Overdue 30+ d` `#FA5C7C`
14. Back in `SHOT_DASH`, drag `CARD_DONUT` in
15. Anchor Point **164, 136**, Position **908, 415**

---

## I · SHOT_DASH — recent invoices + frame

1. Shape Layer → Rectangle **839 × 135**, Roundness 12, Fill `#232734`,
   Stroke `#333A49` 1 px → Position **652, 636**
2. Type `Recent Invoices` — Inter SemiBold **14**, `#F0F2F7` *align*
3. Type `Latest 6 of 234 synced` — Inter Medium **11**, `#7E899B` *align*
4. Type `View all →` — Inter Medium **11**, `#727CF5`, right aligned *align*
5. Type the five column headers — Inter SemiBold **9**, Tracking **+80**, `#7E899B` *align*:
   `INVOICE #` `CUSTOMER` `DUE` `BALANCE` `STATUS`
6. Type the one visible row — Inter Medium **11** *align*:
   `INV-28170` `Sofia Whitfield` `2026-08-19` `$1,650.00` `● Open`
7. Select layers 1–6 → **Ctrl+Shift+C** → name `CARD_RECENT`
8. Anchor Point **652, 636**, Position **652, 636**

Then the border:

9. Shape Layer, name `SHOT_FRAME` → Rectangle **1116 × 704**, Roundness 16,
   **no Fill**, Stroke `#333A49` 1.5 px → Position **558, 352**
10. Drag `SHOT_FRAME` to just **below** `REF_CAPTURE`

---

## J · F12_CONTENT

Open `F12_CONTENT`.

1. **Ctrl+Y** → 1920 × 1080 solid, colour `#16191F`, name `BG_PREVIEW`
2. Send it to the bottom → **Layer ▸ Guide Layer**
3. Drag `SHOT_DASH` in → Position **960, 619** (leave Anchor at 558, 352)
4. **Layer ▸ Layer Styles ▸ Drop Shadow** → Opacity **50**, Distance **12**, Size **40**,
   Angle **120**
5. Type `Your whole month, at a glance.` — Inter Tight ExtraBold **96**, Tracking **−20**,
   `#F0F2F7`, **centre aligned**, Position **960, 200**. Do **not** Ctrl+Alt+Home.
6. On that layer: **Text ▸ Animate ▸ Fill Color ▸ RGB**
7. `Range Selector 1 ▸ Advanced` → **Units → Index**
8. **Start 18**, **End 29**
9. Set the animator's **Fill Color** to `#727CF5`
10. Set **Amount** to **0%**

---

## K · Counters

For each of the four tile value layers (inside `TILE_1`–`TILE_4`):

1. Select the value text layer
2. **Alt+click** the **Source Text** stopwatch
3. Paste the expression for that tile:

| Comp | Expression |
|---|---|
| `TILE_1` | `n = easeOut(time, framesToTime(54), framesToTime(72), 0, 48);`<br>`Math.round(n).toString()` |
| `TILE_2` | `n = easeOut(time, framesToTime(58), framesToTime(76), 0, 25);`<br>`Math.round(n).toString()` |
| `TILE_3` | `n = easeOut(time, framesToTime(62), framesToTime(80), 0, 17);`<br>`Math.round(n).toString()` |
| `TILE_4` | `n = easeOut(time, framesToTime(66), framesToTime(84), 0, 37);`<br>`Math.round(n).toString()` |

No keyframes and no Slider Control — `easeOut()` clamps at both ends, so the value can't
overshoot its target or drop below zero. Before the start frame it reads 0; after the end
frame it holds the target.

Keyframing a slider instead lets the interpolation overshoot — the count runs past its
target and comes back.

---

## L · Bars

Inside `CARD_BARS`, for all 12 bars:

1. Press **S**, **click the chain icon** to unlink X from Y
2. Keyframe Scale Y — X stays 100 throughout:

| Pair | Frame | Scale Y |
|---|---|---|
| Mar | 66 → 84 | 0 → 100 |
| Apr | 70 → 88 | 0 → 100 |
| May | 74 → 92 | 0 → 100 |
| Jun | 78 → 96 | 0 → 100 |
| Jul | 82 → 100 | 0 → 100 |
| Aug | 86 → 104 | 0 → 100 |

3. **Shift+F9** on each end keyframe

---

## M · Donut

Inside `CARD_DONUT`, keyframe **Trim Paths ▸ End** on each arc:

| Layer | Frame | End |
|---|---|---|
| `ARC_NOTDUE` | 90 → 108 | 0% → **53%** |
| `ARC_OVER30` | 102 → 114 | 53% → **85%** |
| `ARC_OVER30PLUS` | 111 → 120 | 85% → **100%** |

**Shift+F9** on each end keyframe. Leave Trim **Start** alone.

---

## N · Frame timing

In `F12_CONTENT`:

| Layer | Property | Frame | Value |
|---|---|---|---|
| `HEADLINE` | Position | 6 → 18 | 960, 224 → 960, **200** |
| `HEADLINE` | Opacity | 6 → 18 | 0 → 100 |
| `HEADLINE` animator | Amount | 24 → 30 | 0 → 100% |
| `SHOT_DASH` | Position | 30 → 45 | 960, 655 → 960, **619** |
| `SHOT_DASH` | Opacity | 30 → 45 | 0 → 100 |

**Shift+F9** on every end keyframe, then right-click → **Keyframe Velocity** → Incoming
Influence **80**.

---

## O · Overdue flash

In `SHOT_DASH`:

1. Shape Layer, name `TILE_FLASH` → Rectangle **195 × 98**, Roundness 8, **no Fill**,
   Stroke `#FA5C7C` 2 px → Position **758, 213**
2. Put it above `TILE_3`
3. Keyframe Opacity: frame **126** = 0, frame **130** = **60**, frame **135** = 0

---

## P · Finish

1. Toggle `REF_CAPTURE`'s eye on and off at frame 135 — every built element should sit on
   its reference
2. Confirm `BG_PREVIEW` and `REF_CAPTURE` both show the Guide Layer icon
3. Drop `F12_CONTENT` into `MASTER` after `F11_CONTENT`
4. **File ▸ Increment and Save**

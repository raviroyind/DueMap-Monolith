# T1 "Convergence" — build steps

**1920 × 1080 · 30 fps · 24 frames (0.8 s).** Bridges F2 → F3.
Clutter from the first two frames collapses into the spark mark.

> **Decide this first — the handoff to F3.** F3's `SPARK` currently animates in at frames
> 6–20 (Scale 0 → 14 → 12, Rotation −90 → 0). If T1 hands over a formed spark, F3 will
> scale it from zero again and the mark visibly rebuilds.
>
> **Delete F3's `SPARK` entrance keyframes.** T1 forms the mark; F3 opens with it already
> present and only types the wordmark on. That's the whole point of a convergence — if F3
> still builds its own spark, T1 is decoration rather than a bridge.
>
> Section H covers the position handoff.

---

## A · Comp

1. **Ctrl+N** → name `T1_CONVERGENCE`, **1920 × 1080**, **30 fps**, duration **0:00:00:24**
2. **Ctrl+click the time display** to work in frames
3. **Ctrl+Y** → 1920 × 1080 solid, colour `#16191F`, name `BG_PREVIEW`
4. Send it to the bottom → **Layer ▸ Guide Layer**
5. Project panel → new folder `Transitions`, drag the comp in

---

## B · Spark

1. Drag `assets/duemap-spark.svg` in, name it `SPARK`
2. **Ctrl+Alt+Home**, then Position **960, 540**
3. Scale so it measures about **167 px** tall (roughly 21%)

---

## C · Orbit rings

Three shape layers, all centred on the spark. No Fill.

| Layer | Ellipse Size | Position | Stroke |
|---|---|---|---|
| `RING_1` | 520, 520 | 960, 540 | `#727CF5` 1.5 px |
| `RING_2` | 700, 700 | 960, 540 | `#727CF5` 1.5 px |
| `RING_3` | 890, 890 | 960, 540 | `#727CF5` 1.5 px |

For each:

1. **Layer ▸ New ▸ Shape Layer**
2. **Add ▸ Ellipse** → Size as above
3. **Add ▸ Stroke** → `#727CF5`, Width 1.5, then set **Stroke 1 ▸ Opacity** to **18%**
4. **Add ▸ Trim Paths** → set **End** to **0%**
5. Layer Position **960, 540**

Anchor Point stays **0, 0** on all three — `Add ▸ Ellipse` puts the path there already.

---

## D · The ten shards

Each shard is a fragment the viewer just watched in F1 or F2. **Duplicate the existing
comps** from `Frame-1/` and `Frame-2/` wherever one exists — that's what makes the
convergence read as *this film's* clutter rather than generic debris.

After duplicating, **delete any keyframes inside** the copy. The shard's motion is
keyframed on the layer in T1, not internally.

| # | Shard | Source | Start Position | Start Rotation |
|---|---|---|---|---|
| 1 | `call unit 4B` amber sticky | F1 `STICKY_NOTE` | 345, 154 | **−8°** |
| 2 | `$1,450.00` panel | F1 `ROW_SPREADSHEET` cell | 242, 360 | 0° |
| 3 | `Due Aug 1` pill | build fresh | 495, 656 | **−8°** |
| 4 | `Hey, rent this week?` bubble | F1 `BUBBLE_1` | 373, 785 | **−8°** |
| 5 | `OVERDUE` chip | F1 `ROW_SPREADSHEET` cell | 1533, 212 | **+5°** |
| 6 | `AUG 12` chip | F1 `CHIP_AUG_12` | 1663, 452 | **−5°** |
| 7 | `fee cap?` amber pill | build fresh | 1637, 715 | 0° |
| 8 | `Unit 12 · grace?` chip | build fresh | 1463, 876 | **−5°** |
| 9 | Calendar grid fragment | F2 `CARD_2` repeater | 775, 283 | 0° |
| 10 | Skeleton bars fragment | F2 `CARD_1` bars | 743, 833 | 0° |

For each shard layer, set **Anchor Point** to wherever the artwork sits inside its comp,
then set Position to the value above.

---

## E · Resting states

At frame 0:

- All ten shards: **Opacity 100**, Scale **100%**, at their start Positions and Rotations
- `RING_1/2/3`: **Trim Paths End 0%**, layer Opacity 100
- `SPARK`: **Opacity 0**, Scale **0%**

---

## F · Timing

24 frames total. Shards stagger **1 frame** apart; each takes **14 frames** to travel.

### Shards

**Every shard ends identically** — Position **960, 540**, Scale **20%**, Rotation **0°**,
Opacity **0**. Only the start values differ.

Start Scale is **100%** and start Opacity **100** on all ten.

| # | Shard | Frames | Start Position | Start Rotation |
|---|---|---|---|---|
| 1 | `call unit 4B` | 0 → 14 | **345, 154** | −8° |
| 2 | `$1,450.00` | 1 → 15 | **242, 360** | 0° |
| 3 | `Due Aug 1` | 2 → 16 | **495, 656** | −8° |
| 4 | `Hey, rent this week?` | 3 → 17 | **373, 785** | −8° |
| 5 | `OVERDUE` | 4 → 18 | **1533, 212** | +5° |
| 6 | `AUG 12` | 5 → 19 | **1663, 452** | −5° |
| 7 | `fee cap?` | 6 → 20 | **1637, 715** | 0° |
| 8 | `Unit 12 · grace?` | 7 → 21 | **1463, 876** | −5° |
| 9 | Calendar grid | 8 → 22 | **775, 283** | 0° |
| 10 | Skeleton bars | 9 → 23 | **743, 833** | 0° |

Worked example — shard 1:

| Property | Frame 0 | Frame 14 |
|---|---|---|
| Position | 345, 154 | **960, 540** |
| Scale | 100% | **20%** |
| Rotation | −8° | **0°** |
| Opacity | 100 | **0** |

### Rings

| Layer | Frames | Trim End | Scale |
|---|---|---|---|
| `RING_1` | 0 → 18 | 0 → **100%** | **140% → 100%** |
| `RING_2` | 2 → 20 | 0 → **100%** | 140% → 100% |
| `RING_3` | 4 → 22 | 0 → **100%** | 140% → 100% |

### Spark

| Property | Frames | Value |
|---|---|---|
| Opacity | 6 → 16 | 0 → **100** |
| Scale | 6 → 20 | 0 → **21%** |

---

## G · Easing — this is an exit, not an entrance

The shards are **leaving**, so ease the **first** keyframe, not the last:

1. Select every shard's **first** keyframe
2. **Ctrl+Shift+F9** (Easy Ease Out)
3. Leave the **last** keyframes linear

Slow start, accelerating into the centre. Using Shift+F9 here would make them decelerate
as they arrive, which reads as parking rather than collapsing.

The rings and spark are entrances — **Shift+F9** on their **last** keyframes as usual.

---

## H · Placing it in MASTER

1. Drag `T1_CONVERGENCE` into `MASTER`
2. Position it so frame 0 lands on `F2_CONTENT`'s last frame
3. `F3_CONTENT` starts on T1's last frame

Then, in `F3_CONTENT`:

4. Select `SPARK` → press **U** → **delete its Scale and Rotation keyframes**
5. Set `SPARK` to its final values — Scale 12%, Rotation 0°, Opacity 100 — from frame 0
6. Leave the `WORDMARK` type-on (frames 20 → 34) and `TAGLINE` (40 → 46) alone

**The size and position won't match across the cut.** T1 ends with the spark at
**960, 540 at 21%**; F3 has it at **718, 494 at 12%**, offset left to make room for the
wordmark. Add a move at the head of F3:

| Layer | Property | Frames | Value |
|---|---|---|---|
| `SPARK` | Position | 0 → 20 | 960, 540 → **718, 494** |
| `SPARK` | Scale | 0 → 20 | 21% → **12%** |

The mark settles into its lockup position as the wordmark types on beside it. That's a
better reveal than F3's original scale-from-zero, and it's only possible because T1 hands
the spark over already formed.

---

## I · Check

1. Scrub 0 → 24 at 25% zoom. Every shard should reach the centre and vanish; nothing
   should still be visible at frame 24 except the spark and rings
2. Play across the T1 → F3 boundary. The spark must not flicker, resize abruptly, or
   restart. If it does, F3 still has its entrance keyframes
3. Confirm T1's last frame and F3's first frame show the spark at the same size

---

## J · Notes for the rest of the transition set

- **T1 is the template.** The checklist asks for one repeated device or a small keyed set;
  convergence-into-the-mark only works once, at the brand reveal. The other joins need a
  different, quieter device — a push or a wipe — or the film starts feeling like a
  showreel.
- **The ten shards are all from F1 and F2 on purpose.** Nothing from F4 onward should
  appear; the viewer hasn't seen it yet, and the point is that the clutter being destroyed
  is the clutter they've been watching.
- **Rose appears here** on the `OVERDUE` chip. That's correct and it's rose's last
  appearance in the film — it's being collapsed into the mark, which is literally the
  colour retiring. Don't carry it past T1.

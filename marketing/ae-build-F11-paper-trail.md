# After Effects build guide — F11 "Paper trail" (1:06–1:12)

**1920×1080 · 30 fps · 6 s (180 frames)**. All coordinates are comp-space at 1920×1080
and match the approved mockup.

> **F11 does not need a product screenshot.** Handbook §7 lists it alongside F5, F6 and
> F12 as a capture frame, but the approved mockup is a native build — a stylised activity
> card, not a `/leases/{id}` capture. That's one blocker removed and one fewer 2× capture
> to take. Update §7 when you next touch the handbook.

---

## 1 · Comp setup

1. **Ctrl+N** → `F11_CONTENT`, 1920 × 1080, 30 fps, duration **`0:00:06:00`** (180 frames)
2. **Ctrl+click the time display** — work in frames
3. Solid `#16191F` named `BG_PREVIEW` at the bottom → **Layer ▸ Guide Layer**
4. Make a **`Frame-11/`** folder in the Project panel

Text on the 160 margin left, card ending on the 1760 margin right.

---

## 2 · Layer stack

```
F11_CONTENT
├── LOCK_CHIP                    (precomp — pill + lock + text)
├── ROW_4  ROW_3  ROW_2  ROW_1   (precomps)
├── CARD_TITLE
├── CARD_DIVIDERS
├── CARD_BODY
├── SUBLINE
├── HEADLINE
└── BG_PREVIEW                   (Guide Layer)
```

---

## 3 · Text

Both **left aligned**, **Anchor Point 0, 0**, no Ctrl+Alt+Home.

| Layer | Text | Font | Size | Leading | Tracking | Colour | Position |
|---|---|---|---|---|---|---|---|
| `HEADLINE` | `A paper trail you ⏎ can stand on.` | Inter Tight ExtraBold | 96 | **100** | −20 | `#F0F2F7` | 160, 488 |
| `SUBLINE` | `Every notice, stamped and kept.` | Inter Medium | 40 | — | 0 | `#AEB6C4` | 160, 672 |

### Recolouring "stand on"

**Characters, not words** — the terminal period stays white, and `on.` is one word.

1. **Text ▸ Animate ▸ Fill Color ▸ RGB**
2. `Range Selector 1 ▸ Advanced` → **Units → Index**, leave Based On at **Characters**
3. **Start 22**, **End 30**
4. Animator **Fill Color** → `#727CF5`
5. Leave **Amount** at 0% — keyframed in §7

> **Verify these two numbers on screen.** AE counts the manual line break as a character,
> so the indices depend on the break landing where you put it. If the highlight starts one
> letter early or late, shift both by the same amount rather than re-deriving them.

Sub-line stays flat `#AEB6C4` — the headline already carries the frame's one accent.

---

## 4 · The card

| Layer | Spec | Position |
|---|---|---|
| `CARD_BODY` | Rectangle **860 × 506**, **Roundness 20**, Fill `#232734` | 1330, 393 |
| `CARD_TITLE` | `Lease activity` — Inter SemiBold 30, `#F0F2F7`, left aligned | 948, 226 |

Card spans x 900 → 1760, y 140 → 646. **48 px inner padding** on both sides, so content
runs 948 → 1712.

### CARD_DIVIDERS

**One** shape layer, four rectangles, each **764 × 1**, Fill `#333A49`:

| Rectangle | Position |
|---|---|
| Header rule | 1330, 264 |
| Row rule 1 | 1330, 349 |
| Row rule 2 | 1330, 433 |
| Row rule 3 | 1330, 517 |

Four, not five — no rule below the last row, the card edge does that.

**These are absolute path coordinates in one layer**, the same exception F6's
`LIST_DIVIDERS` uses. So neutralise the layer transform:

| `CARD_DIVIDERS` layer | Value |
|---|---|
| Anchor Point | **1330, 393** |
| Position | **1330, 393** |

---

## 5 · The four rows

Row pitch is **84 px**. Each row is a dot, a label and a timestamp, precomposed.

| Row | Centre Y | Text baseline Y |
|---|---|---|
| 1 | 307 | 315 |
| 2 | 391 | 399 |
| 3 | 475 | 483 |
| 4 | 559 | 567 |

| Element | Spec | X |
|---|---|---|
| Dot | Ellipse **14 × 14** | 955 |
| Label | Inter SemiBold **28**, `#F0F2F7`, left aligned | 982 |
| Timestamp | Inter Medium **24**, `#7E899B`, **right aligned** | 1712 |

The timestamp is right aligned, so Anchor 0, 0 pins its **right** edge to 1712. Don't
Ctrl+Alt+Home it.

| Row | Dot colour | Label | Timestamp |
|---|---|---|---|
| `ROW_1` | `#0ACF97` | `Reminder` | `Aug 1, 6:02 PM CT` |
| `ROW_2` | `#FFBC00` | `Due-today nudge` | `Aug 5, 6:01 PM CT` |
| `ROW_3` | `#727CF5` | `Promise logged — Fri, Aug 15` | `Aug 8, 2:14 PM CT` |
| `ROW_4` | `#FFBC00` | `Late-fee warning` | **`Aug 16, 6:00 PM CT`** |

### Placing the four rows

Build row 1's three layers, precompose as `ROW_1`, then **duplicate the comp in the
Project panel** three times and edit each copy's label, timestamp and dot colour.

Because every copy's artwork stays at row 1's Y, **Anchor Point is 1330, 307 on all
four** — it points at the artwork inside the comp, not at the destination:

| Layer | Anchor Point | Position |
|---|---|---|
| `ROW_1` | 1330, **307** | 1330, 307 |
| `ROW_2` | 1330, **307** | 1330, **391** |
| `ROW_3` | 1330, **307** | 1330, **475** |
| `ROW_4` | 1330, **307** | 1330, **559** |

> **Anchor and Position match only on row 1.** Setting a row's anchor to its destination
> cancels the move — `391 + (307 − 391) = 307`, and rows 1 and 2 land on top of each other.
> Same rule as F7's state chips: all seven were built at 960, 540 and placed at seven
> different X values with one shared anchor.
>
> If you'd rather have anchor and position match on every row, move each copy's artwork
> down inside its own comp instead. More typing, same result.

> **Row 4's date is changed from the mockup — Aug 12 → Aug 16.** As drawn, a promise is
> logged on Aug 8 for **Fri, Aug 15**, and then a late-fee warning fires on **Aug 12** —
> three days *before* the promised date. That directly contradicts F10, which the viewer
> watched six seconds earlier and which claims notices pause when a promise is logged.
>
> Aug 16 makes the timeline the literal payoff of F10's closing line, *"and resumes
> automatically if the promise breaks"*: promised Friday the 15th, didn't pay, warning went
> out Saturday the 16th. One string, and the two frames now prove each other.
>
> The cost is F1's `AUG 12` chip losing its echo here. `Fri, Aug 15` is the stronger link —
> it's the value the viewer just read in F10's input field.

---

## 6 · LOCK_CHIP

```
LOCK_CHIP           ← the precomp
├── CHIP_LABEL
├── LOCK_SHACKLE
├── LOCK_BODY
└── CHIP_PILL
```

| Layer | Spec | Position |
|---|---|---|
| `CHIP_PILL` | Rectangle **174 × 42**, **Roundness 21** (full pill), Fill `#2B3040` | 1625, 216 |
| `LOCK_BODY` | Rectangle **13 × 11**, Roundness 2, Fill `#AEB6C4` | 1552, 219 |
| `LOCK_SHACKLE` | Ellipse **9 × 9**, no Fill, Stroke `#AEB6C4` **2 px** | 1552, 210 |
| `CHIP_LABEL` | `append-only` — Inter Medium 20, `#AEB6C4`, left aligned | 1571, 222 |

| | Anchor Point | Position |
|---|---|---|
| `LOCK_CHIP` | 1625, 216 | 1625, 216 |

The shackle sits above the body and overlaps it by ~1 px, so the ellipse's lower arc is
hidden. At 13 px this is two shapes doing a lot of work — if it reads as a smudge at 25%
zoom, delete both and let `append-only` carry it. The word is the claim; the glyph is
decoration.

---

## 7 · Resting states

| Layer | Value |
|---|---|
| Every layer except `BG_PREVIEW` | Opacity 0 |
| `HEADLINE` animator **Amount** | 0% |

---

## 8 · Timing sheet

Six seconds, against the VO:
*"Every notice is time-stamped and kept for good — documentation that holds up when it
matters."*

| Start | End | Layer | Move |
|---|---|---|---|
| 6 | 18 | `HEADLINE` | Rise + fade — **hero**, 12 frames |
| 24 | 30 | `HEADLINE` animator | Amount 0 → 100% — "stand on" turns indigo |
| 30 | 42 | `CARD_BODY` + `CARD_DIVIDERS` | Rise + fade from Y **+32** |
| 36 | 42 | `SUBLINE` | Rise + fade |
| 48 | 54 | `CARD_TITLE` | Fade in |
| 60 | 66 | `ROW_1` | Fade in · *"every notice is time-stamped"* |
| 72 | 78 | `ROW_2` | Fade in |
| 84 | 90 | `ROW_3` | Fade in |
| 96 | 102 | `ROW_4` | Fade in |
| **120** | **129** | `LOCK_CHIP` | **Pop** · *"documentation that holds up"* |
| 129 | 180 | — | **Hold** |

### The chip lands last, on purpose

`append-only` is the frame's actual argument — a timeline anyone can edit proves nothing.
Popping it after all four rows have landed makes it read as the *guarantee* on what you
just watched accumulate, rather than as a label on the card. It's also the only element in
the frame that pops; everything else fades.

### Rows fade, they don't rise

Same reasoning as F9's notice contents. A ledger whose entries slide into place looks like
a UI being assembled; entries that simply appear, one after another, read as a record
being written. The frame is about permanence — nothing should look repositionable.

---

## 9 · Motion recipes

### Rise + fade

| Layer | Position start | Position end |
|---|---|---|
| `HEADLINE` | 160, 512 | 160, 488 |
| `SUBLINE` | 160, 696 | 160, 672 |
| `CARD_BODY` | 1330, **425** | 1330, 393 |
| `CARD_DIVIDERS` | 1330, **425** | 1330, 393 |

`CARD_BODY` and `CARD_DIVIDERS` must carry **identical** keyframes — they're separate
layers drawing one object. Select both and key them together, or the rules will lag the
card by a frame and smear.

### Pop — `LOCK_CHIP`

Scale **88 → 106 → 100** at frames 120 / 125 / 129, Opacity 0 → 100 over 120 → 124.

### Easing

**Shift+F9** on the **last** keyframe of every entrance, Incoming Influence **80%**. First
keyframe linear. **F9** the middle keyframe of the pop. Plain fades need none.

---

## 10 · Polish

- **Check the timestamp column at 100%.** Four right-aligned strings on 1712 — if one is
  off, it's a Ctrl+Alt+Home that shouldn't have been run.
- **Read all four rows in order** and check the story still holds after the Aug 16 change:
  reminder before due, nudge on the day, promise logged, warning after the promise broke.
- **Motion blur off.** Nothing travels far.
- **Hold the last 51 frames.**

---

## 11 · Finishing

1. Drop `F11_CONTENT` into **MASTER** after `F10_CONTENT`
2. Confirm `BG_PREVIEW` is still a Guide Layer
3. **File ▸ Increment and Save**

---

## 12 · Notes for the final pass

- **This card is the film's whole timeline in one place.** Aug 1 reminder is F1's first
  chip; Aug 5 is F6's close; `Fri, Aug 15` is the value typed into F10's modal; the
  late-fee warning is what F1's rose `OVERDUE` row was dreading. One lease, eight frames,
  one ledger. Any date edit has to be checked against all of them.
- **Green, amber and indigo all appear as dots**, which is the first time three state
  colours share one element in this film. That's fine — they're a legend, not accents.
  Don't add a fourth.
- **`Fri, Aug 15` is not a Friday in 2026**, which is the year showing in F5's captured
  invoice dates. Nobody will cross-reference a day-of-week against a screenshot from 40
  seconds earlier, but if the seed data is ever regenerated, pick a year where Aug 15 falls
  on a Friday and the whole film lines up.
- **F11 is in neither cutdown**, so no trim constraint and no 9:16 version needed.

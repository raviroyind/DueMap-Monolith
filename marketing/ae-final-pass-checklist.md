# DueMap ad — final pass checklist

Frames are being built as **self-contained modules**: entrance + hold, no exits,
no cross-boundary animation. Everything below is deliberately deferred until all
frames exist, so it can be designed as one coherent pass rather than fourteen
local decisions.

---

## 1. Transition screens

The reason frames are modular. Design these once the full set exists and the
rhythm is visible.

- Decide the transition vocabulary — one repeated device, or a small set keyed to
  narrative beats (e.g. convergence into the mark at F3, a push at F6).
- Build each as its own comp so it can be re-timed without touching frame content.
- The convergence idea from the script is still worth using somewhere: F2's three
  cards collapsing inward to become F3's spark. F2_CONTENT's anchor is already at
  comp centre, so scaling that layer down converges everything for free.

## 2. Undo the F1 → F2 crossover

Built before the modular decision was made. In MASTER, `F1_CONTENT` carries Scale
and Opacity keyframes at frames 168 and 178, and `F2_CONTENT` starts at 168.
Remove those four keyframes and re-lay the timing when transitions are designed.

## 3. Atmospheric arc

Currently only F1's rose glow build (frames 150→180) exists. The film's colour
story still needs laying in across MASTER:

- **Rose retires at F3.** `GLOW_ROSE_R` and `GLOW_ROSE_BL` fade to 0 over roughly
  44 frames — slow enough that nobody notices it leaving.
- **Indigo blooms at F3.** New shape layer: ellipse 1200×800 at (960, 490), fill
  `#727CF5`, Fast Box Blur 250 / 3 iterations / Repeat Edge Pixels off, blending
  mode Add, opacity ramping 0 → 12%. Sits above BG_INK, below the rose glows.
- **Green resolution.** The last third of the film (F10 onward) shifts toward
  `#0ACF97` per the brand kit. Same technique.

## 4. Frame timing in MASTER

Frames are laid end to end for now. Final pass sets the real rhythm — nominal
6s each, but the transition screens will claim frames from both sides.

## 5. Motion blur

Enable the comp master switch and tick per-layer on anything that travels far
(sticky note, spreadsheet row, card entrances). Untick on text layers if they
soften.

## 6. Idle drift

Keeps held frames from reading as posters. Alt+click a Position stopwatch and
add `wiggle(0.3, 8)`. Apply to clutter, never to type or to elements that need
to feel systemic.

**Do not add this by parenting to a null** — re-parenting a layer that already
has Position keyframes reinterprets those keyframes in the parent's space and
scatters the animation.

## 7. Audio

- VO recorded and cut to the script.
- Music: sparse percussion under F1–F2, opening out at the F3 brand reveal.
- Frame timings were choreographed against VO phrasing (each clutter element in
  F1 lands on its spoken noun; each F2 card lands on its question). Expect to
  re-time slightly once real audio exists — the script's word counts were
  estimates.

## 8. Per-frame guide backgrounds

Each frame comp has a `BG_PREVIEW` ink solid marked as a **Guide Layer** so it
renders while working but is excluded from output and from MASTER. Confirm every
frame has one, and that none were accidentally un-flagged.

## 9. Export

- Master: ProRes 422 HQ, **Time Span = Length of Comp** (not Work Area).
- Deliver H.264 via Media Encoder from the ProRes, never straight from AE.
- Then the 30s and 15s cutdowns, and the 9:16 versions of those two only.

## 10. Outstanding asset + copy blockers

- **Official QuickBooks logo** still missing (`wwwroot/img/intuit/`). F4 needs the
  licensed mark from Intuit's brand portal — the "QB" lettermark is a placeholder.
- **Confirm the public domain** before rendering F14's end card. Dev config says
  `duemap.dev`.
- **Proof-point slots** in the script (F13 setup time) are still unfilled. Fill
  with measured data or cut the line.
- **State claims:** name only the 7 launch states (AZ CA FL GA NC NY TX). The
  database lists all 50 but only those 7 have rule versions.

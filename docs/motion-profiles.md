# Motion Profiles

## Design goal

Synthetic motion is used instead of G-code so the control input is deterministic, minimal, and analytically differentiable.

## Segment types

### Constant velocity

For duration `T`:

\[
v(t)=v_0
\]

\[
a(t)=0
\]

\[
x(t)=v_0t
\]

### Constant acceleration

For `0 <= t <= T`:

\[
v(t)=v_0+at
\]

\[
x(t)=v_0t+\frac12at^2
\]

The builder may accept `v0`, `v1`, and positive acceleration magnitude `a_m`, deriving:

\[
a=sign(v_1-v_0)a_m
\]

\[
T=\frac{|v_1-v_0|}{a_m}
\]

## Transition metadata

Each segment boundary should produce a transition record containing at least:

- unique index or name;
- absolute time;
- absolute distance;
- velocity before;
- velocity after;
- requested flow before;
- requested flow after where known;
- semantic kind such as acceleration-start, cruise-start, slowdown-start, corner-entry, corner-exit, stop.

Metrics should consume these markers instead of guessing transitions from derivatives.

## Built-in scenarios

### acceleration

Purpose: isolate acceleration under-flow.

Suggested shape:

- 20 mm/s steady segment;
- accelerate to 100 mm/s at 2000 mm/s²;
- 40 mm cruise at 100 mm/s.

### deceleration

Purpose: isolate pressure release and over-flow.

Suggested shape:

- 100 mm/s steady segment;
- decelerate to 20 mm/s at 2000 mm/s²;
- 40 mm cruise at 20 mm/s.

### trapezoid

Purpose: show accelerate/cruise/decelerate behavior in one move.

Suggested shape:

- 20 mm/s;
- accelerate to 120 mm/s;
- cruise;
- decelerate back to 20 mm/s;
- settle segment.

### corner

Purpose: approximate the extrusion demand around a high-speed path corner without XY kinematics.

Suggested shape:

- 30 → 120 mm/s acceleration;
- high-speed cruise;
- 120 → 35 mm/s deceleration;
- 2 mm cruise at 35 mm/s;
- 35 → 120 mm/s acceleration;
- high-speed cruise;
- decelerate to 0.

Include explicit `corner-entry` and `corner-exit` markers.

### multi-change

Purpose: stress repeated transients.

Suggested plateaus:

- 40;
- 120;
- 40;
- 80;
- 30;
- 100;
- 20 mm/s.

Connect plateaus with constant acceleration and short cruises.

## Endpoint handling

The simulator must avoid profile-evaluation ambiguity exactly at segment boundaries.

Recommended policy:

- segments are half-open `[start, end)` except the final segment, which includes the final endpoint;
- a boundary time evaluates to the next segment state, allowing acceleration to change exactly at the marker;
- profile duration is the exact sum of segment durations;
- the final sample evaluates the final segment endpoint.

Tests must encode whichever boundary rule is implemented so reporting and settling analysis remain stable.

# Testing Strategy

The repository should favor deterministic unit tests with a few artifact-level integration tests.

## Core mathematical tests

### Geometry conversion

Given:

- velocity 100 mm/s;
- width 0.45 mm;
- height 0.20 mm.

Expected requested flow: exactly 9.0 mm³/s within floating-point tolerance.

### Flow derivative

For width 0.45 mm, height 0.20 mm, acceleration 2000 mm/s²:

\[
\dot Q=0.45\cdot0.20\cdot2000=180\ mm^3/s^2
\]

### Pressure advance

With K=0.04 s and derivative 180 mm³/s²:

\[
Q_{advance}=7.2\ mm^3/s
\]

Test positive and negative derivatives.

### Clamp

If requested flow is 1.0 and advance is -2.0:

- raw drive = -1.0;
- ClampToZero final drive = 0.0 and `WasClamped=true`;
- AllowNegative final drive = -1.0 and `WasClamped=false`.

## First-order analytical response

For constant drive `Qd`, analytical pressure is:

\[
P(t)=P_\infty+(P_0-P_\infty)e^{-t/\tau}
\]

Use a fixture such as:

- tau = 0.04 s;
- gain = 1;
- Qd = 9 mm³/s;
- P0 = 0;
- duration = 0.2 s.

Run Euler at several step sizes and verify decreasing error as dt decreases.

Do not assert an unrealistically tight tolerance for explicit Euler.

## Motion tests

Cover:

- acceleration duration derived correctly;
- endpoint velocity;
- endpoint distance;
- segment-boundary evaluation rule;
- cumulative profile distance;
- transition marker positions;
- invalid discontinuity rejection unless explicitly allowed.

## Simulation tests

Cover:

- K=0 PA controller yields same samples as no-compensation controller;
- constant speed at steady-state initial pressure produces zero flow error;
- positive acceleration produces transient under-flow for K=0;
- deceleration produces transient over-flow for K=0;
- selected near-tau K reduces IAFE in an ideal non-saturating test scenario;
- excessive K worsens at least one transient metric;
- repeated run equality.

## Metric tests

Create tiny hand-computable sample sequences.

Verify:

- trapezoidal IAFE;
- RMSE;
- under/over volumes;
- IAFE ≈ under-volume + over-volume;
- peak sign/magnitude;
- zero-error sequence;
- settling boundary behavior.

## K sweep tests

Verify:

- inclusive endpoints;
- invalid range rejection;
- tie chooses lower K;
- best point exists;
- ideal-model grid result lies near tau within a tolerance no tighter than one or two sweep steps, depending on profile/clamp conditions.

## Reporting tests

### CSV

- header exactness;
- row count equals sample count;
- invariant decimal separator;
- booleans stable;
- no NaN/Infinity.

### JSON

- parse round trip;
- expected principal fields exist;
- invariant numeric representation;
- no NaN/Infinity.

### SVG

Parse output as XML and verify:

- root element is SVG;
- title exists;
- axis labels exist;
- at least one path/polyline for non-empty series;
- transition markers appear where expected;
- zero-range fixture renders;
- special characters are escaped;
- output contains no NaN/Infinity.

## CLI integration tests

At minimum test process-level behavior for:

- `list-scenarios` exit 0;
- invalid tau exit non-zero;
- simulate produces required artifact files;
- sweep produces sweep files.

If process-level tests become brittle, keep them few and push validation into testable CLI command classes.

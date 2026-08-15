# Acceptance Matrix

This matrix connects observable requirements to implementation evidence.

## Core physics

- Requested flow uses `v*w*h`.
  - Evidence: unit test with 100 × 0.45 × 0.20 = 9.0 mm³/s.
- Requested-flow derivative uses `w*h*a` for built-in profiles.
  - Evidence: unit test with 0.45 × 0.20 × 2000 = 180 mm³/s².
- Pressure advance uses `K*dQr/dt`.
  - Evidence: positive/negative derivative tests.
- Plant uses first-order Euler update.
  - Evidence: hand-calculated single-step test and analytical response test.
- Actual flow is `P/G`.
  - Evidence: steady-state and non-unit-gain tests.

## Control behavior

- K=0 equals no compensation.
  - Evidence: sample-by-sample equality test.
- Acceleration without PA creates negative signed flow error.
  - Evidence: acceleration scenario assertion.
- Deceleration without PA creates positive signed flow error.
  - Evidence: deceleration scenario assertion.
- Near-tau K reduces IAFE in suitable ideal scenario.
  - Evidence: deterministic comparison test.
- Excessive K demonstrates over-compensation.
  - Evidence: metric regression relative to near-optimal K.
- Clamp is visible.
  - Evidence: raw drive, final drive, clamp boolean, clamp count.

## Motion

- Required five scenarios exist.
  - Evidence: `list-scenarios` integration test.
- Motion is continuous by default.
  - Evidence: builder validation tests.
- Corner scenario represents speed reduction/recovery only.
  - Evidence: docs and scenario metadata; no XY kinematics types.

## Metrics

- IAFE is correct.
  - Evidence: hand-computable trapezoidal fixture.
- Peak under/over signs are correct.
  - Evidence: pure-positive and pure-negative fixtures.
- Directional volumes are correct.
  - Evidence: `IAFE ≈ under + over` invariant test.
- RMSE is correct.
  - Evidence: hand-computable fixture.
- Settling requires continuous hold window.
  - Evidence: in-band/out-of-band boundary tests.
- Settling distance is spatial delta from transition.
  - Evidence: synthetic sample-position test.

## Numerical behavior

- Fixed-step deterministic loop.
  - Evidence: repeated-run equality test.
- Final endpoint is represented exactly.
  - Evidence: duration not divisible by dt test.
- Euler convergence is observable.
  - Evidence: 4/2/1/0.5 ms analytical-error comparison.
- Large dt relative to tau warns.
  - Evidence: CLI/config warning test if warning is testable at command layer.

## Reports

- Required SVGs exist.
  - Evidence: integration test.
- SVGs parse as XML.
  - Evidence: XML parser test.
- No NaN/Infinity in SVG/JSON/CSV.
  - Evidence: content assertions.
- CSV rows equal sample count.
  - Evidence: integration test.
- JSON contains resolved configuration and metrics.
  - Evidence: deserialization/assertion test.
- K sweep chart marks best K.
  - Evidence: SVG structural/label assertion.

## CLI

- simulate works.
- compare works.
- sweep works.
- list-scenarios works.
- invalid inputs fail non-zero with actionable message.
- config precedence is CLI → JSON → defaults.

Evidence should be executable tests wherever practical rather than documentation-only claims.

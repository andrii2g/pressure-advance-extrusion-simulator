# Normative Specification

This file defines the normative behavior for `pressure-advance-extrusion-simulator`.

## 1. Product objective

The application shall simulate a deliberately simplified extrusion system in which nozzle flow lags a requested volumetric flow command. It shall compare uncompensated extrusion with pressure-advance feed-forward compensation across deterministic motion profiles.

The product is educational software, not printer-control software and not a calibration authority for real hardware.

## 2. Technology

- Runtime and target framework: .NET 10 / `net10.0`.
- Language: C#.
- Primary executable: console CLI.
- Core simulation: platform-neutral library.
- Reporting: separate library producing SVG, CSV, and JSON.

## 3. Units

All domain values shall use the following units unless a type or property explicitly states otherwise:

- time: seconds (`s`);
- distance: millimeters (`mm`);
- velocity: millimeters per second (`mm/s`);
- acceleration: millimeters per second squared (`mm/s²`);
- layer height / extrusion width: millimeters (`mm`);
- requested/advance/drive/actual flow: cubic millimeters per second (`mm³/s`);
- integrated flow error and directional error volume: cubic millimeters (`mm³`);
- K: seconds (`s`) in this simplified formulation;
- tau: seconds (`s`);
- gain G: model pressure units per (`mm³/s`).

No public CLI output may label K as dimensionless.

## 4. Requested-flow model

The simulator shall use the rectangular bead-area approximation:

\[
A = w h
\]

\[
Q_r = v A = vwh
\]

where `w` is extrusion width and `h` is layer height.

The application shall document that this is a pedagogical approximation and not an exact slicer bead-volume model.

## 5. Pressure-advance controller

The controller shall compute:

\[
Q_{advance} = K \dot{Q_r}
\]

\[
Q_{drive,raw} = Q_r + Q_{advance}
\]

For built-in motion profiles:

\[
\dot{Q_r} = wha
\]

The default drive-flow policy shall be `ClampToZero`:

\[
Q_d = \max(0,Q_{drive,raw})
\]

A second policy, `AllowNegative`, shall be supported for controlled experiments.

Every simulation sample shall expose both raw advance contribution and final drive flow so clamping is visible.

## 6. First-order plant

The plant state is nozzle pressure `P`.

The differential equation is:

\[
\tau \frac{dP}{dt}=GQ_d-P
\]

or:

\[
\frac{dP}{dt}=\frac{GQ_d-P}{\tau}
\]

Actual flow is:

\[
Q_a=\frac{P}{G}
\]

The numerical update shall use explicit Euler:

\[
P_{n+1}=P_n+\Delta t\frac{GQ_{d,n}-P_n}{\tau}
\]

Default initial pressure shall correspond to the scenario's initial requested steady-state flow unless the scenario explicitly supplies a different initial pressure. This avoids an unrelated startup transient in scenarios intended to study speed changes.

The simulation configuration shall permit explicit initial pressure so tests can exercise startup transients.

## 7. Motion profile

The motion profile shall be piecewise deterministic and expose velocity and acceleration at every simulation time.

The implementation shall support at minimum:

- constant-velocity segments;
- constant-acceleration segments between explicit start/end velocities;
- explicit segment duration or distance sufficient to resolve the segment deterministically;
- validation that adjacent segments are velocity-continuous unless a scenario explicitly opts into a discontinuity for a step-response test.

Built-in scenarios shall include:

- `acceleration`;
- `deceleration`;
- `trapezoid`;
- `corner`;
- `multi-change`.

The `corner` scenario models only the speed reduction and recovery associated with a corner. It shall not claim to simulate XY cornering dynamics.

## 8. Simulation sample

Every emitted sample shall contain at least:

- time;
- distance;
- velocity;
- acceleration;
- requested flow;
- requested-flow derivative;
- advance flow;
- raw drive flow;
- final drive flow;
- whether drive flow was clamped;
- nozzle pressure;
- equilibrium pressure for requested flow;
- actual flow;
- signed flow error.

Signed flow error shall be defined exactly as:

\[
e=Q_a-Q_r
\]

Therefore:

- `e < 0` means under-extrusion / under-flow;
- `e > 0` means over-extrusion / over-flow.

## 9. Metrics

Each run shall calculate:

### Integrated absolute flow error

\[
IAFE=\int |e(t)|dt
\]

Numerical integration shall use a deterministic rectangle or trapezoidal rule documented in code and tests. The preferred implementation is the trapezoidal rule over emitted samples.

### Peak under-flow

Report both signed minimum error and positive magnitude:

\[
e_{min}=\min e(t)
\]

\[
PeakUnder=max(0,-e_{min})
\]

### Peak over-flow

\[
PeakOver=max(0,\max e(t))
\]

### RMSE

\[
RMSE=\sqrt{\frac{1}{T}\int e(t)^2dt}
\]

### Under-extrusion volume

\[
V_{under}=\int \max(0,-e(t))dt
\]

### Over-extrusion volume

\[
V_{over}=\int \max(0,e(t))dt
\]

### Settling time and settling distance

Settling shall be measured after each marked transition event using an error tolerance and hold window.

Default settling tolerance shall be the larger of:

- 2% of the post-transition requested flow magnitude;
- an absolute floor of 0.02 mm³/s.

The default hold window shall be 0.050 s.

A transition is settled at the earliest sample from which the absolute error stays within tolerance for the complete hold window. Settling distance is the distance traveled between the transition sample and the first settled sample.

If the simulation ends before settling can be proven, settling time/distance shall be reported as unavailable rather than zero.

Aggregate metrics shall include the worst transition settling distance among transitions that settle, plus a count of unsettled transitions.

## 10. K sweep

The CLI shall support an inclusive K sweep from `start` to `end` by positive `step`.

Each K value shall run the same resolved scenario and plant configuration.

Sweep output shall include at least:

- K;
- IAFE;
- peak under-flow;
- peak over-flow;
- RMSE;
- under-volume;
- over-volume;
- clamp count;
- worst settling distance if available.

The best K shall be the K with minimum IAFE. Ties within a documented epsilon shall choose the lower K.

The built-in ideal-model sweep shall include tau in its search range and shall demonstrate a minimum near tau when negative drive is allowed or the selected profile does not materially activate the zero clamp near the optimum.

## 11. Output contracts

A single simulation shall write:

- `run.json`;
- `metrics.json`;
- `samples.csv`;
- `speed.svg`;
- `flow.svg`;
- `pressure.svg`;
- `flow-error.svg`.

A comparison run shall additionally write `comparison.svg`.

A sweep shall write:

- `k-sweep.csv`;
- `k-sweep.json`;
- `k-sweep.svg`.

Outputs shall be UTF-8 and culture-invariant. Numeric machine-readable output shall use invariant decimal notation.

## 12. SVG requirements

SVGs shall:

- be standalone SVG files;
- have explicit width, height, and viewBox;
- contain a title and axis labels;
- label units;
- handle constant-value series without division by zero;
- tolerate zero-length or tiny ranges;
- escape XML text;
- draw transition markers when available;
- be deterministic for identical input.

Charts shall use distance on the X axis because spatial error is intuitive for printing.

## 13. CLI validation

The CLI shall reject:

- `tau <= 0`;
- `gain <= 0`;
- `dt <= 0`;
- negative K;
- non-positive layer height or width;
- negative velocities;
- invalid acceleration segment definitions;
- empty scenarios;
- K sweep step `<= 0`;
- K sweep end `< start`;
- non-finite numbers.

Errors shall result in non-zero exit code and concise actionable text.

## 14. Determinism

Given the same configuration and executable version, the simulator shall produce the same numerical samples and metrics on repeated runs, allowing normal insignificant platform formatting differences only where unavoidable.

The simulator shall not use random values.

## 15. Explicit exclusions

The simulator shall not implement printer communication, G-code parsing, input shaping, heater PID, firmware parameter conversion, real-world PA calibration, or detailed polymer mechanics.

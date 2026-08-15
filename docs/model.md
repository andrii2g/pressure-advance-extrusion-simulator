# Mathematical Model

## Purpose of the simplification

Real extrusion contains motor dynamics, filament compression, tube compliance, melt rheology, nozzle restriction, temperature effects, retraction behavior, and geometry-dependent flow. This model intentionally collapses them into one first-order state so the transient and feed-forward compensation can be reasoned about analytically.

The model is not intended to predict a physical printer quantitatively.

## Geometry-to-flow conversion

Approximate deposited cross-sectional area:

\[
A=wh
\]

Requested volumetric flow:

\[
Q_r=vA=vwh
\]

Example:

- `v = 100 mm/s`;
- `w = 0.45 mm`;
- `h = 0.20 mm`.

Then:

\[
Q_r = 100\cdot0.45\cdot0.20 = 9.0\ mm^3/s
\]

## Flow derivative

For constant geometry:

\[
\dot Q_r=wh\dot v=wha
\]

The built-in scenarios expose acceleration analytically, and the simulator uses this expression instead of finite-difference differentiation.

This is important because the repository is teaching feed-forward behavior, not derivative-noise filtering.

## Pressure advance

Pressure advance is modeled as:

\[
Q_{advance}=K\dot Q_r
\]

\[
Q_{drive,raw}=Q_r+Q_{advance}
\]

`K` has units of seconds in this formulation because it multiplies a flow derivative (`mm³/s²`) to produce a flow contribution (`mm³/s`).

Default clamp:

\[
Q_d=\max(0,Q_{drive,raw})
\]

The raw value must still be recorded because it reveals when ideal feed-forward asks for reverse extrusion.

## Plant

The plant state `P` is an abstract effective nozzle pressure.

\[
\tau\dot P=GQ_d-P
\]

The equilibrium pressure for a constant drive flow is:

\[
P_{eq}=GQ_d
\]

Actual flow:

\[
Q_a=P/G
\]

At steady state:

\[
P=GQ_d \Rightarrow Q_a=Q_d
\]

## Why lag creates flow error

During acceleration, requested flow rises immediately with speed, but `P` approaches its new equilibrium exponentially. Therefore `Q_a < Q_r` temporarily.

During deceleration, requested flow falls while stored pressure decays with the same first-order lag. Therefore `Q_a > Q_r` temporarily.

## Ideal cancellation intuition

Ignoring clamp and assuming the input is differentiable, the transfer from drive flow to actual flow is:

\[
\frac{Q_a(s)}{Q_d(s)}=\frac{1}{\tau s+1}
\]

The feed-forward controller is:

\[
Q_d(s)=(1+Ks)Q_r(s)
\]

So:

\[
\frac{Q_a(s)}{Q_r(s)}=\frac{1+Ks}{1+\tau s}
\]

When:

\[
K=\tau
\]

the ideal continuous-time model cancels:

\[
\frac{Q_a(s)}{Q_r(s)}=1
\]

The implementation uses discrete Euler integration, finite motion segments, endpoint handling, and optional clamping, so observed error is not mathematically zero. The measured optimum is near tau under suitable idealized conditions.

## Domain representation

The core uses validated immutable records for geometry, plant parameters, pressure-advance parameters, simulation options, motion transitions, emitted samples, metrics, and sweep points. Invalid non-finite or out-of-range configuration is rejected at construction.

`SimulationSample` carries every motion, demand, drive, pressure, actual-flow, and signed-error value needed downstream. Reporting consumes those values directly and does not recompute hidden domain state.

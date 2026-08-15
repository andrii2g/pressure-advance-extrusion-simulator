# Metrics

## Error sign convention

The only signed flow-error definition is:

\[
e(t)=Q_a(t)-Q_r(t)
\]

Therefore:

- negative error = under-flow;
- positive error = over-flow.

Do not flip this sign in charts, JSON, logs, or tests.

## Trapezoidal integration

For sample pair `(t_i, y_i)` and `(t_{i+1}, y_{i+1})`:

\[
\int y(t)dt \approx \frac{y_i+y_{i+1}}{2}(t_{i+1}-t_i)
\]

Use this for IAFE, squared-error integral, under-volume, and over-volume.

## Integrated absolute flow error

\[
IAFE=\int|e(t)|dt
\]

Units: `mm³`.

This metric is the primary K-sweep objective.

## RMSE

\[
RMSE=\sqrt{\frac{1}{T}\int e(t)^2dt}
\]

Units: `mm³/s`.

If total duration is zero, reject the simulation rather than divide by zero.

## Peak values

Record:

- `minimumSignedError`;
- `maximumSignedError`;
- `peakUnderFlow = max(0, -minimumSignedError)`;
- `peakOverFlow = max(0, maximumSignedError)`.

Peak magnitudes are always non-negative.

## Directional volume error

Under-volume:

\[
V_{under}=\int max(0,-e(t))dt
\]

Over-volume:

\[
V_{over}=\int max(0,e(t))dt
\]

A useful invariant within numerical tolerance is:

\[
IAFE = V_{under}+V_{over}
\]

Add a test for this identity.

## Settling analysis

Settling is spatially important for printing, so report both time and distance.

For each transition marker:

1. obtain post-transition requested-flow reference;
2. compute tolerance:

\[
tol=max(0.02|Q_{post}|,0.02\ mm^3/s)
\]

3. scan samples at or after the transition;
4. candidate sample qualifies only if every subsequent sample covering the full hold window remains within `abs(error) <= tol`;
5. choose the earliest qualifying sample;
6. calculate:

\[
T_{settle}=t_{settled}-t_{transition}
\]

\[
D_{settle}=x_{settled}-x_{transition}
\]

Default hold window: `0.050 s`.

### Critical edge cases

Test:

- error exactly on tolerance boundary counts as in-band;
- a single out-of-band sample inside the hold window resets the candidate;
- simulation ending before the hold window completes means “unsettled/unavailable”;
- zero post-flow uses the absolute tolerance floor;
- a transition at the final sample cannot settle unless hold duration is zero, which the MVP should not allow.

## Aggregate settling fields

Recommended aggregate fields:

- transition count;
- settled transition count;
- unsettled transition count;
- maximum settling time among settled transitions;
- maximum settling distance among settled transitions;
- per-transition result list.

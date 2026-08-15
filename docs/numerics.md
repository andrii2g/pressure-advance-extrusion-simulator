# Numerical Integration Notes

## Explicit Euler

The simulator intentionally uses explicit Euler because the update is transparent:

\[
P_{n+1}=P_n+dt\frac{GQ_d-P_n}{\tau}
\]

For the homogeneous first-order system, numerical stability requires sufficiently small dt. The explicit update makes the effect of step size visible.

## Default step

Default:

- `dt = 0.001 s`;
- `tau = 0.040 s`.

This gives `dt/tau = 0.025`, which is comfortably small for the intended demonstration.

## Quality warning

The CLI warns when:

\[
dt > \tau/10
\]

This warning is a heuristic for accuracy, not the exact mathematical stability boundary.

The configuration remains runnable unless dt is otherwise invalid, allowing deliberate observation of poor integration.

## Endpoint step

If total profile duration is not an integer multiple of dt, use a final shorter step:

\[
dt_{final}=T-t_n
\]

Do not overshoot the profile duration and interpolate backward.

## Time-step convergence experiment

The analytical convergence test compares Euler response error at:

- 4 ms;
- 2 ms;
- 1 ms;
- 0.5 ms.

The test verifies decreasing analytical error as the step becomes smaller.

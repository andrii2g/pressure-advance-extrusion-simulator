# Numerical Integration Notes

## Explicit Euler

The MVP intentionally uses explicit Euler because the update is transparent:

\[
P_{n+1}=P_n+dt\frac{GQ_d-P_n}{\tau}
\]

For the homogeneous first-order system, numerical stability requires sufficiently small dt. The repository should teach that step size matters rather than hiding this fact behind a sophisticated integrator.

## Default step

Default:

- `dt = 0.001 s`;
- `tau = 0.040 s`.

This gives `dt/tau = 0.025`, which is comfortably small for the intended demonstration.

## Quality warning

The CLI should warn when:

\[
dt > \tau/10
\]

This warning is a heuristic for accuracy, not the exact mathematical stability boundary.

The configuration may still run unless dt is otherwise invalid, allowing users to observe poor integration deliberately.

## Endpoint step

If total profile duration is not an integer multiple of dt, use a final shorter step:

\[
dt_{final}=T-t_n
\]

Do not overshoot the profile duration and interpolate backward.

## Time-step convergence experiment

A test or optional developer command should compare metrics at:

- 4 ms;
- 2 ms;
- 1 ms;
- 0.5 ms.

The expected behavior is convergence, not bit-identical metrics.

## Future integrators

RK4 may be added later behind an `IIntegrator` abstraction only if there is a clear educational experiment comparing integration methods. Do not introduce it merely for abstraction completeness in the MVP.

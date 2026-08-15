# Architecture

## Runtime data flow

```mermaid
flowchart LR
    Scenario[Motion scenario] --> Motion[MotionProfile]
    Motion --> State[velocity v(t) and acceleration a(t)]
    Geometry[ExtrusionGeometry] --> Demand[ExtrusionDemandCalculator]
    State --> Demand
    Demand --> Requested[Requested flow Qr and derivative dQr/dt]
    Requested --> FF[Feed-forward controller]
    FF --> Drive[Drive command Qd]
    Drive --> Plant[FirstOrderExtrusionPlant]
    Plant --> Pressure[Nozzle pressure P]
    Pressure --> Actual[Actual flow Qa]
    Requested --> Sample[SimulationSample]
    Drive --> Sample
    Pressure --> Sample
    Actual --> Sample
    Sample --> Metrics[Metrics calculators]
    Sample --> Reporting[SVG / CSV / JSON]
```

## Dependency boundaries

```mermaid
flowchart TD
    Cli[PressureAdvance.Cli]
    Core[PressureAdvance.Core]
    Reporting[PressureAdvance.Reporting]
    CoreTests[PressureAdvance.Core.Tests]
    ReportingTests[PressureAdvance.Reporting.Tests]

    Cli --> Core
    Cli --> Reporting
    Reporting --> Core
    CoreTests --> Core
    ReportingTests --> Reporting
    ReportingTests --> Core
    ReportingTests --> Cli
```


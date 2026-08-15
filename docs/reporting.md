# Reporting Contract

## Output directories

Each command resolves one output directory and creates it when necessary. Reporting overwrites only the known artifact filenames inside that directory. It never recursively deletes a caller-supplied path.

A comparison contains the standard selected-K artifacts at its root, a `comparison.svg`, and full standard run artifacts in both subdirectories:

```text
artifacts/corner-compare/
├── no-pa/
│   └── standard run artifacts
├── selected-k/
│   └── standard run artifacts
├── run.json
├── metrics.json
├── samples.csv
├── speed.svg
├── pressure.svg
├── flow.svg
├── flow-error.svg
└── comparison.svg
```

## CSV

`samples.csv` is UTF-8, uses invariant numeric formatting, contains one row per emitted sample, and has this exact header:

```text
time_s,distance_mm,velocity_mm_s,acceleration_mm_s2,requested_flow_mm3_s,requested_flow_derivative_mm3_s2,advance_flow_mm3_s,raw_drive_flow_mm3_s,drive_flow_mm3_s,drive_clamped,nozzle_pressure,equilibrium_pressure,actual_flow_mm3_s,flow_error_mm3_s
```

Booleans are lowercase `true` or `false`. Non-finite report data is rejected.

## JSON

`run.json` contains:

- application and numerical-model identifiers;
- scenario name;
- resolved geometry, plant, and pressure-advance parameters;
- timestep and initial-pressure mode/value;
- total duration and distance;
- transition metadata;
- artifact filenames.

`metrics.json` contains all continuous, peak, directional-volume, clamp, and settling metrics. Enum values use stable names such as `ClampToZero`. JSON properties use camelCase.

## SVG engine

`SvgChart` is a narrowly scoped direct-SVG renderer. It provides aligned panels, axis scaling, invariant ticks, grid lines, legends, polylines, zero baselines, and vertical transition markers. Text is XML-escaped, and identical inputs produce identical SVG text. No chart package, HTML canvas, or JavaScript is used.

## Charts

### speed.svg

Two aligned distance-axis panels show velocity in `mm/s` and acceleration in `mm/s²`.

### flow.svg

The distance-axis flow panel shows requested, raw drive, final drive, and actual flow in `mm³/s`. Clamping is visible wherever raw and final drive diverge.

### pressure.svg

The distance-axis pressure panel shows nozzle pressure and requested-flow equilibrium pressure `G * Q_r`.

### flow-error.svg

The distance-axis panel shows signed error `Q_a - Q_r` with a zero baseline. Negative values mean under-flow; positive values mean over-flow.

### comparison.svg

The distance-axis panel shows requested flow, actual flow for K=0, and actual flow for the selected K. Both runs share the same resolved non-K configuration.

### k-sweep.svg

The K axis is in seconds and the Y axis is IAFE in `mm³`. Vertical markers identify the best grid K and configured tau.

## Robustness

Axis scaling handles constant series, all-zero values, negative values, tiny ranges, one-point series, and empty optional series without division by zero. Reports never contain `NaN` or `Infinity`. Every SVG has explicit width, height, viewBox, title, axis labels, units, and parseable XML structure.

# Reporting Specification

## Output directory behavior

The CLI should resolve one output directory per command invocation. It may create subdirectories for compared runs, but it must never recursively delete an arbitrary path supplied by the user.

Suggested compare layout:

```text
artifacts/corner-compare/
├── no-pa/
│   ├── run.json
│   ├── metrics.json
│   └── samples.csv
├── selected-k/
│   ├── run.json
│   ├── metrics.json
│   └── samples.csv
├── speed.svg
├── pressure.svg
├── flow.svg
├── flow-error.svg
└── comparison.svg
```

## CSV contract

Use invariant culture and a header row.

Recommended columns:

```text
time_s
distance_mm
velocity_mm_s
acceleration_mm_s2
requested_flow_mm3_s
requested_flow_derivative_mm3_s2
advance_flow_mm3_s
raw_drive_flow_mm3_s
drive_flow_mm3_s
drive_clamped
nozzle_pressure
equilibrium_pressure
actual_flow_mm3_s
flow_error_mm3_s
```

CSV escaping must be correct even though current headers are simple.

## JSON contract

`run.json` should contain:

- application/version info when easily available;
- scenario name;
- resolved geometry;
- resolved plant parameters;
- resolved PA parameters;
- dt;
- initial pressure mode/value;
- total duration/distance;
- transition metadata;
- artifact filenames.

`metrics.json` should contain all run and settling metrics.

For AOT friendliness, prefer source-generated serialization metadata if practical.

## SVG chart engine

Implement only what the project needs.

Suggested primitives:

- `SvgDocument`;
- `SvgChartLayout`;
- `SvgSeries`;
- `SvgPoint`;
- axis scaling helpers;
- tick generator;
- path/polyline writer;
- legend writer;
- transition marker writer.

Do not introduce HTML canvas, JavaScript, or a charting dependency.

## Required charts

### speed.svg

X axis: distance (`mm`).

Series:

- velocity (`mm/s`);
- acceleration (`mm/s²`).

Because units differ, either:

- use separate aligned panels in the same SVG, or
- use a clearly labeled secondary Y axis.

Prefer aligned panels for implementation simplicity and clarity.

### flow.svg

X axis: distance (`mm`).

Series:

- requested flow;
- raw/final drive flow (choose final as primary, raw optional dashed/secondary representation);
- actual flow.

Mark clamp intervals or clamp points when present.

### pressure.svg

X axis: distance (`mm`).

Series:

- nozzle pressure;
- requested-flow equilibrium pressure `G * Q_r`.

Optionally add drive equilibrium `G * Q_d` if it remains legible.

### flow-error.svg

X axis: distance (`mm`).

Series:

- signed error `Q_a - Q_r`.

Draw zero baseline whenever visible. Negative region means under-flow, positive region means over-flow. Text labels should explain the sign convention.

### comparison.svg

X axis: distance (`mm`).

Series:

- requested flow;
- actual flow for K=0;
- actual flow for selected K.

Use exactly the same scenario and plant parameters for both compared runs.

### k-sweep.svg

X axis: K (`s`).

Y axis: IAFE (`mm³`).

Mark the selected best K and, when convenient, the configured tau as an annotated vertical marker so users can visually compare them.

## SVG robustness

Axis code must handle:

- min == max;
- all zeros;
- negative values;
- very small ranges;
- one-point series;
- empty optional series;
- XML-reserved characters in labels.

Never emit `NaN`, `Infinity`, or scientific notation so extreme that labels become unreadable without a fallback formatter.

# SBCTrigger
## Overview

SBCTrigger is a Duet3D RepRapFirmware (RRF) plugin that provides advanced SBC-side triggers. Triggers evaluate object-model expressions and run G‑code actions when conditions are met.

## Status

Version: 0.0.1-preview.1  
See: https://github.com/marvineer98/SBCTrigger

## Configuration

SBCTrigger uses RRF trigger-style commands (M583.1) to define triggers. Each trigger includes a logical number, run condition, a state expression, and an action G‑code to execute.

## Trigger syntax

General form:
```
M583.1 T<n> R<r> P"<expression>" A"<gcode>"
```

Parameters:
- Tnn: Logical trigger number (starts at 0).
- R: Run condition:
    - R0 — Trigger at any time (default)
    - R1 — Only while printing from SD card
    - R2 — Only when not printing
    - R-1 — Temporarily disable the trigger
- P: Object-model expression evaluated to decide whether to trigger (e.g., state.status, heat.heaters[n].current).
- A: G-code action to run when the expression is true.

## Examples

Run a macro when the printer turns off while not printing:
```
M583.1 T1 R2 P"state.status==off" A'M98 P"/sys/callme.g"'
```

Light up LEDs if tool 0 temperature gets high:
```
M583.1 T2 R0 P"heat.heaters[1].current > 90" A"M150 R255 U0 B0 P255 S10"
```

Turn off LEDs when tool 0 temperature drops:
```
M583.1 T3 R0 P"heat.heaters[1].current < 90" A"M150 R0 U0 B0 P0 S10"
```

Beep whenever status changes from idle:
```
M583.1 T0 P"state.status != idle" A"M300 S3000 P150"
```

## Notes

- Expressions use the RRF object model; verify property names and indexes.
- Triggers can be set from files or the console.
- Actions are run on the SBC code stream, so they do not lock up movement while printing.

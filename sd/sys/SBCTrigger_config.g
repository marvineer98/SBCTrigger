; SBCTrigger config file
; Version 0.0.1-preview.1
; see https://github.com/marvineer98/SBCTrigger for further info
;
; How to define a trigger:
; M583.1
;  Parameters: (They are based on the documentation of RRF triggers)
;   Tnn: Logical trigger number to assign (starting from 0 up to a SBC-specific maximum).
;   R: Run condition:
;     R0 – Trigger at any time (default)
;     R1 – Only trigger while printing from SD card
;     R2 – Only trigger when not printing
;     R-1 – Temporarily disables the trigger (can be run in files, from console and so on)
;   P: Defines the object model state expression that will trigger the action
;   A: G-code action to run when the trigger condition is met
;
; Examples:
;   Run a macro when the printer turns off while not printing
;    M583.1 T1 R2 P"state.status==off" A'M98 P"/sys/callme.g"'
;
;   Light up LEDs if tool 0 temperature gets high
;    M583.1 T2 R0 P"heat.heaters[1].current > 90" A"M150 R255 U0 B0 P255 S10"
;   Turn off LEDs if tool 0 temperature gets low
;    M583.1 T3 R0 P"heat.heaters[1].current < 90" A"M150 R0 U0 B0 P0 S10" 
;
;   Whenever the status changes from idle to something else: beep
;    M583.1 T0 P"state.status != idle" A"M300 S3000 P150"
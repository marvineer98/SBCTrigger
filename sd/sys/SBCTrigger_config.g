; SBCTrigger config file
; Version 0.0.1-preview.3
; see https://github.com/marvineer98/SBCTrigger for further info
;
; Examples:
;   Run a macro when the printer turns off while not printing
;    M583 T1 R2 P"state.status==off" A'M98 P"/sys/callme.g"'
;
;   Light up LEDs if tool 0 temperature gets high
;    M583 T2 R0 P"heat.heaters[1].current > 90" A"M150 R255 U0 B0 P255 S10"
;   Turn off LEDs if tool 0 temperature gets low
;    M583 T3 R0 P"heat.heaters[1].current < 90" A"M150 R0 U0 B0 P0 S10" 
;
;   Whenever the status changes from idle to something else: beep
;    M583 T0 P"state.status != idle" A"M300 S3000 P150"
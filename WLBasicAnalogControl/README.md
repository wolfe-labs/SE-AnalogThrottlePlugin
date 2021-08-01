# WL Basic Analog Control Script

This script is a basic analog control script, showing what can be done and implemented using AnalogThrottle. It allows you to easily map your controllers under the Custom Data field of the Programmable Block, using the following syntax:

```
! Controller Name
@ Axis1 = Command1
@ Axis2 = Command2
```

Where the first character of a line (after any spaces) is always the *mode*. Anything that comes before `=` is considered the *target* of the command and anything after are the *arguments*, that can be separated by the `|` character.

## Available Modes

**Set Controller Name** - `!`

This mode sets the active controller where the following actions will be bound to. It has no arguments.

**Set Axis Command** - `@`

This mode allows you to listen to one of the controller's axis, sliders or buttons. The first argument is always the destination command inside the script, while the second argument usually is the transformation applied to the input.

Right now the following axis are available:

- `X`, `Y` and `Z` for the main axis of the controller. Normally `X` and `Y` will be your stick's main axis and `Z` will be your throttle stick's main control. Make sure you test which are those first.
- `RX`, `RY`, `RZ` for the rotation axis of the controller. Normally `RZ` will be your stick's yaw/rudder axis, while on some throttle stick they will be the rudder pads.
- `SN` where `N` is a number starting from `0`: maps to each of the controller's sliders (if any)
- `BN` where `N` is a number starting from `0`: maps to each of the controller's buttons (if any). Digital (binary) only!

These are the available commands on this script:

- `ThrustForward` applies thrust only in the forward direction
- `ThrustBackward` applies thrust only in the backward direction
- `ThrustLateral` applies thrust to the left and right directions and automatically converts your input data into a -1 to +1 range
- `ThrustVertical` applies thrust to the up and down directions and automatically converts your input data into a -1 to +1 range
- `Pitch` applies Pitch and Gyro Override to your gyroscopes, also automatically detects and takes into account your gyroscope orientations
- `Roll` applies Roll and Gyro Override to your gyroscopes, also automatically detects and takes into account your gyroscope orientations
- `Yaw` applies Yaw and Gyro Override to your gyroscopes, also automatically detects and takes into account your gyroscope orientations
- `GyroPower` controls the Power property of your gyroscopes, useful to link into one of your sliders

These are the available transformations:

- `Normal` that makes your `0...+1` range still be `0...+1`, used by default
- `Center` that makes your `0...+1` range become `-1...+1`, useful if you want to make your throttle stick apply reverse power - will still be implemented, though
- `Reverse` that makes your `0...+1` range become `+1...0`, useful if your throttle stick is reversed
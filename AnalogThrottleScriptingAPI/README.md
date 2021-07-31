# AnalogThrottle In-game Script API
🕹️ API for interacting with the AnalogThrottle plugin from your in-game scripts

**Note:** this is still a work in progress and possibly may change later, specially regarding the format data is passed down to scripts. Make sure to keep updated to prevent script bugs/crashes!

## Installation

Make sure you have the [AnalogThrottle](https://github.com/wolfe-labs/SE-AnalogThrottlePlugin) plugin installed first, as it is the bridge between DirectInput's reading of controller data and your script!

### Visual Studio

If you use Visual Studio for writing your scripts, you can use the Shared Project feature to reference it in your project and access its classes. You will need [MDK](https://github.com/malware-dev/MDK-SE) installed so that the compilation works correctly and the files are properly embedded on your script.

With that out of the way, after creating your MDK script, add the following entry to the top of your C# `Program.cs` file:

```cs
using IngameScript.WolfeLabs.AnalogThrottleAPI;
```

Finally, on your `Main` method, add the following line:

```cs
ControllerInputCollection inputs = ControllerInputCollection.FromString(argument);
```

With that done you should be ready to receive controller input directly in your script!

### Vanilla Usage

If you prefer not using an IDE or using MDK, you will need to import the two classes `ControllerInput` and `ControllerInputCollection` to your `Program.cs` file. With that done, make sure you add this line to your `Main` method:

```cs
ControllerInputCollection inputs = ControllerInputCollection.FromString(argument);
```

## Usage

Please keep in mind your Programmable Block **MUST** have the tag `[AnalogThrottle]` to work properly, otherwise the plugin will not communicate with it!

Now that you're receiving events on your script via the `Main()` method, you can process each of the different controller actions however you desire. This includes using joysticks to control slider-based values from any grid block, such as Thrusters, Pistons, Rotors, etc. This can be done by looping through the `ControllerInputCollection inputs` contents. Each entry will have the following properties available:

- `Source` - The name of the controller which sent that event
- `Axis` - The axis of that controller where the event happend
- `RawValue` - The raw value provided by the controller, as a `ushort`
- `AnalogValue` - The value above normalized into a range of 0 to 1, making it easier to use on sliders
- `DigitalValue` - The 1-bit digital representation of that value. Anything below `ushort.MaxValue` is `false`, anything above it is `true`
# AnalogThrottle Plugin for Space Engineers
🕹️ Plugin for Space Engineers that enables you to pipe your controller's analog (and digital) inputs into in-game script actions

**Note:** this is still a work in progress and possibly may change later, specially regarding the format data is passed down to scripts. Make sure to keep updated to prevent script bugs/crashes!

## Usage

This plugin ***does not*** provide any hooks into the game's movement system and is quite useless on its own, as it is intended to work instead with Programmable Blocks.

The idea behind that decision is both simplicity and the goal of providing ship builders extra flexibility on controlling their builds, like using the analog axis to control cranes, pistons, rotors, etc.

Please check the [Scripting API](https://github.com/wolfe-labs/SE-AnalogThrottleAPI) for more details.

Make sure your Programmable Block has the tag `[AnalogThrottle]` on it, otherwise the plugin **will not trigger it**

## How it works

Being a plugin instead of a mod or script, it has way more access to things outside of what the game engine exposes and also allows for usage anywhere, since it's only client-side.

In the inner workings, the plugin uses SharpDX, a wrapper for DirectX, to access DirectInput's devices, which includes your controller, be it a gamepad, joystick or HOTAS. All available devices are connected at beginning of a game session and updated in sync with the game engine's ticks, so we don't overload the game with requests or anything.

After new controller data is fetched, it is compared with the previous state and only the changes are then combined and sent to the Programmable Block. This ensures fewer calls to the script and potentially less performance issues.

## Development and Build from source

To keep things as compatible and in sync as possible between the Plugin and Scripts, both use the same Scripting API. That API contains any shared classes along with methods to serialize and deserialize data.

When developing or building this project from its source, please keep in mind you include the Scripting API submodule by using the cloning the repository using these two extra flags: `git clone --recurse-submodules --remote-submodules` as that will also download the latest version of the API for you and it will be included into the Visual Studio Solution. From there you can work normally as you normally would.
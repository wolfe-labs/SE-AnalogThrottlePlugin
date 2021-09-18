# AnalogThrottle Plugin for Space Engineers
🕹️ Plugin for Space Engineers that enables you to pipe your controller's analog (and digital) inputs into in-game script actions

**Note:** this is still a work in progress and possibly may change later, specially regarding the format data is passed down to scripts. Make sure to keep updated to prevent script bugs/crashes!

## Usage

This plugin ***does not*** provide any hooks into the game's movement system and is quite useless on its own, as it is intended to work instead with Programmable Blocks.

The idea behind that decision is both simplicity and the goal of providing ship builders extra flexibility on controlling their builds, like using the analog axis to control cranes, pistons, rotors, etc.

Please check the [Scripting API](AnalogThrottleScriptingAPI/) for more details.

Make sure your Programmable Block has the tag `[AnalogThrottle]` on it, otherwise the plugin **will not trigger it**

## Does it work on multiplayer? Does it require anything installed on the server?

The plugin is compatible with multiplayer and doesn't require anything installed on the server, with the only requirement being that the server allows you to create and use Programmable Blocks and scripts.

Any processing regarding the raw input of your controllers is done client-side and converted to a common format that is then read by the script on the Programmable Block, which is running on the server. Read more below for a detailed view on how it works.

**Note:** In non-local sessions (aka multiplayer) the plugin will automatically throttle the amount of updates sent to the server to around a third of the normal amount, so it doesn't overuse bandwidth or cause lag due to Programmable Block processing.

## How it works

Being a plugin instead of a mod or script, it has way more access to things outside of what the game engine exposes and also allows for usage anywhere, since it's only client-side.

In the inner workings, the plugin uses SharpDX, a wrapper for DirectX, to access DirectInput's devices, which includes your controller, be it a gamepad, joystick or HOTAS. All available devices are connected at beginning of a game session and updated in sync with the game engine's ticks, so we don't overload the game with requests or anything.

After new controller data is fetched, it is compared with the previous state and only the changes are then combined and sent to the Programmable Block. This ensures fewer calls to the script and potentially less performance issues.

## Script Development

To keep things as compatible and in sync as possible between the Plugin and Scripts, both use the same Scripting API. That API contains any shared classes along with methods to serialize and deserialize data.

The shared API is present on the [AnalogThrottleScriptingAPI](AnalogThrottleScriptingAPI/) directory of this project, along with instructions on how to use it on your scripts.

## Sample Scripts

A repository containing some sample Scripts to be used with this plugin is available [here](https://github.com/wolfe-labs/SE-AnalogThrottleScripts). It used to be bundled with the Plugin but was removed so that only plugin-related stuff is included here.
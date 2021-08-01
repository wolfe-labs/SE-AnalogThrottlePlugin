using IngameScript.WolfeLabs.AnalogThrottleAPI;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.

        List<IMyTextPanel> GridPanels = new List<IMyTextPanel>();
        List<IMyThrust> GridThrusters = new List<IMyThrust>();
        Dictionary<string, ControllerInput> Axis = new Dictionary<string, ControllerInput>();

        public string GetAxisID (ControllerInput axis)
        {
            return $"{ axis.Source }.{ axis.Axis }";
        }

        public string GetAxisValue (ControllerInput axis)
        {
            return $"{ string.Format("{0:#.000}", axis.AnalogValue) } | { string.Format("{0:#.000}", 2 * axis.AnalogValue - 1) } | { axis.RawValue }";
        }

        public Program ()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.

            this.GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(this.GridPanels);
            this.GridTerminalSystem.GetBlocksOfType<IMyThrust>(this.GridThrusters);

            ControllerInputCollection storedInput = ControllerInputCollection.FromString(this.Me.CustomData);
            foreach (ControllerInput input in storedInput) {
                this.Axis.Add(this.GetAxisID(input), input);
            }
        }

        public void Save ()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        public void Main (string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.

            // Only runs the code below on "user-triggered" events => from plugin
            if (0 != (updateSource & UpdateType.Mod) && argument.Length > 0) {
                // By default our throttle change is off
                float throttlePercentage = -1;

                // Tries to get a list of changes from argument
                ControllerInputCollection args = ControllerInputCollection.FromString(argument);

                // If any input is present, does processing
                if (args.Count > 0) {
                    string zAxisId = null;
                    foreach (ControllerInput input in args) {
                        // Now, generate the Axis ID that will identify our current position in the stored data
                        string axisId = this.GetAxisID(input);

                        // Updates the Axis dictionary to have up-to-date data
                        if (this.Axis.ContainsKey(axisId)) {
                            this.Axis[axisId] = input;
                        } else {
                            this.Axis.Add(axisId, input);
                        }

                        // Stores Z-Axis ID for later usage
                        if ("Z" == input.Axis)
                            zAxisId = axisId;
                    }

                    // Stores updated data
                    this.Me.CustomData = ControllerInputCollection.FromArray(Axis.Values.ToArray()).ToString();

                    // Handles throttle changes from Z axis, sends it to thrusters
                    if (null != zAxisId) {
                        throttlePercentage = 1.0f - Axis[zAxisId].AnalogValue;
                        foreach (IMyThrust thruster in this.GridThrusters) {
                            // Only affect forward thrusters
                            if (thruster.GridThrustDirection == VRageMath.Vector3I.Backward)
                                thruster.ThrustOverridePercentage = throttlePercentage;
                        }
                    }
                }

                // Prepares the output that will go to screens, etc.
                string output = "";

                // Prints each of the stored axis and inputs
                foreach (KeyValuePair<string, ControllerInput> storedRow in this.Axis) {
                    output += $"{ storedRow.Key } = { this.GetAxisValue(storedRow.Value) }\n";
                }

                // Does some quick sorting on the output
                string[] outputParts = output.Split('\n');
                Array.Sort(outputParts);
                output = string.Join("\n", outputParts).Trim();

                // Debugs throttle changes too
                if (throttlePercentage >= 0) {
                    output = $"Thruster Override: { string.Format("{0:000.000}", 100 * throttlePercentage) }%\n{ output }";
                }

                // Finally prints the output
                foreach (IMyTextPanel panel in this.GridPanels) {
                    if (panel.CustomName.Contains("[AnalogThrottle.Debug]")) {
                        panel.WriteText(output, false);
                    }
                }
            }
        }
    }
}
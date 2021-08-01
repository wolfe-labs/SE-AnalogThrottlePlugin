using IngameScript.WolfeLabs.AnalogThrottleAPI;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        // List of valid axis types
        private readonly string[] AXIS_TYPES = new string[] { "Normal", "Center", "Reverse" };
        private readonly string[] AXIS_DIRECTIONS = new string[] { "Normal", "Reverse" };

        // The minimum value to actuate an axis
        private readonly float DELTA_MIN = 0.005f;

        // This will contain ALL possible commands of this script
        private Dictionary<string, Action<ControllerInput, string[]>> availableCommands = new Dictionary<string, Action<ControllerInput, string[]>>();

        // This will provide a mapping of Controller-Axis to one of the script's Actions
        private Dictionary<string, Action<ControllerInput, string[]>> shipCommands = new Dictionary<string, Action<ControllerInput, string[]>>();
        private Dictionary<string, string[]> shipCommandsOptions = new Dictionary<string, string[]>();


        // This will store Gyroscope information, including Matrices indicating how they are rotated to provide proper Pitch/Roll/Yaw values
        private List<IMyGyro> shipGyroscopes = new List<IMyGyro>();
        private Dictionary<long, Vector3I[]> shipGyroscopesTransforms = new Dictionary<long, Vector3I[]>();

        // This will store Thruster information
        private List<IMyThrust> shipThrusters = new List<IMyThrust>();

        // This will store the Main Cockpit
        private IMyCockpit shipMainCockpit = null;

        // This is our grid's Main Direction
        Vector3D shipMainOrientationForward;
        Vector3D shipMainOrientationRight;
        Vector3D shipMainOrientationUp;

        private string GetCommandKey (string controller, string axis)
        {
            return $"{ axis }.{ controller }";
        }

        private float GetAxisValueByZero (float inputValue, string zero)
        {
            switch (zero) {
                case "Center": return (2.0f * inputValue) - 1.0f;
                case "Reverse": return 1.0f - inputValue;
                case "Normal":
                default:
                    return inputValue;
            }
        }

        private void InvokeCommandFromInput (ControllerInput input)
        {
            // Gets command key first
            string commandKey = this.GetCommandKey(input.Source, input.Axis);

            // Invokes the command
            if (this.shipCommands.ContainsKey(commandKey))
                this.shipCommands[commandKey](input, this.shipCommandsOptions[commandKey]);
        }

        private string GetArg (string[] args, int position, string defaultOption, string[] validOptions = null)
        {
            // Less than needed args
            if (args.Length <= position)
                return defaultOption;

            // Gets value
            string value = args[position];

            // Checks for valid option
            if (null != validOptions && !validOptions.Contains(value))
                return defaultOption;

            // Now returns the validated value
            return value;
        }

        private Vector3I[] GetLocalRotation (MatrixD blockWorldMatrix)
        {
            // Temporary double-precision vectors, I really **wish** those were Vector3I instead but the WorldMatrix is a double-precision number. Oh well...
            Vector3D doubleForward = Vector3D.TransformNormal(this.shipMainOrientationForward, MatrixD.Transpose(blockWorldMatrix));
            Vector3D doubleRight = Vector3D.TransformNormal(this.shipMainOrientationRight, MatrixD.Transpose(blockWorldMatrix));
            Vector3D doubleUp = Vector3D.TransformNormal(this.shipMainOrientationUp, MatrixD.Transpose(blockWorldMatrix));

            // Does the conversion to Vector3I
            return new Vector3I[] {
                new Vector3I((int)Math.Round(doubleRight.X), (int)Math.Round(doubleRight.Y), (int)Math.Round(doubleRight.Z)),
                new Vector3I((int)Math.Round(doubleUp.X), (int)Math.Round(doubleUp.Y), (int)Math.Round(doubleUp.Z)),
                new Vector3I((int)Math.Round(doubleForward.X), (int)Math.Round(doubleForward.Y), (int)Math.Round(doubleForward.Z)),
            };
        }

        private int GetVectorComponentSum (Vector3I vec)
        {
            return vec.X + vec.Y + vec.Z;
        }

        private void Reload ()
        {
            // Cleanup existing settings
            this.shipCommands.Clear();
            this.shipGyroscopes.Clear();
            this.shipThrusters.Clear();

            // Loads the ship parts such as Gyroscopes, Thrusters, etc, along with any settings
            this.GridTerminalSystem.GetBlocksOfType(this.shipGyroscopes);
            this.GridTerminalSystem.GetBlocksOfType(this.shipThrusters);

            // Finds the Main Cockpit
            List<IMyCockpit> availableCockpits = new List<IMyCockpit>();
            this.GridTerminalSystem.GetBlocksOfType(availableCockpits);
            foreach (IMyCockpit cockpit in availableCockpits) {
                if (cockpit.IsMainCockpit) {
                    this.shipMainCockpit = cockpit;
                }
            }

            // Warns if no Main Cockpit is set then extract the WorldMatrix of what we'll consider the orientation referece
            MatrixD currentOrientation;
            if (null == this.shipMainCockpit) {
                Echo("Warning: No Main Cockpit set! Using orientations from Programmable Block.");
                currentOrientation = this.Me.WorldMatrix;
            } else {
                currentOrientation = this.shipMainCockpit.WorldMatrix;
            }

            // Calculates the Forward, Right and Up main vectors to be able to calculate local rotation later
            this.shipMainOrientationForward = Vector3D.TransformNormal(Vector3D.Forward, currentOrientation);
            this.shipMainOrientationRight = Vector3D.TransformNormal(Vector3D.Right, currentOrientation);
            this.shipMainOrientationUp = Vector3D.TransformNormal(Vector3D.Up, currentOrientation);

            // Finds the proper rotation of Gyroscopes
            foreach (IMyGyro gyro in this.shipGyroscopes) {
                this.shipGyroscopesTransforms.Add(gyro.EntityId, this.GetLocalRotation(gyro.WorldMatrix));
            }

            // Loads options from the Custom Data field
            string controller = "";
            string[] commands = this.Me.CustomData.Split('\n');
            foreach (string command in commands) {
                // Clear any spaces around the line
                string line = command.Trim();

                // Only processes non-empty lines
                if (line.Length > 0) {
                    // Separates the parts of that line
                    char operation = line[0];
                    string[] parts = line.Substring(1).Trim().Split('=');
                    string variable = parts[0].Trim();
                    string[] args = (parts.Length > 1 ? parts[1].Trim().Split('|') : new string[] { })
                            .Select((string part) => part.Trim())
                            .ToArray();

                    // Only processes on valid operations
                    switch (operation) {
                        // Defines current controller
                        case '!':
                            controller = variable;
                            break;

                        // Defines a controller axis
                        case '@':
                            // Checks for existing controller first
                            if (0 == controller.Length)
                                throw new Exception("Add a controller using '! Controller Name' before adding any actions!");

                            // Gets the axis name
                            string cmd = args[0];

                            // Checks if action exists
                            if (!this.availableCommands.ContainsKey(cmd))
                                throw new Exception($"Invalid mode '{ cmd }' for axis '{ variable }' on controller { controller }");

                            // Binds action if not already set
                            string commandKey = this.GetCommandKey(controller, variable);
                            if (this.shipCommands.ContainsKey(commandKey))
                                throw new Exception($"Command already set for axis '{ variable }' on controller '{ controller }'");
                            this.shipCommands.Add(commandKey, this.availableCommands[cmd]);
                            this.shipCommandsOptions.Add(commandKey, args);
                            break;
                    }
                }
            }
        }

        public Program ()
        {
            // This makes sure our script will only ever fire on actual controller input
            Runtime.UpdateFrequency = UpdateFrequency.None;

            // Links command names to their respective Actions
            this.availableCommands.Add("ThrustForward", this.MakeSetThrust(Vector3I.Forward));
            this.availableCommands.Add("ThrustBackward", this.MakeSetThrust(Vector3I.Backward));
            this.availableCommands.Add("ThrustLateral", this.MakeSetThrustCentered(Vector3I.Left, Vector3I.Right));
            this.availableCommands.Add("ThrustVertical", this.MakeSetThrustCentered(Vector3I.Down, Vector3I.Up));
            this.availableCommands.Add("Pitch", this.MakeSetGyros(Vector3I.Right));
            this.availableCommands.Add("Roll", this.MakeSetGyros(Vector3I.Forward));
            this.availableCommands.Add("Yaw", this.MakeSetGyros(Vector3I.Up));
            this.availableCommands.Add("GyroPower", this.MakeSetGyroPower());

            // Setup
            this.Reload();
        }

        public void Main (string argument, UpdateType updateSource)
        {
            // Makes sure we're dealing with an actual input
            if (UpdateType.Mod == updateSource && argument.Length > 0) {
                // Tries parsing it
                ControllerInputCollection inputs = ControllerInputCollection.FromString(argument);

                // Does actions on it
                foreach (ControllerInput input in inputs) {
                    // Invokes command for that input
                    this.InvokeCommandFromInput(input);
                }
            }
        }

        /// <summary>
        /// Generates a function to set Thrust Override on the ship with only one possible direction
        /// </summary>
        /// <param name="direction">The direction of desired thrust</param>
        /// <returns>Action that sets the Thrust Override on the ship</returns>
        public Action<ControllerInput, string[]> MakeSetThrust (Vector3I direction)
        {
            // For some reason we need to invert this so that Forward means thrust Forward
            Vector3I gridThrustDirection = -direction;

            return (ControllerInput input, string[] args) => {
                foreach (IMyThrust thruster in this.shipThrusters) {
                    // Skips inactive thrusters
                    if (!thruster.Enabled)
                        continue;

                    // If the direction matches, set thrust
                    if (thruster.GridThrustDirection == gridThrustDirection)
                        thruster.ThrustOverridePercentage = Math.Max(0, this.GetAxisValueByZero(input.AnalogValue, this.GetArg(args, 1, "Normal", this.AXIS_TYPES)));
                }
            };
        }

        /// <summary>
        /// Generates a function to set Thrust Override on the ship with two possible directions
        /// </summary>
        /// <param name="direction">The direction of desired thrust</param>
        /// <returns>Action that sets the Thrust Override on the ship</returns>
        public Action<ControllerInput, string[]> MakeSetThrustCentered (Vector3I directionNegative, Vector3I directionPositive)
        {
            // For some reason we need to invert this so that Forward means thrust Forward
            Vector3I gridThrustDirectionNegative = -directionNegative;
            Vector3I gridThrustDirectionPositive = -directionPositive;

            return (ControllerInput input, string[] args) => {
                // Gets the directional input (corrects when on reverse axis, etc)
                float inputDirectional = this.GetAxisValueByZero(input.AnalogValue, this.GetArg(args, 1, "Normal", AXIS_DIRECTIONS));

                // Gets the centered value of that directional input
                float value = this.GetAxisValueByZero(inputDirectional, "Center");

                // Finally, gets the actual percentage to send to thrusters
                float thrust = Math.Abs(value);

                // Determines the correct direction
                Vector3I direction = Vector3I.Zero;
                if (value < 0)
                    direction = gridThrustDirectionNegative;
                if (value > 0)
                    direction = gridThrustDirectionPositive;

                foreach (IMyThrust thruster in this.shipThrusters) {
                    // Handles cases of zero thrust = zero everything
                    if (thrust < this.DELTA_MIN && (thruster.GridThrustDirection == direction || thruster.GridThrustDirection == direction)) {
                        thruster.ThrustOverridePercentage = 0.0f;
                    } else {
                        // Applies thrust to desired direction
                        if (thruster.GridThrustDirection == direction)
                            thruster.ThrustOverridePercentage = thrust;

                        // Remove thrust from reverse direction
                        if (thruster.GridThrustDirection == -direction)
                            thruster.ThrustOverridePercentage = 0.0f;
                    }
                }
            };
        }

        /// <summary>
        /// Generates a function to set Gyroscope axis on the ship around a certain axis
        /// </summary>
        /// <param name="direction">The direction of desired thrust</param>
        /// <returns>Action that sets the Gyroscope Override on the ship</returns>
        public Action<ControllerInput, string[]> MakeSetGyros (Vector3I axis)
        {
            return (ControllerInput input, string[] args) => {
                // Gets the directional input (corrects when on reverse axis, etc)
                float inputDirectional = this.GetAxisValueByZero(input.AnalogValue, this.GetArg(args, 1, "Normal", AXIS_DIRECTIONS));

                // Centers value first
                float value = 6 * this.GetAxisValueByZero(inputDirectional, "Center");

                // Small values correct to zero!
                if (Math.Abs(value) < this.DELTA_MIN)
                    value = 0;

                // Now pass it down to the gyroscopes
                // TODO: Find out which gyro should be activated accordingly to their orientation
                foreach (IMyGyro gyro in this.shipGyroscopes) {
                    // Gets the Gyro transform data
                    Vector3I[] gyroTransforms = this.shipGyroscopesTransforms[gyro.EntityId];

                    // We need to enable Gyro Override first to set these values, but only do that if we have any input
                    if (Math.Abs(value) > 0) {
                        gyro.GyroOverride = true;
                    }

                    // Temporary Pitch, Roll and Yaw values
                    float tPitch = 0f,
                        tRoll = 0f,
                        tYaw = 0f;

                    // Sets temporary axis values
                    if (axis == Vector3I.Right)
                        tPitch = this.GetVectorComponentSum(Vector3I.Right);
                    else if (axis == Vector3I.Forward)
                        tRoll = this.GetVectorComponentSum(Vector3I.Forward);
                    else if (axis == Vector3I.Up)
                        tYaw = this.GetVectorComponentSum(Vector3I.Up);

                    // These second values are now used to calculate the proper axis
                    float rPitch = tPitch * gyroTransforms[0].X + tYaw * gyroTransforms[1].X + tRoll * gyroTransforms[2].X,
                        rYaw = tPitch * gyroTransforms[0].Y + tYaw * gyroTransforms[1].Y + tRoll * gyroTransforms[2].Y,
                        rRoll = tPitch * gyroTransforms[0].Z + tYaw * gyroTransforms[1].Z + tRoll * gyroTransforms[2].Z;

                    // Finally, we set the actual correct axis
                    if (0 != rPitch)
                        gyro.Pitch = value * rPitch;
                    else if (0 != rRoll)
                        gyro.Roll = value * rRoll;
                    else if (0 != rYaw)
                        gyro.Yaw = value * rYaw;

                    // Determines state of Gyro Override
                    if (gyro.Pitch + gyro.Roll + gyro.Yaw == 0) {
                        gyro.GyroOverride = false;
                    }
                }
            };
        }

        /// <summary>
        /// Generates a function to set Gyroscope power around an axis
        /// </summary>
        /// <returns>Action that sets the Gyroscope Power on the ship</returns>
        public Action<ControllerInput, string[]> MakeSetGyroPower ()
        {
            return (ControllerInput input, string[] args) => {
                // Gets the directional input (corrects when on reverse axis, etc)
                float inputDirectional = this.GetAxisValueByZero(input.AnalogValue, this.GetArg(args, 1, "Normal", AXIS_DIRECTIONS));

                // Sets gyro power
                foreach (IMyGyro gyro in this.shipGyroscopes) {
                    gyro.GyroPower = inputDirectional;
                }
            };
        }
    }
}
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
        private float DEAD_ZONE = 0.05f;

        // Input curve exponents
        private readonly double INPUT_EXP_THRUST = 1.2;

        private readonly double INPUT_EXP_GYRO = 1.8;
        private readonly double INPUT_EXP_FLAT = 1;

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

        private List<IMyThrust> shipLiftThrusters = new List<IMyThrust>();

        // Notes:
        // Maximum effective thrust depends on atmospheric density for Ion and Atmospheric type thrusters
        // Maximum mass depends on maximum effective thrust and current gravity
        // Thrust and mass values must be recalculated periodically to keep up with environment and grid changes

        // Effective gravity, default to 1G
        private float gravity = 9.806f;

        // Maximum mass that we can lift in current gravity and atmospheric density
        private float maxShipMass = 0;

        private float currentShipMass = 0;

        // Minimum thrust necessary to float given current weight, gravity and atmospheric density
        private float baselineLiftThrust = 0;
        private float baselineDelta = 0;

        // Maximum available lift thrust given current atmospheric density
        private float availableLiftThrust = 0;

        // This will store the Main Cockpit
        private IMyCockpit shipCockpit = null;

        // This is our grid's Main Direction
        private Vector3D shipMainOrientationForward;

        private Vector3D shipMainOrientationRight;
        private Vector3D shipMainOrientationUp;

        private string GetCommandKey(string controller, string axis)
        {
            return $"{ axis }.{ controller }";
        }

        private float GetAxisValueByZero(float inputValue, string zero)
        {
            switch (zero)
            {
                case "Center": return (2.0f * inputValue) - 1.0f;
                case "Reverse": return 1.0f - inputValue;
                case "Normal":
                default:
                    return inputValue;
            }
        }

        private void InvokeCommandFromInput(ControllerInput input)
        {
            // Gets command key first
            string commandKey = this.GetCommandKey(input.Source, input.Axis);

            // Invokes the command
            if (this.shipCommands.ContainsKey(commandKey))
                this.shipCommands[commandKey](input, this.shipCommandsOptions[commandKey]);
        }

        private string GetArg(string[] args, int position, string defaultOption, string[] validOptions = null)
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

        private Vector3I[] GetLocalRotation(MatrixD blockWorldMatrix)
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

        private int GetVectorComponentSum(Vector3I vec)
        {
            return vec.X + vec.Y + vec.Z;
        }

        private float PlotOnExpCurve(float input, float min, float max, double exp)
        {
            float factor = (float)Math.Pow(Math.Abs(input) / max, exp);
            float scaled = min + ((max - min) * input / max * factor);

            Echo($"orig: {input:F3}");
            Echo($"exponent: {exp:F3}");
            Echo($"factor: {factor:F3}");
            Echo($"scaled: {scaled:F3}");
            return (scaled);
        }

        private void UpdateThrustMass()
        {
            if (this.shipCockpit == null)
            {
                Echo("No cockpit detected. Set one as main.");
                return;
            }

            // Update total effective lifting thrust
            this.availableLiftThrust = 0;
            foreach (IMyThrust thruster in this.shipLiftThrusters)
            {
                // Filter out disabled, broken, etc
                if (thruster.IsWorking)
                    this.availableLiftThrust += thruster.MaxEffectiveThrust;
            }

            this.currentShipMass = this.shipCockpit.CalculateShipMass().TotalMass;
            this.gravity = (float)this.shipCockpit.GetNaturalGravity().Length();
            if (this.gravity > 0)
            {
                this.baselineLiftThrust = this.currentShipMass * this.gravity;
                this.baselineDelta = this.baselineLiftThrust / this.availableLiftThrust;
                this.maxShipMass = this.availableLiftThrust / this.gravity;
            }
            else
            {
                this.baselineLiftThrust = 0;
                this.baselineDelta = 0;
                this.maxShipMass = 0;
            }
        }

        private void Reload()
        {
            // Cleanup existing settings
            this.shipCommands.Clear();
            this.shipGyroscopes.Clear();
            this.shipThrusters.Clear();
            this.shipLiftThrusters.Clear();

            // Loads the ship parts such as Gyroscopes, Thrusters, etc, along with any settings
            this.GridTerminalSystem.GetBlocksOfType(this.shipGyroscopes);
            this.GridTerminalSystem.GetBlocksOfType(this.shipThrusters);

            // Finds the Main Cockpit
            List<IMyCockpit> shipControllers = new List<IMyCockpit>();
            this.GridTerminalSystem.GetBlocksOfType(shipControllers);
            this.shipCockpit = shipControllers.First();

            MatrixD currentOrientation;
            currentOrientation = this.shipCockpit.WorldMatrix;

            // Calculates the Forward, Right and Up main vectors to be able to calculate local rotation later
            this.shipMainOrientationForward = Vector3D.TransformNormal(Vector3D.Forward, currentOrientation);
            this.shipMainOrientationRight = Vector3D.TransformNormal(Vector3D.Right, currentOrientation);
            this.shipMainOrientationUp = Vector3D.TransformNormal(Vector3D.Up, currentOrientation);

            // Finds the proper rotation of Gyroscopes
            foreach (IMyGyro gyro in this.shipGyroscopes)
                this.shipGyroscopesTransforms.Add(gyro.EntityId, this.GetLocalRotation(gyro.WorldMatrix));

            // Lift thrusters
            foreach (IMyThrust thruster in shipThrusters)
                if (thruster.Orientation.Forward == Base6Directions.GetFlippedDirection(this.shipCockpit.Orientation.Up))
                    this.shipLiftThrusters.Add(thruster);

            // Loads options from the Custom Data field
            string controller = "";
            string[] commands = this.Me.CustomData.Split('\n');
            foreach (string command in commands)
            {
                // Clear any spaces around the line
                string line = command.Trim();

                // Only processes non-empty lines
                if (line.Length > 0)
                {
                    // Separates the parts of that line
                    char operation = line[0];
                    string[] parts = line.Substring(1).Trim().Split('=');
                    string variable = parts[0].Trim();
                    string[] args = (parts.Length > 1 ? parts[1].Trim().Split('|') : new string[] { })
                            .Select((string part) => part.Trim())
                            .ToArray();

                    // Only processes on valid operations
                    switch (operation)
                    {
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

            Echo("Setup complete.");
        }

        public Program()
        {
            // This makes sure our script will only ever fire on actual controller input
            Runtime.UpdateFrequency = UpdateFrequency.Update100 | UpdateFrequency.Update1;

            // Links command names to their respective Actions
            this.availableCommands.Add("ThrustForward", this.MakeSetThrust(Vector3I.Forward));
            this.availableCommands.Add("ThrustBackward", this.MakeSetThrust(Vector3I.Backward));
            this.availableCommands.Add("ThrustLateral", this.MakeSetThrustCentered(Vector3I.Left, Vector3I.Right));
            this.availableCommands.Add("ThrustVertical", this.MakeSetThrustCentered(Vector3I.Down, Vector3I.Up));
            this.availableCommands.Add("ThrustHorizontal", this.MakeSetThrustCentered(Vector3I.Forward, Vector3I.Backward));
            this.availableCommands.Add("Pitch", this.MakeSetGyros(Vector3I.Left));
            this.availableCommands.Add("Roll", this.MakeSetGyros(Vector3I.Backward));
            this.availableCommands.Add("Yaw", this.MakeSetGyros(Vector3I.Down));
            this.availableCommands.Add("GyroPower", this.MakeSetGyroPower());

            // Setup
            this.Reload();
        }

        public void Main(string argument, UpdateType updateType)
        {
            // Update thrust and mass values every 100 ticks
            if ((updateType & UpdateType.Update100) != 0)
                this.UpdateThrustMass();

            // Makes sure we're dealing with an actual input
            if (argument.Length > 0)
            {
                // Tries parsing it
                ControllerInputCollection inputs = ControllerInputCollection.FromString(argument);

                // Does actions on it
                foreach (ControllerInput input in inputs)
                {
                    // Invokes command for that input
                    this.InvokeCommandFromInput(input);
                }
            }
            else
            {
                Echo("input event not triggered");
            }

            if (this.DEBUG)
            {
                // Print thrust and mass stats
                Echo($"Gravity: {this.gravity}m/s/s");
                Echo($"available lift: {(this.availableLiftThrust / 1000.0):F2}kN");
                Echo($"baseline lift: {(this.baselineLiftThrust / 1000.0):F2}kN");
                Echo($"baseline delta: {this.baselineDelta * 100}%");
                Echo($"current mass: {this.currentShipMass}Kg");
                Echo($"max mass: {this.maxShipMass}Kg");
            }
        }

        /// <summary>
        /// Generates a function to set Thrust Override on the ship with only one possible direction
        /// </summary>
        /// <param name="direction">The direction of desired thrust</param>
        /// <returns>Action that sets the Thrust Override on the ship</returns>
        public Action<ControllerInput, string[]> MakeSetThrust(Vector3I direction)
        {
            // For some reason we need to invert this so that Forward means thrust Forward
            Vector3I gridThrustDirection = -direction;

            // All thrusters that push in the desired direction
            List<IMyThrust> directionalThrusters = this.shipThrusters.FindAll((t) => t.GridThrustDirection == gridThrustDirection);

            // Mark lift function
            bool isLift = gridThrustDirection == Vector3I.Up;

            return (ControllerInput input, string[] args) =>
            {
                float inputValue;
                float thrustOverride;

                foreach (IMyThrust thruster in directionalThrusters)
                {
                    // Skips inactive thrusters
                    if (!thruster.IsWorking)
                        continue;

                    inputValue = this.GetAxisValueByZero(input.AnalogValue, this.GetArg(args, 1, "Normal", this.AXIS_TYPES));
                    thrustOverride = this.PlotOnExpCurve(inputValue, 0, 1, INPUT_EXP_THRUST);
                    thruster.ThrustOverridePercentage = Math.Max(0, thrustOverride);
                }
            };
        }

        /// <summary>
        /// Generates a function to set Thrust Override on the ship with two possible directions
        /// </summary>
        /// <param name="direction">The direction of desired thrust</param>
        /// <returns>Action that sets the Thrust Override on the ship</returns>
        // TODO: Account for baseline lift thrust
        public Action<ControllerInput, string[]> MakeSetThrustCentered(Vector3I directionNegative, Vector3I directionPositive)
        {
            // For some reason we need to invert this so that Forward means thrust Forward
            Vector3I gridThrustDirectionNegative = -directionNegative;
            Vector3I gridThrustDirectionPositive = -directionPositive;
            bool isLift = directionNegative == Vector3I.Up || directionPositive == Vector3I.Up;

            return (ControllerInput input, string[] args) =>
            {
                // Gets the directional input (corrects when on reverse axis, etc)
                float inputDirectional = this.GetAxisValueByZero(input.AnalogValue, this.GetArg(args, 1, "Normal", AXIS_DIRECTIONS));
                Echo($"Axis: {input.Axis}");

                // Gets the centered value of that directional input
                float value = this.GetAxisValueByZero(inputDirectional, "Center");
                bool isZeroInput = Math.Abs(value) < this.DEAD_ZONE;

                // Plot input on a simple curve
                value = this.PlotOnExpCurve(value, 0, 1, INPUT_EXP_THRUST);

                // Account for gravity when lifting
                if (isLift)
                    value += this.baselineDelta;

                // Determines the correct direction
                Vector3I direction = Vector3I.Zero;
                if (value < 0)
                    direction = gridThrustDirectionNegative;
                if (value > 0)
                    direction = gridThrustDirectionPositive;

                foreach (IMyThrust thruster in this.shipThrusters)
                {
                    // Handles cases of zero thrust = zero everything
                    if (isZeroInput && 
                         (thruster.GridThrustDirection == direction || thruster.GridThrustDirection == -direction))
                        thruster.ThrustOverridePercentage = 0f;

                    else
                    {
                        // Applies thrust to desired direction
                        if (thruster.GridThrustDirection == direction)
                            thruster.ThrustOverridePercentage = Math.Abs(value);

                        // Remove thrust from reverse direction
                        if (thruster.GridThrustDirection == -direction)
                            thruster.ThrustOverridePercentage = 0f;
                    }
                }
            };
        }

        /// <summary>
        /// Generates a function to set Gyroscope axis on the ship around a certain axis
        /// </summary>
        /// <param name="direction">The direction of desired thrust</param>
        /// <returns>Action that sets the Gyroscope Override on the ship</returns>
        public Action<ControllerInput, string[]> MakeSetGyros(Vector3I axisNegative)
        {
            Vector3I axis = -axisNegative;

            return (ControllerInput input, string[] args) =>
            {
                // Gets the directional input (corrects when on reverse axis, etc)
                float inputDirectional = this.GetAxisValueByZero(input.AnalogValue, this.GetArg(args, 1, "Normal", AXIS_DIRECTIONS));
                Echo($"Axi: {input.Axis}");

                // Centers value first
                float value = 6 * this.GetAxisValueByZero(inputDirectional, "Center");

                // Small values correct to zero!
                if (Math.Abs(value) < this.DEAD_ZONE)
                    value = 0;

                // Plot input on a simple curve
                value = this.PlotOnExpCurve(value, 0, 6, INPUT_EXP_GYRO);

                // Now pass it down to the gyroscopes
                // TODO: Find out which gyro should be activated accordingly to their orientation
                foreach (IMyGyro gyro in this.shipGyroscopes)
                {
                    // Gets the Gyro transform data
                    Vector3I[] gyroTransforms = this.shipGyroscopesTransforms[gyro.EntityId];

                    // We need to enable Gyro Override first to set these values, but only do that if we have any input
                    if (Math.Abs(value) > 0)
                    {
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
                    if (gyro.Pitch + gyro.Roll + gyro.Yaw == 0)
                    {
                        gyro.GyroOverride = false;
                    }
                }
            };
        }

        /// <summary>
        /// Generates a function to set Gyroscope power around an axis
        /// </summary>
        /// <returns>Action that sets the Gyroscope Power on the ship</returns>
        public Action<ControllerInput, string[]> MakeSetGyroPower()
        {
            return (ControllerInput input, string[] args) =>
            {
                Echo($"Axi: {input.Axis}");
                // Gets the directional input (corrects when on reverse axis, etc)
                float inputDirectional = this.GetAxisValueByZero(input.AnalogValue, this.GetArg(args, 1, "Normal", AXIS_DIRECTIONS));

                // Plot input on a simple curve
                float value = this.PlotOnExpCurve(inputDirectional, 0, 1, INPUT_EXP_FLAT);

                // Sets gyro power
                foreach (IMyGyro gyro in this.shipGyroscopes)
                {
                    gyro.GyroPower = value;
                }
            };
        }
    }
}
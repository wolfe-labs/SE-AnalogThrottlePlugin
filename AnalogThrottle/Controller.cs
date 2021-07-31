using System;
using System.Collections.Generic;
using SharpDX.DirectInput;

namespace WolfeLabs.AnalogThrottle
{
    public class Controller
    {
        /// <summary>
        /// The event handler when an analog axis is changed (sliders, etc)
        /// </summary>
        public class AnalogEventArgs : EventArgs
        {
            public string Axis { get; set; }
            public ushort Value { get; set; }
        }

        /// <summary>
        /// The event handler when an digital axis is changed (buttons, etc)
        /// </summary>
        public class DigitalEventArgs : EventArgs
        {
            public string Axis { get; set; }
            public bool Value { get; set; }
        }

        /// <summary>
        /// Fires whenever an analog input is received (sliders, for example)
        /// </summary>
        public event EventHandler<AnalogEventArgs> AnalogInput;

        /// <summary>
        /// Fires whenever an digital input is received (buttons, for example)
        /// </summary>
        public event EventHandler<DigitalEventArgs> DigitalInput;

        /// <summary>
        /// The underlying DirectInput instance
        /// </summary>
        public DirectInput DirectInput { get; private set; }

        /// <summary>
        /// The underlying DirectInput DeviceInstance being used by this Controller instance
        /// </summary>
        public DeviceInstance Device { get; private set; }

        /// <summary>
        /// The underlying DirectInput Joystick being used by this Controller instance
        /// </summary>
        public Joystick Joystick { get; private set; }

        // The range of this specific input, to be remapped into a ushort later
        private InputRange inputRange;

        // List of all axis
        private readonly DeviceObjectInstance[] Axis;

        // Previous state of all axis
        private JoystickState AxisPreviousState;

        /// <summary>
        /// Creates a new instance of a joystick/controller device
        /// </summary>
        /// <param name="directInput">The main instance of DirectInput</param>
        /// <param name="baseDevice">The DirectInput device</param>
        public Controller (DirectInput directInput, DeviceInstance baseDevice)
        {
            // Prepare general class members
            this.DirectInput = directInput;
            this.Device = baseDevice;
            this.Joystick = new Joystick(directInput, this.Device.InstanceGuid);

            // Makes sure the Joystick was properly initialized
            if (null == this.Joystick)
                throw new Exception($"Could not initialize Joystick device '{ baseDevice.InstanceName }' [{ baseDevice.InstanceGuid }]");

            // Set-up the Joystick Axis array
            this.Axis = (this.Joystick.GetObjects() as List<DeviceObjectInstance>).ToArray();

            // Handles each of the Axis and makes sure it's ready to feed data
            for (int axisIndex = 0; axisIndex < this.Axis.Length; axisIndex++) {
                // Gets the proper object
                DeviceObjectInstance axis = this.Axis[axisIndex];

                // Sets the range limiter to fit an ushort later on
                try {
                    this.inputRange = this.Joystick.GetObjectPropertiesById(axis.ObjectId).Range;
                } catch { }
            }

            // Starts the Joystick
            this.Joystick.Acquire();
            this.AxisPreviousState = this.Joystick.GetCurrentState();
        }

        /// <summary>
        /// Triggers a pooling event on the Controller
        /// </summary>
        public void HandleInput ()
        {
            // Gets the state of the Joystick
            JoystickState currentState = this.Joystick.GetCurrentState();

            // Processes Analog X/Y/Z
            this.CompareAndEmitAnalog("X", this.AxisPreviousState.X, currentState.X);
            this.CompareAndEmitAnalog("Y", this.AxisPreviousState.Y, currentState.Y);
            this.CompareAndEmitAnalog("Z", this.AxisPreviousState.Z, currentState.Z);

            // Processes Analog Rotation X/Y/Z
            this.CompareAndEmitAnalog("RX", this.AxisPreviousState.RotationX, currentState.RotationX);
            this.CompareAndEmitAnalog("RY", this.AxisPreviousState.RotationY, currentState.RotationY);
            this.CompareAndEmitAnalog("RZ", this.AxisPreviousState.RotationZ, currentState.RotationZ);

            // Handles POV hats
            for (int iPOV = 0; iPOV < currentState.PointOfViewControllers.Length; iPOV++) {
                this.CompareAndEmitAnalog($"P{ iPOV }", this.AxisPreviousState.PointOfViewControllers[iPOV], currentState.PointOfViewControllers[iPOV]);
            }

            // Handles sliders
            for (int iSlider = 0; iSlider < currentState.Sliders.Length; iSlider++) {
                this.CompareAndEmitAnalog($"S{ iSlider }", this.AxisPreviousState.Sliders[iSlider], currentState.Sliders[iSlider]);
            }

            // Handles buttons
            for (int iButton = 0; iButton < currentState.Buttons.Length; iButton++) {
                this.CompareAndEmitDigital($"B{ iButton }", this.AxisPreviousState.Buttons[iButton], currentState.Buttons[iButton]);
            }

            // Updates previous state so that only new changes trigger events
            this.AxisPreviousState = currentState;
        }

        private ushort NormalizeValue (int rawValue)
        {
            return (ushort)(((double)rawValue / (double)this.inputRange.Maximum) * (double)ushort.MaxValue);
        }

        private bool CompareAndEmitAnalog (string axisName, int oldValue, int newValue)
        {
            // Does check for difference
            if (oldValue != newValue) {
                // Emits event (if any handler is present)
                if (null != this.AnalogInput) {
                    this.AnalogInput(this, new AnalogEventArgs { Axis = axisName, Value = this.NormalizeValue(newValue) });
                }

                // Indicates a difference was found
                return true;
            }

            // Indicates no difference was found
            return false;
        }

        private bool CompareAndEmitDigital (string axisName, bool oldValue, bool newValue)
        {
            // Does check for difference
            if (oldValue != newValue) {
                // Emits event (if any handler is present)
                if (null != this.DigitalInput) {
                    this.DigitalInput(this, new DigitalEventArgs { Axis = axisName, Value = newValue });
                }

                // Indicates a difference was found
                return true;
            }

            // Indicates no difference was found
            return false;
        }
    }
}
using System;

namespace IngameScript.WolfeLabs.AnalogThrottleAPI
{
    public class ControllerInput
    {
        public ControllerInput (string source, string axis, ushort rawValue) {
            this.Source = source;
            this.Axis = axis;
            this.RawValue = rawValue;
            this.AnalogValue = (float)rawValue / (float)ushort.MaxValue;
            this.DigitalValue = rawValue > ushort.MaxValue / 2;
        }

        /// <summary>
        /// The Controller name that triggered this event
        /// </summary>
        public string Source { get; private set; }

        /// <summary>
        /// The Controller axis that triggered this event
        /// </summary>
        public string Axis { get; private set; }

        /// <summary>
        /// The Raw value of this event
        /// </summary>
        public float RawValue { get; private set; }

        /// <summary>
        /// The Analog value of this event
        /// </summary>
        public float AnalogValue { get; private set; }

        /// <summary>
        /// The Digital value of this event
        /// </summary>
        public bool DigitalValue { get; private set; }

        // This is the character separating each of the parts of each line
        private static char separator = ':';

        /// <summary>
        /// Parses a string containing the data from controller input into a script-friendly object
        /// </summary>
        public static ControllerInput FromString (string source)
        {
            // Parses the string
            string[] args = source.Trim().Split(ControllerInput.separator);

            // Makes sure we have the right amount of arguments
            if (3 != args.Length)
                throw new Exception("Invalid number of arguments!");

            // Extracts individual values
            string controller = args[0].Trim();
            string axis = args[1].Trim();
            ushort value = ushort.Parse(args[2].Trim());

            // Creates the input data and applies the actual values
            return new ControllerInput(controller, axis, value);
        }

        /// <summary>
        /// Converts a controller input object into a storage/transmission-friendly string
        /// </summary>
        public override string ToString ()
        {
            return String.Join(ControllerInput.separator.ToString(), new string[] {
                this.Source,
                this.Axis,
                this.RawValue.ToString(),
            });
        }
    }
}
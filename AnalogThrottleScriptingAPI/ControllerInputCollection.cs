using System;
using System.Collections.Generic;

namespace IngameScript.WolfeLabs.AnalogThrottleAPI
{
    public class ControllerInputCollection : List<ControllerInput>
    {
        // The character separating each of the different input entries
        private static char separator = '\n';

        /// <summary>
        /// Creates a collection from an Array of controller inputs
        /// </summary>
        public static ControllerInputCollection FromArray (ControllerInput[] source)
        {
            ControllerInputCollection collection = new ControllerInputCollection();
            foreach (ControllerInput input in source) {
                collection.Add(input);
            }
            return collection;
        }

        /// <summary>
        /// Parses a string containing the data from controller input collection into a script-friendly array of objects
        /// </summary>
        public static ControllerInputCollection FromString (string source, bool skipCorruptEntries)
        {
            // Separates every entry as a string
            string[] entriesAsString = source.Trim().Split(ControllerInputCollection.separator);

            // Now process every entry's string into a proper object
            ControllerInputCollection collection = new ControllerInputCollection();
            foreach (string entry in entriesAsString) {
                try {
                    // Attempts to parse the entry and add it to our results collection
                    collection.Add(ControllerInput.FromString(entry));
                } catch (Exception e) {
                    // If it fails, check if we're ignoring/skipping corrupt entries
                    if (!skipCorruptEntries)
                        throw e;
                }
            }

            // Returns list of entries
            return collection;
        }

        /// <summary>
        /// Parses a string containing the data from controller input collection into a script-friendly array of objects
        /// </summary>
        public static ControllerInputCollection FromString (string source)
        {
            return ControllerInputCollection.FromString(source, true);
        }

        /// <summary>
        /// Converts a collection of controller inputs into a storage/transmission-friendly string
        /// </summary>
        public override string ToString ()
        {
            // Processes each of the collection's entries into a string first
            List<string> entriesAsString = new List<string>();
            foreach(ControllerInput input in this) {
                entriesAsString.Add(input.ToString());
            }

            // Now we just join everything with a separator in between
            return string.Join(ControllerInputCollection.separator.ToString(), entriesAsString.ToArray());
        }
    }
}

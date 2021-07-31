using System;
using System.IO;

namespace WolfeLabs.AnalogThrottle
{
    public class DebugHelper
    {
        public static readonly string LogFile = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(Plugin)).Location), "AnalogThrottle.log");

#if DEBUG
        private static readonly StreamWriter LogWriter = new StreamWriter(DebugHelper.LogFile, true);
#endif
        public static void Log (object data)
        {
#if DEBUG
            string line = $"[{ System.DateTime.Now.ToString("u") }] { Newtonsoft.Json.JsonConvert.SerializeObject(data) }";
            Console.WriteLine(line);
            LogWriter.WriteLine(line);
            LogWriter.Flush();
#endif
        }
    }
}
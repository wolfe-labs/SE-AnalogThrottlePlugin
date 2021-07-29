#pragma warning disable ProhibitedMemberRule // Ignores whitelist errors

using System;
using System.IO;
using Newtonsoft.Json;

namespace WolfeLabs.AnalogThrottle
{
    class DebugHelper
    {
        private static StreamWriter LogFile = new StreamWriter("F:\\tmp\\AnalogThrottle.log");
        public static void Log (object data)
        {
            string line = $"[{ System.DateTime.Now.ToString("u") }] { JsonConvert.SerializeObject(data) }";
            Console.WriteLine(line);
            LogFile.WriteLine(line);
            LogFile.Flush();
        }
    }
}

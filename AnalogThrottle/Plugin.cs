using VRage.Plugins;

namespace WolfeLabs.AnalogThrottle
{
    class Plugin : IPlugin
    {
        /// <summary>
        /// Tag that indicates a block is able to receive data from the plugin
        /// </summary>
        public const string TAG_SCRIPTABLE = "[AnalogThrottle]";

        /// <summary>
        /// In multiplayer, only handle joystick events every one in N ticks
        /// Ex: 2 => 1 in 2 ticks, 3 => 1 in 3 ticks, etc
        /// </summary>
        public const ushort THROTTLE_MULTIPLAYER = 3;

        public void Init (object gameObject) { }

        public void Update () { }

        public void Dispose () { }
    }
}
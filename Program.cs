#pragma warning disable ProhibitedMemberRule // Ignores whitelist errors

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.DirectInput;

namespace WolfeLabs.AnalogThrottle
{
    class Program
    {
        static void Main ()
        {
            // Our main DI handler
            DirectInput directInput = new DirectInput();

            // Gets list of devices
            var availableDevices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);

            // List of all controllers
            List<Controller> controllers = new List<Controller>();

            // Find appropriate type of devices
            foreach (DeviceInstance device in availableDevices) {
                Controller controller = new Controller(directInput, device);
                controller.AnalogInput += (object sender, Controller.AnalogEventArgs args) => {
                    Console.WriteLine($"Received/A [{ controller.Device.InstanceName }].[{ args.Axis }]: { args.Value }");
                };
                controllers.Add(controller);
            }

            // Main loop where we handle controller input
            while (true) {
                foreach(Controller controller in controllers) {
                    controller.HandleInput();
                }
            }
        }
    }
}
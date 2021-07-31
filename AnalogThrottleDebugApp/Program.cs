using SharpDX.DirectInput;
using WolfeLabs.AnalogThrottle;
using System;
using System.Collections.Generic;
using System.Threading;

namespace AnalogThrottleDebugApp
{
    class Program
    {
        // The column format for printing
        const string COLUMNS = "|{0,3}|{1,5}|{2}|";

        static void Main (string[] args)
        {
            // Start here
            Console.Write("Detecting connected devices...");

            // Our list of Controllers
            List<Controller> controllers = new List<Controller>();

            // Initializes DirectInput
            DirectInput directInput = new DirectInput();

            // Fetches the available controllers and sets them up
            List<DeviceInstance> availableDevices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly) as List<DeviceInstance>;
            foreach (DeviceInstance deviceInstance in availableDevices) {
                // Adds the Controller to available/monitoring list
                Controller controller = new Controller(directInput, deviceInstance);
                controller.AnalogInput += (object sender, Controller.AnalogEventArgs data) => { Program.DebugAnalogInput(controller, data); };
                controller.DigitalInput += (object sender, Controller.DigitalEventArgs data) => { Program.DebugDigitalInput(controller, data); };
                controllers.Add(controller);

                Console.WriteLine($"Monitoring device: { deviceInstance.InstanceName }");
            }

            // Creates the main timer
            Timer simTimer = new Timer(new TimerCallback((object timer) => {
                // Processes actions of each controller
                foreach(Controller controller in controllers) {
                    controller.HandleInput();
                }
            }), null, 0, 1000 / 60);

            // Exits on keypress
            Console.ReadKey();
        }

        static void PrintRow(params object[] data)
        {
            Console.WriteLine(string.Format(Program.COLUMNS, data));
        }

        static void DebugAnalogInput (Controller controller, Controller.AnalogEventArgs data)
        {
            Program.PrintRow(data.Axis, data.Value, controller.Device.InstanceName);
        }

        static void DebugDigitalInput (Controller controller, Controller.DigitalEventArgs data)
        {
            Program.PrintRow(data.Axis, data.Value ? "ON" : "OFF", controller.Device.InstanceName);
        }
    }
}

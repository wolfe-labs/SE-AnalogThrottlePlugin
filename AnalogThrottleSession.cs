#pragma warning disable ProhibitedMemberRule // Ignores whitelist errors

using IngameScript.WolfeLabs.AnalogThrottleAPI;
using Sandbox.ModAPI;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace WolfeLabs.AnalogThrottle
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    class AnalogThrottleSession : MySessionComponentBase
    {
        public static AnalogThrottleSession Instance;

        // This List stores all currently connected controllers
        private List<Controller> controllers = new List<Controller>();

        // This List will store all commands that will be sent to our PBs
        private ControllerInputCollection queuedInputEvents = new ControllerInputCollection();

        public override void Init (MyObjectBuilder_SessionComponent sessionComponent)
        {
            DebugHelper.Log("Session.Init()");

            // Initializes DirectInput
            DirectInput directInput = new DirectInput();

            // Fetches the available controllers and sets them up
            List<DeviceInstance> availableDevices = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly) as List<DeviceInstance>;
            foreach (DeviceInstance deviceInstance in availableDevices) {
                // Adds the Controller to available/monitoring list
                Controller controller = new Controller(directInput, deviceInstance);
                controller.AnalogInput += this.HandleAnalogInput;
                controller.DigitalInput += this.HandleDigitalInput;
                this.controllers.Add(controller);
            }
        }

        public override void LoadData ()
        {
            AnalogThrottleSession.Instance = this;
        }

        protected override void UnloadData ()
        {
            AnalogThrottleSession.Instance = null;
        }

        public override void UpdateBeforeSimulation ()
        {
            // Ticks the Controllers
            foreach (Controller controller in this.controllers) {
                controller.HandleInput();
            }

            // Sends list of queued events and clears queue
            DebugHelper.Log($"Sending { this.queuedInputEvents.Count } controller events to game");
            this.SendInputToActiveGrid(this.queuedInputEvents.ToString());
            this.queuedInputEvents.Clear();
        }

        private void SendInputToActiveGrid (string message)
        {
            try {
                // This is our active entity, which may be the player, cockpit, etc
                var currentEntity = this.Session.Player.Controller.ControlledEntity;

                // We want to make sure we're in a cockpit
                DebugHelper.Log($"Attempting input to: { this.Session.Player.Controller.ControlledEntity.GetType().FullName }");
                IMyCockpit currentControlUnit = currentEntity as IMyCockpit;
                if (null == currentControlUnit)
                    return;

                DebugHelper.Log($"Cockpit activated: { currentControlUnit.GetFriendlyName() }");
                DebugHelper.Log($"Grid found: { currentControlUnit.CubeGrid.GetFriendlyName() }");

                // Tries to fetch all blocks on the grid
                List<IMySlimBlock> gridBlocks = new List<IMySlimBlock>();
                currentControlUnit.CubeGrid.GetBlocks(gridBlocks);

                DebugHelper.Log($"Success! Found { gridBlocks.Count } blocks!");

                // Now filters any Programming Blocks that contain the [AnalogThrottle] tag
                foreach (IMySlimBlock block in gridBlocks) {
                    // Tries to convert into a PB
                    IMyProgrammableBlock blockAsPB = block.FatBlock as IMyProgrammableBlock;

                    // Sends event if valid
                    if (null != blockAsPB && blockAsPB.CustomName.Contains("[AnalogThrottle]")) {
                        DebugHelper.Log($"Sending event to PB: { blockAsPB.CustomName }");
                        blockAsPB.Run(message, Sandbox.ModAPI.Ingame.UpdateType.Terminal);
                    }
                }
            } catch (Exception e) {
                DebugHelper.Log("ERROR: " + e.Message);
                DebugHelper.Log(e);
            }
        }

        private void HandleAnalogInput (object sender, Controller.AnalogEventArgs args)
        {
            Controller controller = sender as Controller;
            //DebugHelper.Log($"Analog [{ controller.Device.InstanceName }].[{ args.Axis }]: { args.Value }");
            this.queuedInputEvents.Add(new ControllerInput(controller.Device.InstanceName, args.Axis, args.Value));
        }

        private void HandleDigitalInput (object sender, Controller.DigitalEventArgs args)
        {
            Controller controller = sender as Controller;
            //DebugHelper.Log($"Digital [{ controller.Device.InstanceName }].[{ args.Axis }]: { (args.Value ? "On" : "Off") }");
            this.queuedInputEvents.Add(new ControllerInput(controller.Device.InstanceName, args.Axis, args.Value ? ushort.MinValue : ushort.MaxValue));
        }
    }
}
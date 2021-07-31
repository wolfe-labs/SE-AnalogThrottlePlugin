using IngameScript.WolfeLabs.AnalogThrottleAPI;
using Sandbox.ModAPI;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;

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

        // Our current Player
        private IMyPlayer currentPlayer = null;

        // Our current Cockpit
        private IMyCockpit currentControlUnit = null;

        // Our current Grid
        private IMyCubeGrid currentGrid = null;

        // Our current tick number
        private ushort currentTick = ushort.MaxValue;

        // Our currently linked Programming Blocks
        private List<IMyProgrammableBlock> currentProgrammableBlocks = new List<IMyProgrammableBlock>();

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
            // Cleanup events
            this.Session.Player.Controller.ControlledEntityChanged -= this.UpdateCurrentControlUnit;

            AnalogThrottleSession.Instance = null;
        }

        public override void UpdateBeforeSimulation ()
        {
            // Accounts for current tick, overflows as needed
            if (this.currentTick == ushort.MaxValue) {
                this.currentTick = ushort.MinValue;
            } else {
                this.currentTick++;
            }

            // Waits for a Player instance to exist
            if (null == this.Session.Player)
                return;

            // If first time detected a player (= login, usually) does first triggers and set-up
            if (null == this.currentPlayer) {
                this.currentPlayer = this.Session.Player;
                this.Session.Player.Controller.ControlledEntityChanged += this.UpdateCurrentControlUnit;
                this.UpdateCurrentControlUnit(null, this.Session.Player.Controller.ControlledEntity);
            }

            // For multiplayer sessions, only processed each n-th input
            if (!this.Session.IsServer && 0 != this.currentTick % Plugin.THROTTLE_MULTIPLAYER) {
                DebugHelper.Log($"Skipping tick for multiplayer: { this.currentTick % Plugin.THROTTLE_MULTIPLAYER } of { Plugin.THROTTLE_MULTIPLAYER }");
                return;
            }

            // Ticks the Controllers
            foreach (Controller controller in this.controllers) {
                controller.HandleInput();
            }

            // Sends list of queued events and clears queue
            if (this.currentProgrammableBlocks.Count > 0 && this.queuedInputEvents.Count > 0) {
                DebugHelper.Log($"Sending { this.queuedInputEvents.Count } controller events to game");
                this.SendInputToActiveGrid(this.queuedInputEvents.ToString());
            }
            this.queuedInputEvents.Clear();
        }

        private void UpdateCurrentControlUnit (IMyControllableEntity oldUnit, IMyControllableEntity newUnit)
        {
            // Tries to convert the new Control Unit into a Cockpit, becomes null otherwise
            this.currentControlUnit = newUnit as IMyCockpit;

            DebugHelper.Log("Convert Check");
            // If a Cockpit and a grid are detected, extract grid information
            if (null != this.currentControlUnit && null != this.currentControlUnit.CubeGrid) {
                // Prepares grid and events
                DebugHelper.Log("Grid");
                this.currentGrid = this.currentControlUnit.CubeGrid;
                DebugHelper.Log("Events");
                this.currentGrid.OnBlockAdded += this.UpdateCurrentGridBlocks;
                this.currentGrid.OnBlockRemoved += this.UpdateCurrentGridBlocks;

                // Fetches the Programmable Blocks
                DebugHelper.Log("UpdateBlocks");
                this.UpdateCurrentGridBlocks(null);

                DebugHelper.Log($"Cockpit found: { this.currentControlUnit.CustomName }");
                DebugHelper.Log($"Grid found: { this.currentControlUnit.CubeGrid.CustomName }");
                return;
            }

            // Clear current grid if no grid available
            if (null != this.currentGrid) {
                this.currentGrid.OnBlockAdded -= this.UpdateCurrentGridBlocks;
                this.currentGrid.OnBlockRemoved -= this.UpdateCurrentGridBlocks;
                this.currentGrid = null;
            }

            // Also clear all PBs
            this.currentProgrammableBlocks.Clear();

            DebugHelper.Log($"No cockpit found");
        }

        private void UpdateCurrentGridBlocks (IMySlimBlock changedBlock)
        {
            // Makes sure we have an active cockpit and grid
            if (this.currentGrid == null || this.currentGrid == null)
                return;

            // Gets a list of all blocks on the grid
            List<IMySlimBlock> gridBlocks = new List<IMySlimBlock>();
            currentControlUnit.CubeGrid.GetBlocks(gridBlocks);

            // Now filters any Programmable Blocks that contain the [AnalogThrottle] tag
            this.currentProgrammableBlocks.Clear();
            foreach (IMySlimBlock block in gridBlocks) {
                // Tries to convert into a PB
                IMyProgrammableBlock blockAsPB = block.FatBlock as IMyProgrammableBlock;

                // Adds to our PB list if found
                if (null != blockAsPB && blockAsPB.CustomName.Contains(Plugin.TAG_SCRIPTABLE)) {
                    this.currentProgrammableBlocks.Add(blockAsPB);
                }
            }

            // Debugging helper, shows how many valid PBs were found
            DebugHelper.Log($"Grid processed: { this.currentProgrammableBlocks.Count } PBs out of { gridBlocks.Count } blocks");
        }

        private void SendInputToActiveGrid (string message)
        {
            DebugHelper.Log($"Preparing command: { message }");
            try {
                // Sends data to each of our PBs
                foreach (IMyProgrammableBlock block in this.currentProgrammableBlocks) {
                    DebugHelper.Log($"Sending command to PB: { block.CustomName }");
                    block.Run(message, Sandbox.ModAPI.Ingame.UpdateType.Mod);
                }
            } catch (Exception e) {
                DebugHelper.Log("ERROR: " + e.Message);
                DebugHelper.Log(e);
            }
        }

        private void HandleAnalogInput (object sender, Controller.AnalogEventArgs args)
        {
            Controller controller = sender as Controller;
            this.queuedInputEvents.Add(new ControllerInput(controller.Device.InstanceName, args.Axis, args.Value));
        }

        private void HandleDigitalInput (object sender, Controller.DigitalEventArgs args)
        {
            Controller controller = sender as Controller;
            this.queuedInputEvents.Add(new ControllerInput(controller.Device.InstanceName, args.Axis, args.Value ? ushort.MinValue : ushort.MaxValue));
        }
    }
}
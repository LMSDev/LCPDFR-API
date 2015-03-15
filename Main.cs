namespace API_Example
{
    using System;
    using System.Windows.Forms;

    using API_Example.Callouts;
    using API_Example.World_Events;

    using GTA;

    using LCPDFR.Networking.User;

    using LCPD_First_Response.Engine;
    using LCPD_First_Response.Engine.Input;
    using LCPD_First_Response.Engine.Networking;
    using LCPD_First_Response.Engine.Scripting.Plugins;
    using LCPD_First_Response.Engine.Timers;
    using LCPD_First_Response.LCPDFR.API;

    using LCPDFR.Networking;

    using SlimDX.XInput;

    /// <summary>
    /// The network messages.
    /// </summary>
    public enum ENetworkMessages
    {
        /// <summary>
        /// The message sent when user requests backup.
        /// </summary>
        RequestBackup,
    }

    /// <summary>
    /// Sample plugin making use of the LCPDFR API. In the attribute below you can specify the name of the plugin.
    /// </summary>
    [PluginInfo("TestPlugin", false, true)]
    public class TestPlugin : Plugin
    {
        /// <summary>
        /// A LCPDFR ped.
        /// </summary>
        private LPed lcpdfrPed;

        /// <summary>
        /// Called when the plugin has been created successfully.
        /// </summary>
        public override void Initialize()
        {
            // Bind console commands
            this.RegisterConsoleCommands();

            // Listen for on duty event
            Functions.OnOnDutyStateChanged += this.Functions_OnOnDutyStateChanged;
            Networking.JoinedNetworkGame += this.Networking_JoinedNetworkGame;

            Log.Info("Started", this);
        }

        /// <summary>
        /// Called when player changed the on duty state.
        /// </summary>
        /// <param name="onDuty">The new on duty state.</param>
        public void Functions_OnOnDutyStateChanged(bool onDuty)
        {
            if (onDuty)
            {
                // Register callouts to LCPDFR
                Functions.RegisterCallout(typeof(Shooting));
                Functions.RegisterCallout(typeof(Pursuit));

                Functions.AddWorldEvent(typeof(Brawl), "Brawl");
            }
        }

        /// <summary>
        /// Called every tick to process all plugin logic.
        /// </summary>
        public override void Process()
        {
            // If on duty and Z is down
            if (LPlayer.LocalPlayer.IsOnDuty && (Functions.IsKeyDown(Keys.Z) || (Functions.IsControllerInUse() && Functions.IsControllerKeyDown(GamepadButtonFlags.DPadRight))))
            {
                DelayedCaller.Call(
                    delegate
                    {
                        LPlayer.LocalPlayer.Ped.DrawTextAboveHead("Test", 500);
                    }, 
                    this, 
                    500);

                if (this.lcpdfrPed == null || this.lcpdfrPed.Exists() || this.lcpdfrPed.IsAliveAndWell)
                {
                    // Create a ped
                    this.lcpdfrPed = new LPed(LPlayer.LocalPlayer.Ped.Position, "F_Y_HOOKER_01");
                    this.lcpdfrPed.NoLongerNeeded();
                    this.lcpdfrPed.AttachBlip();
                    this.lcpdfrPed.ItemsCarried = LPed.EPedItem.Drugs;
                    LPed.EPedItem item = this.lcpdfrPed.ItemsCarried;
                    this.lcpdfrPed.PersonaData = new PersonaData(DateTime.Now, 0, "Sam", "T", false, 1337, true);
                }
            }

            // If our ped exists and has been arrested, kill it
            if (this.lcpdfrPed != null && this.lcpdfrPed.Exists())
            {
                if (this.lcpdfrPed.HasBeenArrested && this.lcpdfrPed.IsAliveAndWell)
                {
                    this.lcpdfrPed.Die();
                }
            }

            if (Functions.IsKeyDown(Keys.B))
            {
                if (Functions.IsPlayerPerformingPullover())
                {
                    LHandle pullover = Functions.GetCurrentPullover();
                    if (pullover != null)
                    {
                        LVehicle vehicle = Functions.GetPulloverVehicle(pullover);
                        if (vehicle != null && vehicle.Exists())
                        {
                            vehicle.AttachBlip().Color = BlipColor.Cyan;
                            if (vehicle.HasDriver)
                            {
                                // Change name of driver to Sam T.
                                LPed driver = vehicle.GetPedOnSeat(VehicleSeat.Driver);
                                if (driver != null && driver.Exists())
                                {
                                    // Modify name.
                                    driver.PersonaData = new PersonaData(DateTime.Now, 0, "Sam", "T", true, 0, false);

                                    string name = driver.PersonaData.FullName;
                                    Functions.PrintText("--- Pulling over: " + name + " ---", 10000);
                                            
                                    // Looking up the driver will make the vehicle explode.
                                    Functions.PedLookedUpInPoliceComputer += delegate(PersonaData data)
                                        {
                                            if (data.FullName == name)
                                            {
                                                DelayedCaller.Call(delegate { if (vehicle.Exists()) { vehicle.Explode(); } }, this, Common.GetRandomValue(5000, 10000));
                                            }
                                        };
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Disable pullovers for vehicle in front.
                    GTA.Vehicle vehicle = World.GetClosestVehicle(LPlayer.LocalPlayer.Ped.GetOffsetPosition(new Vector3(0, 10, 0)), 5f);
                    if (vehicle != null && vehicle.Exists())
                    {
                        LVehicle veh = LVehicle.FromGTAVehicle(vehicle);
                        if (veh != null)
                        {
                            veh.DisablePullover = true;
                            veh.AttachBlip();
                        }
                    }
                }
            }

            // Kill all partners.
            if (Functions.IsKeyDown(Keys.N))
            {
                LHandle partnerManger = Functions.GetCurrentPartner();
                LPed[] peds = Functions.GetPartnerPeds(partnerManger);
                if (peds != null)
                {
                    foreach (LPed partner in peds)
                    {
                        if (partner.Exists())
                        {
                            partner.Die();
                        }
                    }
                }
            }

            // Send RequestBackup message in network game.
            if (Functions.IsKeyDown(Keys.X))
            {
                if (Networking.IsInSession && Networking.IsConnected)
                {
                    if (Networking.IsHost)
                    {
                        Vector3 position = LPlayer.LocalPlayer.Ped.Position;

                        // Tell client we need backup.
                        DynamicData dynamicData = new DynamicData(Networking.GetServerInstance());
                        dynamicData.Write(position);
                        Networking.GetServerInstance().Send("API_Example", ENetworkMessages.RequestBackup, dynamicData);
                    }
                }
            }
        }

        /// <summary>
        /// Called when the plugin is being disposed, e.g. because an unhandled exception occured in Process. Free all resources here!
        /// </summary>
        public override void Finally()
        {
        }

        /// <summary>
        /// Called when player has joined a network game.
        /// </summary>
        private void Networking_JoinedNetworkGame()
        {
            // Just to be sure.
            if (Networking.IsInSession && Networking.IsConnected)
            {
                // When client, listen for messages. Of course, this could be changed to work for host as well.
                // It's recommended to use the same string identifier all the time.
                if (!Networking.IsHost)
                {
                    Client client = Networking.GetClientInstance();
                    client.AddUserDataHandler("API_Example", ENetworkMessages.RequestBackup, this.NeedBackupHandlerFunction);
                }
            }
        }

        /// <summary>
        /// Called when <see cref="ENetworkMessages.RequestBackup"/> has been received.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="message">The message.</param>
        private void NeedBackupHandlerFunction(NetworkServer sender, ReceivedUserMessage message)
        {
            // Read position and get associated area.
            Vector3 position = message.ReadVector3();
            string area = Functions.GetAreaStringFromPosition(position);

            // Display message and blip.
            Functions.PrintText(string.Format("Officer {0} requests backup at {1}", sender.SafeName, area), 5000);
            Blip areaBlip = Functions.CreateBlipForArea(position, 25f);

            // Cleanup.
            DelayedCaller.Call(parameter => areaBlip.Delete(), this, 10000);
        }

        [ConsoleCommand("StartCallout", false)]
        private void StartCallout(ParameterCollection parameterCollection)
        {
            if (parameterCollection.Count > 0)
            {
                string name = parameterCollection[0];
                Functions.StartCallout(name);
            }
            else
            {
                Game.Console.Print("StartCallout: No argument given.");
            }
        }
    }
}
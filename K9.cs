using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API;
using FivePD.API.Utils;
using MenuAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace K9_Plugin
{
    public class K9 : Plugin
    {
        public enum DogType
        {
            BOMB,
            NARCOTICS
        }

        private Menu k9Menu;
        private Vehicle vehicle = null;
        private string dogName = "K-9";
        private DogType dogType;

        private Dictionary<string, bool> k9Jobs;

        //Discord Perm Check
        private bool hasRole = false;

        //flags
        private bool isSitting = false;
        private bool isLayingDown = false;
        private bool noCollision = false;
        private bool subtanceFound = false;
        private int alertTaskNumb = 0;

        //Target Data
        private Ped targetPed = null;
        private int target = 0;

        //Vehicle
        private int veh = 0;

        //Dog Data
        private Ped dog = null;

        //Player Data
        private Vehicle playerVehicle = null;

        //Substances
        private List<string> drugList = new List<string>();
        private List<string> bombList = new List<string>();

        //Shit stuff
        private bool noShitCollision = false;
        private int shit = 0;

        //Config Data
        private bool useAcePerms = true;

        internal K9()
        {
            LoadConfigData();

            BuildJobs();
            BuildMenu();

            EventHandlers["K9:HasRoles"] += new Action<int>(HasRoles);

            API.RegisterCommand("K9Menu", new Action(async () =>
            {
                TriggerServerEvent("K9:RoleCheck");
                await Delay(250); //give time for the server script to trigger

                if(useAcePerms)
                {
                    if (!hasRole) { ShowNotification("~r~For K9 Officers Only"); return; }
                }

                if (!k9Menu.Visible) { k9Menu.Visible = true; }
            }), false);
            API.RegisterKeyMapping("K9Menu", "K9 Menu", "KEYBOARD", "F9");
        }
        private void HasRoles(int status)
        {
            if(status == 1)
            {
                hasRole = true;
            }
        }
        private void BuildMenu()
        {
            k9Menu = new Menu("K9 Menu", "Menu for K9 Operations");
            Menu k9CommandsMenu = new Menu("Commands Menu", "K9 Emote Menu");
            MenuController.AddMenu(k9Menu);
            MenuController.AddSubmenu(k9Menu, k9CommandsMenu);

            MenuListItem spawnDogOptions = new MenuListItem("K9 Type", new List<string>() {"Narcotics", "Bomb"}, 0, "Select which type of K9 to spawn");
            MenuItem spawnDogBtn = new MenuItem("Spawn K9", "Spawns the K9 of selected type");
            MenuItem deleteDogBtn = new MenuItem("Delete K9", "Gets rid of the K9");
            MenuItem followOfficerBtn = new MenuItem("Toggle Follow Officer", "Toggles K9 following behind Officer");
            MenuItem dogVehBtn = new MenuItem("Enter/Exit Vehicle", "K9 enters or exits the players vehicle");
            MenuItem searchVehicleBtn = new MenuItem("Search Vehicle", "K9 Searches the vehicle");
            MenuItem petDogBtn = new MenuItem("Pet K9", "Sometimes the K9 just wants to be pet");
            MenuItem haltBtn = new MenuItem("Halt", "K9 stops whatever its doing");
            MenuItem commandsMnuBtn = new MenuItem("K9 Emote Commands", "Opens the K9 Emote Commands Menu");

            k9Menu.AddMenuItem(spawnDogOptions);
            k9Menu.AddMenuItem(spawnDogBtn);
            k9Menu.AddMenuItem(followOfficerBtn);
            k9Menu.AddMenuItem(dogVehBtn);
            k9Menu.AddMenuItem(searchVehicleBtn);
            k9Menu.AddMenuItem(petDogBtn);
            k9Menu.AddMenuItem(haltBtn);
            k9Menu.AddMenuItem(commandsMnuBtn);

            MenuItem sitCmdBtn = new MenuItem("Command: Sit", "TOGGLE: The K9 will sit down or get up");
            MenuItem laydownCmdBtn = new MenuItem("Command: Down", "TOGGLE: The K9 will lay down or get up");
            MenuItem speakCmdBtn = new MenuItem("Command: Speak", "K9 will bark");
            MenuItem begCmdBtn = new MenuItem("Command: Beg", "K9 will beg for snacks");
            MenuItem givePawCmdBtn = new MenuItem("Command: Give Paw", "K9 will raise its paw");
            MenuItem playDeadCmdBtn = new MenuItem("Command: Play Dead", "K9 will play dead");
            MenuItem playFetchCmdBtn = new MenuItem("Command: Fetch", "Play fetch with your K9");
            MenuItem shitCmdBtn = new MenuItem("Command: Take a Dump", "K9 takes a dump");
            MenuItem renameDogBtn = new MenuItem("Pet: Rename", "Rename the K-9");

            k9CommandsMenu.AddMenuItem(sitCmdBtn);
            k9CommandsMenu.AddMenuItem(laydownCmdBtn);
            k9CommandsMenu.AddMenuItem(speakCmdBtn);
            k9CommandsMenu.AddMenuItem(begCmdBtn);
            k9CommandsMenu.AddMenuItem(givePawCmdBtn);
            k9CommandsMenu.AddMenuItem(playDeadCmdBtn);
            k9CommandsMenu.AddMenuItem(playFetchCmdBtn);
            k9CommandsMenu.AddMenuItem(shitCmdBtn);
            k9CommandsMenu.AddMenuItem(renameDogBtn);

            MenuController.BindMenuItem(k9Menu, k9CommandsMenu, commandsMnuBtn);

            k9Menu.OnItemSelect += async (_menu, _item, _index) =>
            {
                if(_item == spawnDogBtn)
                {
                    if(dog != null)
                    {
                        ShowNotification("~r~You already have a K9");
                        return;
                    }

                    string dogModel = null;

                    if(spawnDogOptions.GetCurrentSelection() == "Narcotics")
                    {
                        dogModel = GetModelFromConfig("Narcotics");
                        dogType = DogType.NARCOTICS;
                    }
                    else
                    {
                        dogModel = GetModelFromConfig("Bomb");
                        dogType = DogType.BOMB;
                    }

                    if(dogModel == null)
                    {
                        ShowNotification("~r~Error Getting K9 Model");
                        return;
                    }

                    SetAllJobsFalse();
                    k9Jobs["Idle"] = true;
                    API.RequestModel((uint)API.GetHashKey(dogModel));
                    while(!API.HasModelLoaded((uint)API.GetHashKey(dogModel)) || !API.HasCollisionForModelLoaded((uint)API.GetHashKey(dogModel)))
                    {
                        await Delay(10);
                    }

                    Vector3 dogCoords = API.GetOffsetFromEntityInWorldCoords(Game.PlayerPed.Handle, 0f, 2f, 0f);
                    float heading = API.GetEntityHeading(Game.PlayerPed.Handle);
                    int k9 = API.CreatePed(30, (uint)API.GetHashKey(dogModel), dogCoords.X, dogCoords.Y, dogCoords.Z, heading, true, true);
                    var peds = World.GetAllPeds().Where(p => World.GetDistance(p.Position, Game.PlayerPed.Position) < 5f).OrderBy(p => World.GetDistance(p.Position, Game.PlayerPed.Position));
                    foreach(Ped ped in peds)
                    {
                        if(ped.Handle == k9)
                        {
                            dog = ped;
                        }
                    }

                    Tick += ShowDogName;
                    Tick += WaitForAttack;
                    Tick += K9OnDuty;
                    ShowNotification("~g~K9 on duty");

                    API.SetBlockingOfNonTemporaryEvents(dog.Handle, true);
                    API.SetPedFleeAttributes(dog.Handle, 0, true);
                    API.SetCanAttackFriendly(dog.Handle, true, true);

                    int blip = API.AddBlipForEntity(dog.Handle);
                    API.SetBlipAsFriendly(blip, true);
                    API.SetBlipSprite(blip, 442);
                    API.BeginTextCommandSetBlipName("STRING");
                    API.AddTextComponentString("K9");
                    API.EndTextCommandSetBlipName(blip);

                    uint hash = 0;
                    API.GetCurrentPedWeapon(dog.Handle, ref hash, false);
                    API.SetWeaponDamageModifier(hash, 0);

                    API.SetEntityInvincible(dog.Handle, true);

                    //Shuffle Menu Items
                    //Remove items
                    k9Menu.RemoveMenuItem(spawnDogOptions);
                    k9Menu.RemoveMenuItem(spawnDogBtn);
                    k9Menu.RemoveMenuItem(followOfficerBtn);
                    k9Menu.RemoveMenuItem(dogVehBtn);
                    k9Menu.RemoveMenuItem(searchVehicleBtn);
                    k9Menu.RemoveMenuItem(petDogBtn);
                    k9Menu.RemoveMenuItem(haltBtn);
                    k9Menu.RemoveMenuItem(commandsMnuBtn);

                    //Add items back
                    k9Menu.AddMenuItem(deleteDogBtn);
                    k9Menu.AddMenuItem(followOfficerBtn);
                    k9Menu.AddMenuItem(dogVehBtn);
                    k9Menu.AddMenuItem(searchVehicleBtn);
                    k9Menu.AddMenuItem(petDogBtn);
                    k9Menu.AddMenuItem(haltBtn);
                    k9Menu.AddMenuItem(commandsMnuBtn);
                }
                if(_item == deleteDogBtn)
                {
                    if(dog.Handle != 0)
                    {
                        API.SetEntityAsMissionEntity(dog.Handle, true, true);
                        API.NetworkUnregisterNetworkedEntity(dog.Handle);
                        dog.Delete();
                        dog = null;

                        Tick -= WaitForAttack;
                        Tick -= ShowDogName;
                        Tick -= K9OnDuty;

                        ShowNotification("~g~K9 off duty");
                        SetAllJobsFalse();


                        //Shuffle Menu Items
                        //Remove items
                        k9Menu.RemoveMenuItem(deleteDogBtn);
                        k9Menu.RemoveMenuItem(followOfficerBtn);
                        k9Menu.RemoveMenuItem(dogVehBtn);
                        k9Menu.RemoveMenuItem(searchVehicleBtn);
                        k9Menu.RemoveMenuItem(petDogBtn);
                        k9Menu.RemoveMenuItem(haltBtn);
                        k9Menu.RemoveMenuItem(commandsMnuBtn);

                        //Add items back
                        k9Menu.AddMenuItem(spawnDogOptions);
                        k9Menu.AddMenuItem(spawnDogBtn);
                        k9Menu.AddMenuItem(followOfficerBtn);
                        k9Menu.AddMenuItem(dogVehBtn);
                        k9Menu.AddMenuItem(searchVehicleBtn);
                        k9Menu.AddMenuItem(petDogBtn);
                        k9Menu.AddMenuItem(haltBtn);
                        k9Menu.AddMenuItem(commandsMnuBtn);
                    }
                    else
                    {
                        ShowNotification("~r~No K9 to Delete");
                    }
                }
                if(_item == followOfficerBtn)
                {
                    if(dog == null)
                    {
                        ShowNotification("~r~No K9 available");
                        return;
                    }

                    if(k9Jobs["Following"])
                    {
                        SetAllJobsFalse();
                        k9Jobs["Idle"] = true;

                        dog.Task.ClearAllImmediately();
                        ShowNotification("~y~K9 stopped following");
                    }
                    else
                    {
                        if (!k9Jobs["Idle"]) { ShowNotification("~r~K9 action in progress"); return; }

                        SetAllJobsFalse();
                        k9Jobs["Following"] = true;

                        API.RequestAnimDict("taxi_hail");
                        while(!API.HasAnimDictLoaded("taxi_hail")) { await Delay(10); }

                        Game.PlayerPed.Task.PlayAnimation("taxi_hail", "hail_taxi");
                        await Delay(1450);

                        API.ClearPedTasksImmediately(dog.Handle);
                        API.DetachEntity(dog.Handle, true, true);
                        API.TaskFollowToOffsetOfEntity(dog.Handle, Game.PlayerPed.Handle, 0.5f, 0f, 0f, 4f, -1, 0.25f, true);
                        ShowNotification("~y~K9 began following");
                    }
                }
                if(_item == dogVehBtn)
                {
                    if(dog == null) { ShowNotification("~r~No K9 on duty"); return; }

                    Vehicle lv = Game.PlayerPed.LastVehicle;
                    if(lv == null) { ShowNotification("~r~You have no vehicle"); return; }

                    if(!k9Jobs["In Vehicle"])
                    {
                        if (!k9Jobs["Idle"] && !k9Jobs["Following"]) { ShowNotification("~r~K9 must be~s~ ~y~Idle~s~ ~r~or~s~ ~y~Following~s~");  return; }

                        SetAllJobsFalse();
                        k9Jobs["In Vehicle"] = true;

                        playerVehicle = lv;

                        Vector3 offset = API.GetOffsetFromEntityInWorldCoords(lv.Handle, -1.45f, -1.25f, 0);

                        float ground = 0;
                        API.GetGroundZFor_3dCoord(offset.X, offset.Y, offset.Z, ref ground, false);

                        ground += 0.33f;
                        offset.Z = ground;

                        API.SetVehicleDoorOpen(lv.Handle, (int)VehicleDoorIndex.BackLeftDoor, false, true);
                        API.TaskGoToCoordAnyMeans(dog.Handle, offset.X, offset.Y, offset.Z, 1.0f, 0, true, 786603, 0xbf800000);

                        API.RequestAnimDict("creatures@rottweiler@in_vehicle@4x4");
                        while(World.GetDistance(API.GetEntityCoords(dog.Handle, true), offset) >= 0.7f) { await Delay(10); }

                        TaskSequence ts = new TaskSequence();
                        ts.AddTask.TurnTo(lv, 2000);
                        ts.AddTask.PlayAnimation("creatures@rottweiler@in_vehicle@4x4", "get_in");
                        ts.Close();
                        dog.Task.PerformSequence(ts);

                        while(!API.IsEntityPlayingAnim(dog.Handle, "creatures@rottweiler@in_vehicle@4x4", "get_in", 3)) { await Delay(25); }
                        noCollision = true;
                        Tick += DogCarCollision;
                        await Delay(2600);

                        API.TaskWarpPedIntoVehicle(dog.Handle, lv.Handle, (int)VehicleSeat.LeftRear);
                        API.SetVehicleDoorShut(lv.Handle, (int)VehicleDoorIndex.BackLeftDoor, true);

                        while(!dog.IsInVehicle()) { await Delay(25); }
                        dog.Task.PlayAnimation("creatures@rottweiler@in_vehicle@4x4", "sit", 8.0f, -1, AnimationFlags.Loop);
                        noCollision = false;
                        Tick -= ShowDogName;
                    }
                    else
                    {
                        SetAllJobsFalse();
                        k9Jobs["Idle"] = true;

                        Vector3 offset = API.GetOffsetFromEntityInWorldCoords(lv.Handle, -1.35f, -1.35f, 0);
                        API.SetVehicleDoorOpen(lv.Handle, (int)VehicleDoorIndex.BackLeftDoor, false, false);

                        API.RequestAnimDict("creatures@rottweiler@in_vehicle@4x4");
                        while (!API.HasAnimDictLoaded("creatures@rottweiler@in_vehicle@4x4")) { await Delay(10); }

                        noCollision = true;
                        Tick += DogCarCollision;

                        API.SetEntityCoords(dog.Handle, dog.Position.X, dog.Position.Y, dog.Position.Z - 0.2f, false, false, false, false);

                        TaskSequence ts = new TaskSequence();
                        ts.AddTask.TurnTo(offset, 1250);
                        ts.AddTask.PlayAnimation("creatures@rottweiler@in_vehicle@4x4", "get_out");
                        ts.AddTask.GoTo(Game.PlayerPed);
                        ts.Close();
                        dog.Task.PerformSequence(ts);

                        while(!API.IsEntityPlayingAnim(dog.Handle,"creatures@rottweiler@in_vehicle@4x4", "get_out", 3)) { await Delay(5); }

                        while (World.GetDistance(dog.Position, Game.PlayerPed.Position) > 1f) { await Delay(50); }
                        dog.Task.ClearAllImmediately();
                        API.SetVehicleDoorShut(lv.Handle, (int)VehicleDoorIndex.BackLeftDoor, true);
                        noCollision = false;
                        Tick += ShowDogName;
                    }
                }
                if(_item == searchVehicleBtn)
                {
                    if(dog != null)
                    {
                        if (!k9Jobs["Idle"] && !k9Jobs["Following"]) { ShowNotification("~r~K9 must be~s~ ~y~Idle~s~ ~r~or~s~ ~y~Following~s~"); return; }

                        SetAllJobsFalse();
                        k9Jobs["Searching"] = true;
                        GetNearestVehicle();
                        while(vehicle == null) { await Delay(10); }

                        API.RequestAnimDict("missfra0_chop_find");
                        while (!API.HasAnimDictLoaded("missfra0_chop_find")) { await Delay(10); }

                        API.RequestAnimDict("gestures@f@standing@casual");
                        while (!API.HasAnimDictLoaded("gestures@f@standing@casual")) { await Delay(10); }

                        Game.PlayerPed.Task.PlayAnimation("gestures@f@standing@casual", "gesture_point", 8.0f, 4500, AnimationFlags.StayInEndFrame);

                        Function.Call((Hash)0x76180407, vehicle.Handle, true);
                        Function.Call((Hash)0xB41A56C2, 93, 182, 229, 255);

                        Vector3 frontLeftOffset = API.GetOffsetFromEntityInWorldCoords(veh, -1.75f, 1.5f, 0);
                        Vector3 frontRightOffset = API.GetOffsetFromEntityInWorldCoords(veh, 1.75f, 1.5f, 0);

                        Vector3 backLeftOffet = API.GetOffsetFromEntityInWorldCoords(veh, -1.75f, -1.5f, 0);
                        Vector3 backRightOffset = API.GetOffsetFromEntityInWorldCoords(veh, 1.75f, -1.5f, 0);

                        TaskSequence ts = new TaskSequence();
                        ts.AddTask.GoTo(frontLeftOffset, true);
                        ts.AddTask.TurnTo(vehicle, 3000);
                        ts.AddTask.PlayAnimation("missfra0_chop_find", "fra0_ig_14_chop_sniff_fwds", 8.0f, 5000, AnimationFlags.UpperBodyOnly);
                        ts.AddTask.GoTo(backLeftOffet, true);
                        ts.AddTask.TurnTo(vehicle, 3000);
                        ts.AddTask.PlayAnimation("missfra0_chop_find", "fra0_ig_14_chop_sniff_fwds", 8.0f, 5000, AnimationFlags.UpperBodyOnly);
                        ts.AddTask.GoTo(backRightOffset, false);
                        ts.AddTask.TurnTo(vehicle, 3000);
                        ts.AddTask.PlayAnimation("missfra0_chop_find", "fra0_ig_14_chop_sniff_fwds", 8.0f, 5000, AnimationFlags.UpperBodyOnly);
                        ts.AddTask.GoTo(frontRightOffset, true);
                        ts.AddTask.TurnTo(vehicle, 3000);
                        ts.AddTask.PlayAnimation("missfra0_chop_find", "fra0_ig_14_chop_sniff_fwds", 8.0f, 5000, AnimationFlags.UpperBodyOnly);
                        ts.Close();

                        dog.Task.PerformSequence(ts);
                        ShowNotification("~g~Search In Progress");

                        subtanceFound = await VehicleContainsSubstance(vehicle, dogType);
                        await Delay(500);

                        if(subtanceFound)
                        {
                            while (API.GetSequenceProgress(dog.Handle) != alertTaskNumb) { await Delay(50); }

                            API.RequestAnimDict("creatures@rottweiler@amb@world_dog_sitting@enter");
                            while (!API.HasAnimDictLoaded("creatures@rottweiler@amb@world_dog_sitting@enter")) { await Delay(10); }

                            API.RequestAnimDict("creatures@rottweiler@amb@world_dog_sitting@base");
                            while (!API.HasAnimDictLoaded("creatures@rottweiler@amb@world_dog_sitting@base")) { await Delay(10); }

                            dog.Task.ClearAllImmediately();

                            TaskSequence drugTs = new TaskSequence();
                            drugTs.AddTask.PlayAnimation("creatures@rottweiler@amb@world_dog_sitting@enter", "enter");
                            drugTs.AddTask.PlayAnimation("creatures@rottweiler@amb@world_dog_sitting@base", "base", 8.0f, -1, AnimationFlags.Loop);
                            drugTs.Close();

                            dog.Task.PerformSequence(drugTs);

                            ShowNotification("~g~K9 Found Something");
                            Function.Call((Hash)0x76180407, vehicle.Handle, false);
                            SetAllJobsFalse();
                            k9Jobs["Idle"] = true;
                            subtanceFound = false;
                            alertTaskNumb = 0;
                            veh = 0;
                            vehicle = null;
                        }
                        else
                        {
                            await Delay(500);
                            while (API.GetSequenceProgress(dog.Handle) != 11) { await Delay(100); }
                            await Delay(6500);

                            ShowNotification("~r~No Search Results");
                            Function.Call((Hash)0x76180407, vehicle.Handle, false);
                            SetAllJobsFalse();
                            k9Jobs["Idle"] = true;
                            subtanceFound = false;
                            veh = 0;
                            vehicle = null;
                        }

                    }
                    else
                    {
                        ShowNotification("~r~No K9 on duty");
                        return;
                    }
                }
                if(_item == petDogBtn)
                {
                    if(dog != null)
                    {
                        if (!k9Jobs["Idle"] && !k9Jobs["Following"]) { ShowNotification("~r~K9 must be~s~ ~y~Idle~s~ ~r~or~s~ ~y~Following~s~"); return; }

                        ShowNotification("Stand next to the K9 to Pet it");

                        SetAllJobsFalse();
                        k9Jobs["Idle"] = true;

                        API.RequestAnimDict("creatures@rottweiler@tricks@");
                        while (!API.HasAnimDictLoaded("creatures@rottweiler@tricks@")) { await Delay(10); }
                        dog.Task.ClearAllImmediately();

                        Tick += PetDog;
                    }
                    else
                    {
                        ShowNotification("~r~No K9 on Duty");
                        return;
                    }
                }
                if(_item == haltBtn)
                {
                    if(dog != null)
                    {
                        ShowNotification("~r~K9 Action Stopped");

                        dog.Task.ClearAllImmediately();
                        SetAllJobsFalse();
                        k9Jobs["Idle"] = true;
                    }
                    else
                    {
                        ShowNotification("~r~No K9 on duty");
                        return;
                    }
                };
            };

            k9CommandsMenu.OnItemSelect += async (_menu, _item, _index) =>
            {
                if (!k9Jobs["Idle"] && !k9Jobs["Following"]) { ShowNotification("~r~K9 must be~s~ ~y~Idle~s~ ~r~or~s~ ~y~Following~s~"); return; }

                if (_item == sitCmdBtn)
                {
                    if(dog != null)
                    {
                        if(!isSitting)
                        {
                            SetAllJobsFalse();
                            k9Jobs["Doing Tricks"] = true;

                            ShowNotification("~y~Command~s~: ~b~Sit");
                            isSitting = true;

                            API.RequestAnimDict("creatures@rottweiler@amb@world_dog_sitting@enter");
                            while(!API.HasAnimDictLoaded("creatures@rottweiler@amb@world_dog_sitting@enter")) { await Delay(10); }

                            API.RequestAnimDict("creatures@rottweiler@amb@world_dog_sitting@base");
                            while (!API.HasAnimDictLoaded("creatures@rottweiler@amb@world_dog_sitting@base")) { await Delay(10); }

                            dog.Task.ClearAllImmediately();

                            TaskSequence ts = new TaskSequence();
                            ts.AddTask.PlayAnimation("creatures@rottweiler@amb@world_dog_sitting@enter", "base");
                            ts.AddTask.PlayAnimation("creatures@rottweiler@amb@world_dog_sitting@base", "base", 8.0f, -1, AnimationFlags.Loop);
                            ts.Close();

                            dog.Task.PerformSequence(ts);
                        }
                        else
                        {
                            SetAllJobsFalse();
                            k9Jobs["Idle"] = true;

                            ShowNotification("~y~Command~s~: ~b~Stand");
                            isSitting = false;
                            API.RequestAnimDict("creatures@rottweiler@amb@world_dog_sitting@exit");
                            while (!API.HasAnimDictLoaded("creatures@rottweiler@amb@world_dog_sitting@exit")) { await Delay(10); }

                            dog.Task.PlayAnimation("creatures@rottweiler@amb@world_dog_sitting@exit", "exit");
                        }
                    }
                    else
                    {
                        ShowNotification("~r~No K9 on Duty");
                        return;
                    }
                }
                if(_item == laydownCmdBtn)
                {
                    if(dog != null)
                    {
                        if(!isLayingDown)
                        {
                            SetAllJobsFalse();
                            k9Jobs["Doing Tricks"] = true;

                            ShowNotification("~y~Command~s~: ~b~Down");
                            isLayingDown = true;

                            API.RequestAnimDict("creatures@rottweiler@amb@sleep_in_kennel@");
                            while (!API.HasAnimDictLoaded("creatures@rottweiler@amb@sleep_in_kennel@")) { await Delay(10); }

                            dog.Task.ClearAllImmediately();
                            dog.Task.PlayAnimation("creatures@rottweiler@amb@sleep_in_kennel@", "sleep_in_kennel", 8.0f, -1, AnimationFlags.Loop);
                        }
                        else
                        {
                            SetAllJobsFalse();
                            k9Jobs["Idle"] = true;

                            ShowNotification("~y~Command~s~: ~b~Up");
                            isLayingDown = false;

                            API.RequestAnimDict("creatures@rottweiler@amb@sleep_in_kennel@");
                            while (!API.HasAnimDictLoaded("creatures@rottweiler@amb@sleep_in_kennel@")) { await Delay(10); }

                            dog.Task.PlayAnimation("creatures@rottweiler@amb@sleep_in_kennel@", "exit_kennel");
                        }
                    }
                    else
                    {
                        ShowNotification("~r~No K9 on Duty");
                        return;
                    }
                }
                if(_item == speakCmdBtn)
                {
                    if(dog != null)
                    {
                        SetAllJobsFalse();
                        k9Jobs["Doing Tricks"] = true;

                        ShowNotification("~y~Command~s~: ~b~Speak~s~");
                        dog.Task.ClearAllImmediately();

                        API.RequestAnimDict("creatures@rottweiler@amb@world_dog_barking@enter");
                        while (!API.HasAnimDictLoaded("creatures@rottweiler@amb@world_dog_barking@enter")) { await Delay(10); }

                        API.RequestAnimDict("creatures@rottweiler@amb@world_dog_barking@idle_a");
                        while (!API.HasAnimDictLoaded("creatures@rottweiler@amb@world_dog_barking@idle_a")) { await Delay(10); }

                        API.RequestAnimDict("creatures@rottweiler@amb@world_dog_barking@exit");
                        while (!API.HasAnimDictLoaded("creatures@rottweiler@amb@world_dog_barking@idle_a")) { await Delay(10); }

                        TaskSequence ts = new TaskSequence();
                        ts.AddTask.PlayAnimation("creatures@rottweiler@amb@world_dog_barking@enter", "enter");
                        ts.AddTask.PlayAnimation("creatures@rottweiler@amb@world_dog_barking@idle_a", "idle_a", 8.0f, 4500, AnimationFlags.Loop);
                        ts.AddTask.PlayAnimation("creatures@rottweiler@amb@world_dog_barking@exit", "exit");
                        ts.Close();

                        API.PlayAnimalVocalization(dog.Handle, 3, "bark");
                        dog.Task.PerformSequence(ts);

                        await Delay(5000);
                        SetAllJobsFalse();
                        k9Jobs["Idle"] = true;
                    }
                    else
                    {
                        ShowNotification("~r~No K9 on duty");
                        return;
                    }
                }
                if(_item == begCmdBtn)
                {
                    if(dog != null)
                    {
                        SetAllJobsFalse();
                        k9Jobs["Doing Tricks"] = true;

                        API.RequestAnimDict("creatures@rottweiler@tricks@");
                        while(!API.HasAnimDictLoaded("creatures@rottweiler@tricks@")) { await Delay(10); }

                        TaskSequence ts = new TaskSequence();
                        ts.AddTask.PlayAnimation("creatures@rottweiler@tricks@", "beg_enter");
                        ts.AddTask.PlayAnimation("creatures@rottweiler@tricks@", "beg_loop", 8.0f, 6500, AnimationFlags.Loop);
                        ts.AddTask.PlayAnimation("creatures@rottweiler@tricks@", "beg_exit");
                        ts.Close();

                        dog.Task.ClearAllImmediately();
                        API.PlayAnimalVocalization(dog.Handle, 3, "bark");
                        dog.Task.PerformSequence(ts);

                        await Delay(7000);
                        SetAllJobsFalse();
                        k9Jobs["Idle"] = true;
                    }
                    else
                    {
                        ShowNotification("~r~No K9 on Duty");
                        return;
                    }
                }
                if(_item == givePawCmdBtn)
                {
                    if(dog != null)
                    {
                        SetAllJobsFalse();
                        k9Jobs["Doing Tricks"] = true;

                        ShowNotification("~y~Command~s~: ~b~Give Paw~s~");
                        API.RequestAnimDict("creatures@rottweiler@tricks@");
                        while(!API.HasAnimDictLoaded("creatures@rottweiler@tricks@")) { await Delay(10); }

                        TaskSequence ts = new TaskSequence();
                        ts.AddTask.PlayAnimation("creatures@rottweiler@tricks@", "paw_right_enter");
                        ts.AddTask.PlayAnimation("creatures@rottweiler@tricks@", "paw_right_loop", 8.0f, 6500, AnimationFlags.Loop);
                        ts.AddTask.PlayAnimation("creatures@rottweiler@tricks@", "paw_right_exit");
                        ts.Close();

                        dog.Task.ClearAllImmediately();
                        dog.Task.PerformSequence(ts);

                        await Delay(7000);
                        SetAllJobsFalse();
                        k9Jobs["Idle"] = true;
                    }
                    else
                    {
                        ShowNotification("~r~No K9 on Duty");
                        return;
                    }
                }
                if(_item == playDeadCmdBtn)
                {
                    if(dog != null)
                    {
                        SetAllJobsFalse();
                        k9Jobs["Doing Tricks"] = true;

                        ShowNotification("~y~Command~s~: ~b~Play Dead~s~");
                        API.RequestAnimDict("creatures@rottweiler@move");
                        while (!API.HasAnimDictLoaded("creatures@rottweiler@move")) { await Delay(10); }

                        API.RequestAnimDict("creatures@rottweiler@getup");
                        while(!API.HasAnimDictLoaded("creatures@rottweiler@getup")) { await Delay(10); }

                        TaskSequence ts = new TaskSequence();
                        ts.AddTask.PlayAnimation("creatures@rottweiler@move", "dying");
                        ts.AddTask.PlayAnimation("creatures@rottweiler@move", "dead_right", 8.0f, 7500, AnimationFlags.Loop);
                        ts.AddTask.PlayAnimation("creatures@rottweiler@getup", "getup_l");
                        ts.Close();

                        dog.Task.ClearAllImmediately();
                        dog.Task.PerformSequence(ts);

                        await Delay(8500);
                        SetAllJobsFalse();
                        k9Jobs["Idle"] = true;
                    }
                }
                if(_item == playFetchCmdBtn)
                {
                    SetAllJobsFalse();
                    k9Jobs["Playing Fetch"] = true;

                    int prop = 0;

                    List<string> idles = new List<string>() { "trevor_impatient_wait_1", "trevor_impatient_wait_2", "trevor_impatient_wait_3", "trevor_impatient_wait_4" };

                    ShowNotification("~y~Command~s~: ~b~Play Fetch~s~");
                    API.RequestAnimDict("weapons@projectile@");
                    while (!API.HasAnimDictLoaded("weapons@projectile@")) { await Delay(10); }

                    API.RequestAnimDict("friends@frt@ig_1");
                    while (!API.HasAnimDictLoaded("friends@frt@ig_1")) { await Delay(10); }

                    API.RequestAnimDict("creatures@rottweiler@move");
                    while (!API.HasAnimDictLoaded("creatures@rottweiler@move")) { await Delay(10); }

                    API.RequestModel((uint)API.GetHashKey("prop_tennis_ball"));
                    while (!API.HasModelLoaded((uint)API.GetHashKey("prop_tennis_ball"))) { await Delay(10); }

                    TaskSequence ts = new TaskSequence();
                    ts.AddTask.PlayAnimation("weapons@projectile@", "throw_m_fb_stand");
                    ts.AddTask.PlayAnimation("friends@frt@ig_1", idles.SelectRandom(), 8.0f, -1, AnimationFlags.Loop);
                    ts.Close();

                    Vector3 fetchLocation = API.GetOffsetFromEntityInWorldCoords(Game.PlayerPed.Handle, 0, 10f, 0);

                    TaskSequence tsDog = new TaskSequence();
                    tsDog.AddTask.RunTo(fetchLocation);
                    tsDog.AddTask.PlayAnimation("creatures@rottweiler@move", "fetch_pickup");
                    tsDog.AddTask.RunTo(Game.PlayerPed.Position);
                    tsDog.AddTask.PlayAnimation("creatures@rottweiler@move", "fetch_drop");
                    tsDog.Close();


                    prop = API.CreateObject(API.GetHashKey("prop_tennis_ball"), 0, 0, 0, true, true, true);
                    API.AttachEntityToEntity(prop, Game.PlayerPed.Handle, API.GetPedBoneIndex(Game.PlayerPed.Handle, 6286), 0.0f, 0f, 0f, 0f, 0f, 0f, true, true, false, true, 1, true);
                    Game.PlayerPed.Task.PerformSequence(ts);
                    dog.Task.PerformSequence(tsDog);

                    await Delay(750);
                    API.SetModelAsNoLongerNeeded((uint)API.GetHashKey("prop_tennis_ball"));
                    API.DeleteObject(ref prop);

                    while(API.GetSequenceProgress(dog.Handle) != 1) { await Delay(15); }
                    prop = API.CreateObject(API.GetHashKey("prop_tennis_ball"), dog.Position.X, dog.Position.Y - 0.35f, dog.Position.Z, true, true, true);
                    API.AttachEntityToEntity(prop, dog.Handle, API.GetPedBoneIndex(dog.Handle, 46240), 0.1f, 0f, 0f, 0f, 0f, 0f, true, true, false, true, 1, true);

                    while(API.GetSequenceProgress(dog.Handle) != 3) { await Delay(15); }
                    await Delay(500);
                    API.DetachEntity(prop, true, false);
                    API.SetModelAsNoLongerNeeded((uint)API.GetHashKey("prop_tennis_ball"));
                    Game.PlayerPed.Task.ClearAll();

                    SetAllJobsFalse();
                    k9Jobs["Idle"] = true;

                    await Delay(3500);
                    API.DeleteObject(ref prop);
                }
                if(_item == shitCmdBtn)
                {
                    List<string> shits = new List<string>() { "prop_big_shit_01", "prop_big_shit_02" };
                    string shitModel = shits.SelectRandom();

                    SetAllJobsFalse();
                    k9Jobs["Taking a Dump"] = true;

                    API.RequestAnimDict("creatures@rottweiler@move");
                    while(!API.HasAnimDictLoaded("creatures@rottweiler@move")) { await Delay(10); }

                    API.RequestAnimDict("missfra0_chop_find");
                    while (!API.HasAnimDictLoaded("missfra0_chop_find")) { await Delay(10); }

                    API.RequestAnimDict("anim@mp_player_intcelebrationfemale@stinker");
                    while (!API.HasAnimDictLoaded("anim@mp_player_intcelebrationfemale@stinker")) { await Delay(10); }

                    API.RequestModel((uint)API.GetHashKey(shitModel));
                    while (!API.HasModelLoaded((uint)API.GetHashKey(shitModel))) { await Delay(10); }

                    TaskSequence ts = new TaskSequence();
                    ts.AddTask.PlayAnimation("missfra0_chop_find", "fra0_ig_14_chop_sniff_fwds", 8.0f, 3500, AnimationFlags.Loop);
                    ts.AddTask.PlayAnimation("creatures@rottweiler@move", "dump_enter");
                    ts.AddTask.PlayAnimation("creatures@rottweiler@move", "dump_loop", 8.0f, 2500, AnimationFlags.Loop);
                    ts.AddTask.PlayAnimation("creatures@rottweiler@move", "dump_exit");
                    ts.AddTask.GoTo(Game.PlayerPed);
                    ts.Close();

                    dog.Task.PerformSequence(ts);
                    await Delay(11750);

                    Vector3 offset = API.GetOffsetFromEntityInWorldCoords(dog.Handle, 0f, -0.35f, 0f);
                    float ground = 0; 
                    API.GetGroundZFor_3dCoord(offset.X, offset.Y, offset.Z, ref ground, true);

                    shit = API.CreateObject(API.GetHashKey(shitModel), offset.X, offset.Y, ground, true, true, true);
                    noShitCollision = true;
                    Tick += DogShitCollision;

                    while(API.GetSequenceProgress(dog.Handle) != 4) { await Delay(10); }

                    if (new Random().Next(0, 100) <= 25)
                    {
                        Game.PlayerPed.Task.PlayAnimation("anim@mp_player_intcelebrationfemale@stinker", "stinker", 8.0f, 4500, AnimationFlags.Loop);
                    }
                    await Delay(1000);

                    SetAllJobsFalse();
                    k9Jobs["Idle"] = true;
                    noShitCollision = false;

                    await Delay(7500);
                    API.DeleteObject(ref shit);
                    dog.Task.ClearAll();
                }
                if(_item == renameDogBtn)
                {
                    SetAllJobsFalse();
                    k9Jobs["Idle"] = true;

                    API.AddTextEntry("FMMC_KEY_TIP1", "Enter K-9 Name");
                    API.DisplayOnscreenKeyboard(1, "FMMC_KEY_TIP1", "", "", "", "", "", 25);

                    Tick += WaitForKeyboardInput;
                }
            };
        }
        private void LoadConfigData()
        {
            //Read JSON file
            string config = API.LoadResourceFile(API.GetCurrentResourceName(), "plugins/k9/config.json");
            var jsonConfig = JObject.Parse(config);

            //Get Substance and Bomb data
            //Drugs
            JToken jsonDrugs = jsonConfig["Drugs"];
            foreach (var item in jsonDrugs)
            {
                drugList.Add(item["Name"].ToString());
            }

            //Bombs
            JToken jsonBombs = jsonConfig["Bombs"];
            foreach(var item in jsonBombs)
            {
                bombList.Add(item["Name"].ToString());
            }

            //Load DiscordPerms Option
            JToken jsonAcePerms = jsonConfig["AcePerms"];
            useAcePerms = jsonAcePerms[0]["IsEnabled"].Value<bool>();
        }
        private async Task WaitForAttack()
        {
            if(!API.IsPlayerFreeAiming(API.PlayerId())) { return; }

            if (Game.IsControlJustPressed(0, (Control)74))
            {
                if(!API.GetEntityPlayerIsFreeAimingAt(API.PlayerId(), ref target)) { ShowNotification("~r~No Target"); return; }

                if (dog == null)
                {
                    ShowNotification("~r~No K9 on duty");
                    return;
                }

                if (k9Jobs["Attacking"])
                {
                    ShowNotification("~r~K9 is already attacking someone");
                    return;
                }

                if (!API.IsEntityAPed(target))
                {
                    ShowNotification("~r~Target not a Ped");
                    return;
                }

                if (API.IsEntityDead(target))
                {
                    ShowNotification("~r~Target is Dead");
                    return;
                }

                var peds = World.GetAllPeds().Where(p => World.GetDistance(p.Position, Game.PlayerPed.Position) <= 75f).OrderBy(p => World.GetDistance(p.Position, Game.PlayerPed.Position));
                foreach(Ped ped in peds)
                {
                    if(targetPed == null)
                    {
                        if (ped.Handle == target)
                        {
                            targetPed = ped;
                        }
                    }
                }

                API.TaskPutPedDirectlyIntoMelee(dog.Handle, targetPed.Handle, 0, -1f, 0, false);
                dog.MaxSpeed = 65f;

                SetAllJobsFalse();
                k9Jobs["Attacking"] = true;

                while(!API.IsEntityDead(targetPed.Handle)) { await Delay(10); }

                dog.Task.ClearAllImmediately();
                API.DetachEntity(dog.Handle, true, true);
                API.TaskFollowToOffsetOfEntity(dog.Handle, Game.PlayerPed.Handle, 0.5f, 0f, 0f, 5f, -1, 0f, true);

                ShowNotification("~g~Target Incapacitated");

                SetAllJobsFalse();
                k9Jobs["Following"] = true;

                Tick -= WaitForAttack;
                Tick += ReviveTarget;
            }

            await Task.FromResult(0);
        }
        private async Task ReviveTarget()
        {
            
            if(World.GetDistance(Game.PlayerPed.Position, targetPed.Position) > 1.5f) { DrawMarker(targetPed.Position); return; }
            Draw3dText("~y~Press~s~ ~r~[H]~s~ to ~y~Help Ped Up~s~", targetPed.Position);

            if(Game.IsControlJustPressed(0, (Control)74))
            {
                
                Tick -= ReviveTarget;
                targetPed.Resurrect();
                targetPed.Task.ClearAllImmediately();
                targetPed.BlockPermanentEvents = true;

                API.RequestAnimDict("amb@world_human_sunbathe@female@back@idle_a");
                while (!API.HasAnimDictLoaded("amb@world_human_sunbathe@female@back@idle_a")) { await Delay(10); }

                API.RequestAnimDict("rcm_barry3");
                API.RequestAnimDict("get_up@directional@movement@from_seated@drunk");
                API.RequestAnimDict("get_up@directional@transition@prone_to_seated@crawl");

                TaskSequence ts = new TaskSequence();
                ts.AddTask.PlayAnimation("amb@world_human_sunbathe@female@back@idle_a", "idle_a", 8.0f, 3500, AnimationFlags.Loop);
                ts.AddTask.PlayAnimation("rcm_barry3", "barry_3_sit_loop", 8.0f, 4500, AnimationFlags.Loop);
                ts.AddTask.PlayAnimation("get_up@directional@movement@from_seated@drunk", "getup_l_0");
                ts.AddTask.HandsUp(-1);
                ts.Close();

                targetPed.Task.PerformSequence(ts);
                API.RemoveDecalsInRange(targetPed.Position.X, targetPed.Position.Y, targetPed.Position.Z, 1.5f);

                targetPed = null;
                target = 0;
                
                Tick += WaitForAttack;
            }

            await Task.FromResult(0);
        }
        private async Task PetDog()
        {
            if(World.GetDistance(Game.PlayerPed.Position, dog.Position) > 1f) { return; }
            Draw3dText("~y~Press~s~ ~g~[H]~s~ to ~y~Pet K9~s~", dog.Position);

            if(Game.IsControlJustPressed(0, (Control)74))
            {
                Tick -= PetDog;

                SetAllJobsFalse();
                k9Jobs["Getting Pet"] = true;

                Game.PlayerPed.Task.PlayAnimation("creatures@rottweiler@tricks@", "petting_franklin", 8.0f, 6500, AnimationFlags.Loop);
                dog.Task.PlayAnimation("creatures@rottweiler@tricks@", "petting_chop", 8.0f, 6500, AnimationFlags.Loop);

                await Delay(6500);
                SetAllJobsFalse();
                k9Jobs["Idle"] = true;
            }

            await Task.FromResult(0);
        }
        private async Task DogCarCollision()
        {
            if(noCollision)
            {
                API.SetEntityNoCollisionEntity(dog.Handle, playerVehicle.Handle, true);
            }
            else
            {
                Tick -= DogCarCollision;
            }

            await Task.FromResult(0);
        }
        private async Task DogShitCollision()
        {
            if(noShitCollision)
            {
                API.SetEntityNoCollisionEntity(dog.Handle, shit, true);
            }
            else
            {
                Tick -= DogShitCollision;
            }

            await Task.FromResult(0);
        }
        private async Task K9OnDuty()
        {
            string duty = "~r~Off Duty";

            if(dog != null) { duty = "~g~On Duty"; }

            API.SetTextFont(0);
            API.SetTextOutline();
            API.SetTextScale(0.4f, 0.4f);
            API.SetTextEntry("STRING");
            API.AddTextComponentString($"{dogName} {duty}");
            API.DrawText(0.005f, 0.6f);

            if(dog != null) 
            {
                string currentJob = GetCurrentJob();

                API.SetTextFont(0);
                API.SetTextOutline();
                API.SetTextScale(0.4f, 0.4f);
                API.SetTextEntry("STRING");
                API.AddTextComponentString($"{dogName} is {currentJob}");
                API.DrawText(0.005f, 0.63f);
            }

            await Task.FromResult(0);
        }
        private async Task WaitForKeyboardInput()
        {
            int status = API.UpdateOnscreenKeyboard();
            if(status == -1) { Tick -= WaitForKeyboardInput; /*Keyboard not active when -1 is returned*/ }
            
            //Entered Name
            if(status == 1)
            {
                dogName = API.GetOnscreenKeyboardResult();
                Tick -= WaitForKeyboardInput;
            }

            //Escaped out
            if(status == 2)
            {
                Tick -= WaitForKeyboardInput;
                ShowNotification("~r~K-9 Name input cancelled");
            }

            await Task.FromResult(0);
        }
        private async Task ShowDogName()
        {
            if(World.GetDistance(Game.PlayerPed.Position, dog.Position) >= 6.5f) { return; }
            Draw3dText(dogName, dog.Position.ApplyOffset(new Vector3(0, 0, 0.5f)));

            await Task.FromResult(0);
        }
        private void BuildJobs()
        {
            k9Jobs = new Dictionary<string, bool>();

            k9Jobs.Add("Idle", false);
            k9Jobs.Add("Following", false);
            k9Jobs.Add("Sitting", false);
            k9Jobs.Add("Laying Down", false);
            k9Jobs.Add("Attacking", false);
            k9Jobs.Add("Searching", false);
            k9Jobs.Add("In Vehicle", false);
            k9Jobs.Add("Getting Pet", false);
            k9Jobs.Add("Doing Tricks", false);
            k9Jobs.Add("Playing Fetch", false);
            k9Jobs.Add("Taking a Dump", false);
        }
        private string GetCurrentJob()
        {
            string job = "Idle";

            foreach(var entry in k9Jobs)
            {
                if(entry.Value)
                {
                    job = entry.Key;
                    break;
                }
            }

            return job;
        }
        private void ShowNotification(string msg)
        {
            API.SetNotificationTextEntry("STRING");
            API.AddTextComponentString(msg);
            API.DrawNotification(true, true);
        }
        private void DrawMarker(Vector3 pos)
        {
            API.DrawMarker(2, pos.X, pos.Y, pos.Z + 0.5f, 0f, 0f, 0f, 0f, 0f, 0f, .4f, .4f, .4f, 255, 0, 0, 255, true, true, 2, true, null, null, false);
        }
        private void Draw3dText(string msg, Vector3 pos)
        {
            float textX = 0f, textY = 0f;
            Vector3 camLoc;
            API.World3dToScreen2d(pos.X, pos.Y, pos.Z, ref textX, ref textY);
            camLoc = API.GetGameplayCamCoords();
            float distance = API.GetDistanceBetweenCoords(camLoc.X, camLoc.Y, camLoc.Z, pos.X, pos.Y, pos.Z, true);
            float scale = (1 / distance) * 2;
            float fov = (1 / API.GetGameplayCamFov()) * 100;
            scale = scale * fov * 0.5f;

            API.SetTextScale(0.0f, scale);
            API.SetTextFont(0);
            API.SetTextProportional(true);
            API.SetTextColour(255, 255, 255, 215);
            API.SetTextDropshadow(0, 0, 0, 0, 255);
            API.SetTextEdge(2, 0, 0, 0, 150);
            API.SetTextDropShadow();
            API.SetTextOutline();
            API.SetTextEntry("STRING");
            API.SetTextCentre(true);
            API.AddTextComponentString(msg);
            API.DrawText(textX, textY);
        }
        private void GetNearestVehicle()
        {
            Vector3 playerPos = Game.PlayerPed.Position;
            Vector3 offset = API.GetOffsetFromEntityInWorldCoords(Game.PlayerPed.Handle, 0f, 5f, 0f);

            int raycast = API.StartShapeTestCapsule(playerPos.X, playerPos.Y, playerPos.Z, offset.X, offset.Y, offset.Z, 1.5f, 10, Game.PlayerPed.Handle, 7);

            ShapeTest(raycast);
        }
        private async void ShapeTest(int raycast)
        {
            bool hit = false;
            Vector3 hitCoords = Vector3.Zero;
            Vector3 surface = Vector3.Zero;
            int entityHit = 0;

            int rslt = API.GetShapeTestResult(raycast, ref hit, ref hitCoords, ref surface, ref entityHit);

            while(entityHit == 1)
            {
                await Delay(5);
                rslt = API.GetShapeTestResult(raycast, ref hit, ref hitCoords, ref surface, ref entityHit);
            }

            if(entityHit == 0)
            {
                ShowNotification("~r~No Vehicle to Search");
            }
            else
            {
                veh = entityHit;
                var vehs = World.GetAllVehicles().Where(v => World.GetDistance(Game.PlayerPed.Position, v.Position) <= 15f).OrderBy(v => World.GetDistance(Game.PlayerPed.Position, v.Position));

                foreach(Vehicle v in vehs)
                {
                    if(vehicle == null)
                    {
                        if(v.Handle == veh)
                        {
                            vehicle = v;
                        }
                    }
                }
            }
        }
        private void SetAllJobsFalse()
        {
            if(k9Jobs["Attacking"])
            {
                try
                {
                    //Reset ticks
                    Tick -= WaitForAttack;
                    Tick -= ReviveTarget;

                    //Add waiting for attack Tick
                    Tick += WaitForAttack;
                }
                catch
                {
                    //Nothing to do here. Bad error handling you could call it.
                }
            }

            //Try to reset any actions there were inprogress that require ticks
            try { Tick -= PetDog; } catch { }
            try { Tick -= DogCarCollision; } catch { }
            try { Tick -= DogShitCollision; } catch { }

            //Deselect the vehicle
            try { Function.Call((Hash)0x76180407, vehicle.Handle, false); } catch { }

            noCollision = false;
            noShitCollision = false;

            k9Jobs["Idle"] = false;
            k9Jobs["Following"] = false;
            k9Jobs["Sitting"] = false;
            k9Jobs["Laying Down"] = false;
            k9Jobs["Attacking"] = false;
            k9Jobs["Searching"] = false;
            k9Jobs["In Vehicle"] = false;
            k9Jobs["Getting Pet"] = false;
            k9Jobs["Doing Tricks"] = false;
            k9Jobs["Playing Fetch"] = false;
            k9Jobs["Taking a Dump"] = false;
        }
        private async Task<bool> VehicleContainsSubstance(Vehicle vehicleToSearch, DogType searchType)
        {
            bool hasSubstance = false;
            List<string> searchList;

            if(searchType == DogType.NARCOTICS)
            {
                searchList = drugList;
            }
            else
            {
                searchList = bombList;
            }

            VehicleData data = await vehicleToSearch.GetData();
            await Delay(250);
            foreach(Item i in data.Items)
            {
                if(i.IsIllegal)
                {
                    StringComparison comp = StringComparison.OrdinalIgnoreCase;
                    foreach(string drug in searchList)
                    {
                        int value = i.Name.IndexOf(drug, comp);
                        if(value >= 0)
                        {
                            hasSubstance = true;
                            alertTaskNumb = new Random().Next(8, 12);
                        }
                    }
                }
            }

            return hasSubstance;
        }
        private string GetModelFromConfig(string type)
        {
            string model = null;

            string config = API.LoadResourceFile(API.GetCurrentResourceName(), "plugins/k9/config.json");
            var jsonConfig = JObject.Parse(config);
            JToken json;
            json = jsonConfig["K9Data"];

            foreach(var item in json)
            {
                K9Data tmp = JsonConvert.DeserializeObject<K9Data>(item.ToString());
                
                if(tmp.DogType == type)
                {
                    model = tmp.Model;
                }
            }

            return model;
        }
    }

    public class K9Data
    {
        public string DogType;
        public string Model;

        public K9Data(string type, string model)
        {
            DogType = type;
            Model = model;
        }
    }
}

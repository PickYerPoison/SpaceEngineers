public const int MAX_ITEMS = 6;
public bool vending = false;

// usage:
//  make sure sean's inventory script is running!
//  set a connector or ejector up with the custom data "vending machine" in it
//      - set the "throw out" property to "on" and turn it off
//  set up a sensor that is set to detect only floating objects in an area covering the device's opening
//      - key up the actions so that it shuts the device off when it detects an object
//  pass, in the argument, the name of the item and the amount to vend (e.g. "SteelPlate 5")
public void Main(string argument)
{
    if (argument == "vend")
    {
        // retrieve vending machines
        var vendingMachines = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(vendingMachines, (IMyTerminalBlock x) => (x.CubeGrid == Me.CubeGrid && x.HasInventory && x.CustomData.Contains("vending machine")));
        
        // now vending
        vending = true;
        
        // set up each vending machine
        foreach (var machine in vendingMachines)
        {
            // turn on the vending machine
            machine.GetActionWithName("OnOff_On").Apply(machine);
        }
    }
    else if (argument == "stop")
    {
        // retrieve vending machines
        var vendingMachines = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(vendingMachines, (IMyTerminalBlock x) => (x.CubeGrid == Me.CubeGrid && x.HasInventory && x.CustomData.Contains("vending machine")));
        
        foreach (var machine in vendingMachines)
        {
            // turn off the vending machine
            machine.GetActionWithName("OnOff_Off").Apply(machine);
        }
        
        vending = false;
    }
    else if (vending)
    {
        // retrieve items in range
        var sensors = new List<IMySensorBlock>();
        GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, (IMySensorBlock x) => (x.CubeGrid == Me.CubeGrid && x.CustomData.Contains("vending sensor")));
        if (sensors.Count() > 0)
        {
            var detected = new List<MyDetectedEntityInfo>();
            sensors.First().DetectedEntities(detected);
            
            if (detected.Count() >= MAX_ITEMS)
            {
                // retrieve vending machines
                var vendingMachines = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(vendingMachines, (IMyTerminalBlock x) => (x.CubeGrid == Me.CubeGrid && x.HasInventory && x.CustomData.Contains("vending machine")));
                
                foreach (var machine in vendingMachines)
                {
                    // turn off the vending machine
                    machine.GetActionWithName("OnOff_Off").Apply(machine);
                }
                
                vending = false;
            }
        }
    }
}
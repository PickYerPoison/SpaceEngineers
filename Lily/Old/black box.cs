const int MAX_ENTRIES = 10000;

Dictionary<string, List<float>> Velocities;

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    Velocities = new Dictionary<string, List<float>>();
}

public void Main(string argument)  
{
    // get sensors
    var sensors = new List<IMySensorBlock>();
    GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, (IMySensorBlock x) => x.CubeGrid == Me.CubeGrid && x.DetectPlayers);
      
    // don't bother if no sensors  
    if (sensors.Count() > 0)  
    {  
        // get all detected entities  
        var detectedEntities = new List<MyDetectedEntityInfo>();  
        foreach (var sensor in sensors)  
        {
            // detect entities  
            var thisDetected = new List<MyDetectedEntityInfo>();   
            sensor.DetectedEntities(thisDetected);  
            bool inList = false;  
            foreach (var d in thisDetected)  
            {
                if (d.Type == MyDetectedEntityType.CharacterHuman)
                {
                    foreach (var t in detectedEntities)  
                    {  
                        if (d.EntityId == t.EntityId)  
                            inList = true;  
      
                        if (inList)  
                            break;  
                    }
                }
  
                if (!inList)  
                    detectedEntities.Add(d);  
            }  
        }
        
        // track players!
        foreach (var entity in detectedEntities)
        {
            var name = entity.Name;
            if (Velocities.ContainsKey(name) == false)
            {
                Velocities.Add(name, new List<float>());
            }
            Velocities[name].Add(entity.Velocity.Length());
            if (Velocities[name].Count() > MAX_ENTRIES)
            {
                Velocities[name].RemoveAt(0);
            }
        }
        
        // output data
        foreach (KeyValuePair<string, List<float>> player in Velocities)
        {
            Echo(player.Key + "'s stats:");
            Echo("   Average velocity: " + Math.Round(player.Value.Average(), 2).ToString() + " m/s");
            Echo("   Highest velocity: " + Math.Round(player.Value.Max(), 2).ToString() + " m/s");
            Echo("   Recent highest velocity: " + Math.Round(GetLastMax(player.Value), 2).ToString() + " m/s");
        }
    }  
}

T GetLastMax<T>(List<T> input) where T : IComparable<T>
{
    if (input.Count() == 0)
    {
        return default(T);
    }
    
    T maximum = input.Last();
    for (int i = input.Count() - 1; i > 0; i--)
    {
        if (input[i].CompareTo(maximum) >= 0)
        {
            maximum = input[i];
        }
        else
        {
            break;
        }
    }
    
    return maximum;
}
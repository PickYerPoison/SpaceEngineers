const bool DOORS_ENABLED = true;
const bool WELDERS_ENABLED = false;

// Sometimes tiny airlocks can activate too quickly and decompress the inner chamber. Use this to delay airlocks in general.
const int AIRLOCK_DELAY = 10;

Dictionary<IMyDoor, int> DoorCounters = new Dictionary<IMyDoor, int>();

// shared ini parser instance
MyIni _ini = new MyIni();

public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

public void Main(string argument)  
{  
    // get sensors  
    var sensors = new List<IMySensorBlock>();   
    GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, (IMySensorBlock x) => x.CubeGrid == Me.CubeGrid && x.DetectPlayers && x.CustomData == "");  
      
    // don't bother if no sensors  
    if (sensors.Count() > 0)  
    {  
        // get all detected entities  
        var detectedEntities = new List<MyDetectedEntityInfo>();  
        foreach (var sensor in sensors)  
        {  
            // set to max range  
            /*sensor.LeftExtend = sensor.MaxRange;
            sensor.RightExtend = sensor.MaxRange;
            sensor.TopExtend = sensor.MaxRange;
            sensor.BottomExtend = sensor.MaxRange;
            sensor.FrontExtend = sensor.MaxRange;
            sensor.BackExtend = sensor.MaxRange;*/
            
            // detect entities  
            var thisDetected = new List<MyDetectedEntityInfo>();   
            sensor.DetectedEntities(thisDetected);  
            bool inList = false;  
            foreach (var d in thisDetected)  
            {  
                foreach (var t in detectedEntities)  
                {  
                    if (d.EntityId == t.EntityId)  
                        inList = true;  
  
                    if (inList)  
                        break;  
                }  
  
                if (!inList)  
                    detectedEntities.Add(d);  
            }  
        }
        if (DOORS_ENABLED == true)
        {
            UpdateDoors(GridTerminalSystem, sensors, detectedEntities);
        }
        if (WELDERS_ENABLED == true)
        {
            UpdateWelders(GridTerminalSystem, sensors, detectedEntities);
        }
    }  
}  
  
public void UpdateDoors(IMyGridTerminalSystem grid, List<IMySensorBlock> sensors, List<MyDetectedEntityInfo> detectedEntities)  
{
    // iterate through delayed door actions
    var doorKeys = new List<IMyDoor>(DoorCounters.Keys);
    foreach (var doorKey in doorKeys)
    {
        DoorCounters[doorKey]--;
        if (DoorCounters[doorKey] < 0)
        {
            DoorCounters.Remove(doorKey);
        }
    }

    // get all doors  
    var doors = new List<IMyDoor>();  
    GridTerminalSystem.GetBlocksOfType<IMyDoor>(doors, (IMyDoor x) => x.CubeGrid == Me.CubeGrid && x.Enabled && !(x is IMyAirtightHangarDoor));
    
    // gather groups of doors
    Dictionary<string, List<IMyDoor>> doorGroups = new Dictionary<string, List<IMyDoor>>();
    foreach (var door in doors)
    {
        bool iniParsed = false;
        iniParsed = _ini.TryParse(door.CustomData);
        
        if (iniParsed == true)
        {
            if (_ini.ContainsKey("autodoor", "mode") && _ini.Get("autodoor", "mode").ToString() == "airlock" &&
                _ini.ContainsKey("autodoor", "group"))
            {
                // see if an existing list is in the dictionary
                string key = _ini.Get("autodoor", "group").ToString();
                if (key != "")
                {
                    if (doorGroups.ContainsKey(key) == false)
                    {
                        doorGroups.Add(key, new List<IMyDoor>());
                    }
                    doorGroups[key].Add(door);
                }
            }
        }
    }
    
    // find doors that should be open  
    int i = 0;
    while (i < doors.Count())
    {
        var door = doors[i];
        
        bool iniParsed = false;
        iniParsed = _ini.TryParse(door.CustomData);
        
        // determine if door is stated to be not autocontrolled
        if (iniParsed == true && _ini.ContainsKey("autodoor", "enabled") && _ini.Get("autodoor", "enabled").ToBoolean() == false)
        {
            doors.RemoveAt(i);
            continue;
        }
          
        // verify that door is within range of some sensor  
        bool isInRangeOfSensor = false;  
        foreach (var sensor in sensors)  
        {  
            if (Vector3D.Distance(door.GetPosition(), sensor.GetPosition()) <= sensor.MaxRange - 10) // 10m/s is as fast as you can run
            {  
                isInRangeOfSensor = true;  
                break;  
            }  
        }  
          
        // don't check the door if it's not in range of any sensors  
        if (!isInRangeOfSensor)  
        {
            doors.RemoveAt(i);
            continue;  
        }  
          
        // check if within the minimum distance to an enemy, and not within to any players  
        bool enemyWithinDistance = false;  
        bool playerWithinDistance = false;  
        foreach (var d in detectedEntities)  
        { 
            if (Vector3D.Distance(door.GetPosition(), d.Position) <= Math.Max(3, Vector3D.Dot(d.Velocity, Vector3D.Normalize(door.GetPosition() - d.Position)) + 0.5)) 
            {  
                if (d.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)//d.Type == MyDetectedEntityType.CharacterHuman)  
                {  
                    playerWithinDistance = true;  
                    break;  
                }  
                else if (d.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)//d.Type == MyDetectedEntityType.CharacterOther)  
                {  
                    enemyWithinDistance = true;  
                }  
            }  
        }  
          
        if (playerWithinDistance)  
        {
            bool canOpen = true;

            // before anything, check if this is an airlock!
            if (_ini.ContainsKey("autodoor", "mode") && _ini.Get("autodoor", "mode").ToString() == "airlock" &&
                _ini.ContainsKey("autodoor", "group"))
            {
                // see if there's an associated door list
                string key = _ini.Get("autodoor", "group").ToString();
                if (doorGroups.ContainsKey(key) == true)
                {
                    // if the list only contains this door, don't mess with it!
                    if (doorGroups[key].Count() == 1)
                    {
                        canOpen = false;
                    }
                    else
                    {
                        foreach (var otherDoor in doorGroups[key])
                        {
                            if (otherDoor != door &&
                                otherDoor.Status != DoorStatus.Closed)
                            {
                                canOpen = false;
                                break;
                            }
                        }
                    }
                }
                
                // attempt delay, if necessary
                if (canOpen == true && AIRLOCK_DELAY > 0)
                {
                    if (DoorCounters.ContainsKey(door) == false)
                    {
                        DoorCounters.Add(door, AIRLOCK_DELAY);
                    }
                }
            }
            
            if (canOpen == true)
            {
                // open the door  
                if (DoorCounters.ContainsKey(door) == false ||
                    DoorCounters[door] == 0)
                {
                    door.GetActionWithName("Open_On").Apply(door);
                }
            }
            
            // go to the next door
            doors.RemoveAt(i);
        }  
        else if (enemyWithinDistance)  
        {  
            // close the door  
            door.GetActionWithName("Open_Off").Apply(door);  
            
            // go to the next door
            doors.RemoveAt(i);
        }  
        else  
        {
            i++;
        }  
    }  
      
    // close all remaining doors  
    foreach (var door in doors)  
    {  
        bool iniParsed = false;
        MyIniParseResult iniData;
        iniParsed = _ini.TryParse(door.CustomData, out iniData);
        
        // determine if door is stated to be not autocontrolled
        if (iniParsed == true && _ini.ContainsKey("autodoor", "enabled") && _ini.Get("autodoor", "enabled").ToBoolean() == false)
        {
            // do not interact with this door
        }
        else
        {
            door.GetActionWithName("Open_Off").Apply(door);
        }
    }  
}  
  
public void UpdateWelders(IMyGridTerminalSystem grid, List<IMySensorBlock> sensors, List<MyDetectedEntityInfo> detectedEntities)  
{  
    // get all welders  
    var welders = new List<IMyShipWelder>();  
    GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(welders, (IMyShipWelder x) => x.CubeGrid == Me.CubeGrid && x.CustomData == "");  
      
    // find welders that should be on  
    int i = 0;  
    while (i < welders.Count())  
    {  
        var welder = welders[i];  
          
        // verify that welder is within range of some sensor  
        bool isInRangeOfSensor = false;  
        foreach (var sensor in sensors)  
        {  
            if (Vector3D.Distance(welder.GetPosition(), sensor.GetPosition()) <= (50)) // 10m/s is as fast as you can run (50 - 10 = 40)  
            {  
                isInRangeOfSensor = true;  
                break;  
            }  
        }  
          
        // don't check the welder if it's not in range of any sensors  
        if (!isInRangeOfSensor)  
        {  
            // remove the welder from the welders list  
            welders.RemoveAt(i);  
            continue;  
        }  
          
        // check if within the minimum distance to an enemy, and not within to any players  
        bool enemyWithinDistance = false;  
        bool playerWithinDistance = false;  
        foreach (var d in detectedEntities)  
        {  
            if (Vector3D.Distance(welder.GetPosition(), d.Position) <= 7)  
            {  
                if (d.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)//d.Type == MyDetectedEntityType.CharacterHuman)  
                {  
                    playerWithinDistance = true;  
                    break;  
                }  
                else if (d.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)//d.Type == MyDetectedEntityType.CharacterOther)  
                {  
                    enemyWithinDistance = true;  
                }  
            }  
        }  
          
        if (!playerWithinDistance && enemyWithinDistance)  
        {  
            // turn the welder on  
            welder.Enabled = true;  
            //welder.GetActionWithName("Open_On").Apply(welder);  
              
            // remove the welder from the welders list  
            welders.RemoveAt(i);  
        }  
        else if (playerWithinDistance)  
        {  
            welder.Enabled = false;  
            welders.RemoveAt(i);  
        }  
        else  
        {  
            i++;  
        }  
    }  
      
    // close all remaining welders  
    foreach (var welder in welders)  
    {  
        welder.Enabled = false;  
        //welder.GetActionWithName("Open_Off").Apply(welder);  
    }  
}
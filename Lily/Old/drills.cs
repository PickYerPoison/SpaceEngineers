const double SAFE_ROTATION = Math.PI / 64;
const float SAFE_SPEED = 1f;

PistonStatus DrillingState = PistonStatus.Retracted;

List<IMyShipDrill> Drills;
List<IMyMotorStator> Rotors;
List<IMyPistonBase> Pistons;

public Program()
{
    Drills = new List<IMyShipDrill>(); 
    GridTerminalSystem.GetBlocksOfType<IMyShipDrill>(Drills);
    
    Rotors = new List<IMyMotorStator>(); 
    GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(Rotors);
    
    Pistons = new List<IMyPistonBase>(); 
    GridTerminalSystem.GetBlocksOfType<IMyPistonBase>(Pistons, (IMyPistonBase x) => x.CubeGrid == Me.CubeGrid);
    
    foreach (var piston in Pistons)
    {
        if (piston.Status != PistonStatus.Retracted)
        {
            DrillingState = PistonStatus.Retracting;
            break;
        }
    }
    
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

public void Main(string argument, UpdateType updateSource)
{
    if ((updateSource & (UpdateType.Script | UpdateType.Terminal | UpdateType.Trigger)) != 0)
    {
        switch (argument.ToLower())
        {
            case "retract":
            {
                DrillingState = PistonStatus.Retracting;
                break;
            }
            case "extend":
            {
                DrillingState = PistonStatus.Extending;
                foreach (var rotor in Rotors)
                {
                    if (rotor.TargetVelocityRPM < 0)
                    {
                        rotor.TargetVelocityRPM = -SAFE_SPEED;
                    }
                    else
                    {
                        rotor.TargetVelocityRPM = SAFE_SPEED;
                    }
                }
                break;
            }
            default: break;
        }
    }
    else if ((updateSource & UpdateType.Update1) != 0)
    {
        // show current state
        switch (DrillingState)
        {
            case PistonStatus.Extended:
                Echo("Extended");
                break;
            case PistonStatus.Extending:
                Echo("Extending");
                break;
            case PistonStatus.Retracting:
                Echo("Retracting");
                break;
            case PistonStatus.Retracted:
                Echo("Retracted");
                break;
            case PistonStatus.Stopped:
                Echo("Stopped");
                break;
            default:
                Echo("Unknown state");
                break;
        }
        
        // turn on/off drills
        /*bool enableDrills = DrillingState == PistonStatus.Extended;
        foreach (var drill in Drills)
        {
            drill.Enabled = enableDrills;
        }*/
        
        // turn on/off rotors
        bool rotorsOff = true;
        foreach (var rotor in Rotors)
        {
            if (DrillingState != PistonStatus.Extended && 
               ((rotor.Angle < SAFE_ROTATION || rotor.Angle > Math.PI*2 - SAFE_ROTATION) ||
                (rotor.Angle > Math.PI - SAFE_ROTATION && rotor.Angle < Math.PI + SAFE_ROTATION)))
            {
                rotor.Enabled = false;
            }
            
            if (DrillingState == PistonStatus.Extended)
            {
                if (rotor.Enabled == false)
                {
                    if (rotor != Rotors.First())
                    {
                        if ((Rotors.First().Angle < Math.PI * 0.5 + SAFE_ROTATION && Rotors.First().Angle > Math.PI * 0.5 - SAFE_ROTATION) ||
                            (Rotors.First().Angle < Math.PI * 1.5 + SAFE_ROTATION && Rotors.First().Angle > Math.PI * 1.5 - SAFE_ROTATION))
                        {
                            rotor.Enabled = true;
                        }
                    }
                    else
                    {
                        rotor.Enabled = true;
                    }
                }
            }
            else if (DrillingState == PistonStatus.Retracting)
            {
                int mod = 1;
                if (rotor.TargetVelocityRPM < 0)
                {
                    mod = -1;
                }
                
                double distRemaining = Math.PI;
                if (mod == 1)
                {
                    if (rotor.Angle > Math.PI)
                    {
                        distRemaining = Math.PI * 2 - rotor.Angle;
                    }
                    else
                    {
                        distRemaining = Math.PI - rotor.Angle;
                    }
                }
                else
                {
                    if (rotor.Angle < Math.PI)
                    {
                        distRemaining = rotor.Angle;
                    }
                    else
                    {
                        distRemaining = rotor.Angle - Math.PI;
                    }
                }
                
                float targetVelocity = (float)Math.Min(distRemaining, Math.PI / 8) * mod;
                rotor.TargetVelocityRad = targetVelocity;
            }
            
            if (rotorsOff == true && rotor.Enabled == true)
            {
                rotorsOff = false;
            }
        }
        
        if (DrillingState == PistonStatus.Extending)
        {
            bool allExtended = true;
            foreach (var piston in Pistons)
            {
                piston.Extend();
                if (piston.Status != PistonStatus.Extended)
                {
                    allExtended = false;
                }
            }
            
            if (allExtended == true)
            {
                DrillingState = PistonStatus.Extended;
            }
        }
        else if (DrillingState == PistonStatus.Retracting && rotorsOff == true)
        {
            bool allRetracted = true;
            foreach (var piston in Pistons)
            {
                piston.Retract();
                if (piston.Status != PistonStatus.Retracted)
                {
                    allRetracted = false;
                }
            }
            
            if (allRetracted == true)
            {
                DrillingState = PistonStatus.Retracted;
            }
        }
    }
}
// wander-tan features: 
// any terminal block: put "nwt" in the custom data to forbid wander-tan from using it 
// control block (cockpit, passenger seat, remote control, etc): enables detection of downward direction and forward-facing direction. if there are multiple that face in different directions, put "wt-primary" in the custom data of one that faces forward. 
//                                                               in compatible modes, also enables user steering. 
// sensor: enables detection of entities 
// camera (forward-facing and right-side-up): enables wall detection 
// LCD screen: enables qt face~ 
  
// version tracking/data loading 
public const double CODE_VERSION = 1.0; 
   
// movement constants 
public const float MAX_VELOCITY = 10f; 
public const float MAX_RPM = 20f; 
public static float STOP_DISTANCE = 10f;  
public const double START_TURN = 0;  
public const double END_TURN = Math.PI / 4; 
public const float RPM_RAMPUP = MAX_RPM / 30f; 
    
// suspension constants 
public const int SUSPENSION_COUNTDOWN = 10; 
public double SUSPENSION_TORQUE_MOD = 1.5; // recommended 1.0-2.0. middling values can help with flatter terrain speed, lower values help with hill climbing 
public const double SUSPENSION_TORQUE_MOD_ADJUST = 0.1; 
public const float SUSPENSION_TORQUE_VALUE = 8000f; 
public const float SUSPENSION_RPM = 5f; 
public const float SUSPENSION_LOWER_LIMIT = -5f; 
public const float SUSPENSION_UPPER_LIMIT = 10f; 
  
// raycasting constants 
public const float RAYCAST_HI_ANGLE = -10f; 
public const float RAYCAST_LO_ANGLE = -15f; 
public float RAYCAST_DISTANCE = STOP_DISTANCE; 
public const float RAYCAST_TOLERANCE = 0.05f; 
public const float RAYCAST_MINIMUM_DISTANCE = 0.001f; 
public const float RAYCAST_CONSECUTIVE_FAILS = 3; 
    
// movement variables 
public Vector3D CurrentTargetLocation;  
public float CurrentRPM = 0f;  
public int backdown = 0; 
public bool forceSitDown = false; 
public bool sitDown = false; 
    
// raycasting variables 
public int raycastConsistencyTracker;

public List<IMyMotorStator> RotorsLeft, RotorsRight, RotorsSuspension; 
 
public Program() 
{ 
    // begin update frequency 
    Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10; 
     
    // initialize text log 
    TextLog = new List<string>();  
      
    // set up backdown 
    backdown = 0;  
      
    // track raycast consistency 
    raycastConsistencyTracker = 0; 
      
    // set up initial target to current position 
    CurrentTargetLocation = Me.Position; 
     
    // initial chassis scanning/setup 
    RotorsLeft = new List<IMyMotorStator>(); 
    RotorsRight = new List<IMyMotorStator>(); 
    RotorsSuspension = new List<IMyMotorStator>(); 
    GatherWheelsAndSuspension(GridTerminalSystem, Me); 
      
    // clear warnings 
    Warn(""); 
} 
 
public void Main(string argument, UpdateType updateType) 
{
    if (argument == "sit") 
    { 
        forceSitDown = !forceSitDown; 
    } 
    else if (argument == "") 
    { 
        Warn(""); 
         
        if (GetSitDown()) 
        { 
            Stop(); 
        } 
         
        // set up reference used for various code functions 
        IMyCubeBlock reference = Me; 
         
        // find if there are any ship controllers (cockpits, remote control blocks, etc) 
        var controllers = new List<IMyShipController>(); 
        GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers, (IMyShipController x) => x.CubeGrid == Me.CubeGrid); 
        if (controllers.Count() > 0) 
        { 
            reference = controllers[0]; 
        } 
         
        if ((updateType & UpdateType.Update10) != 0) 
        { 
            var landingGears = new List<IMyLandingGear>(); 
            GridTerminalSystem.GetBlocksOfType<IMyLandingGear>(landingGears, (IMyLandingGear x) => x.CubeGrid == reference.CubeGrid); 
            if (landingGears.Count() > 0) 
            { 
                SetUpSuspension(GridTerminalSystem, reference); 
             
                if (GetSitDown() && RotorsSuspension[0].Angle < 2) 
                { 
                    if (!landingGears[0].IsLocked) 
                    { 
                        landingGears[0].GetActionWithName("Lock").Apply(landingGears[0]); 
                    } 
                } 
                else 
                { 
                    if (landingGears[0].IsLocked) 
                    { 
                        landingGears[0].GetActionWithName("Unlock").Apply(landingGears[0]); 
                    } 
                } 
            } 
        } 
          
        // set up stop distance to properly reflect current grid size 
        var boundingBox = Me.CubeGrid.WorldAABB; 
        //Log(boundingBox.Size.Length());  
        STOP_DISTANCE = (float)(boundingBox.Size.Length() / 2 * 1.5); 
        if (controllers.Count() > 0) 
        { 
            var velocity = controllers[0].GetShipSpeed(); 
            if (velocity > 6) 
            { 
                STOP_DISTANCE *= (float)(velocity / 6); 
            } 
        } 
         
        if (backdown > 0) 
        { 
            backdown--; 
 
            Move(Math.PI); 
 
            if (backdown == 0) 
                Stop(); 
        } 
         
        var detected = new List<MyDetectedEntityInfo>(); 
         
        // get the face sensors 
        var sensors = new List<IMySensorBlock>();  
        GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, (IMySensorBlock x) => x.CubeGrid == Me.CubeGrid);  
 
        if (sensors.Count() > 0) 
        { 
            // check if there are detected entities 
            foreach (var sensor in sensors) 
            { 
                var thisDetected = new List<MyDetectedEntityInfo>();  
                sensor.DetectedEntities(thisDetected); 
                bool inList = false; 
                foreach (var d in thisDetected) 
                { 
                    foreach (var t in detected) 
                    { 
                        if (d.EntityId == t.EntityId) 
                            inList = true; 
                          
                        if (inList) 
                            break; 
                    } 
 
                    if (!inList) 
                        detected.Add(d); 
                } 
            } 
             
            // check whether to stop or if no entities were detected 
            if (detected.Count() > 0) 
            { 
                double minDist = -1; 
                double x = 0; 
                double y = 0; 
                double z = 0; 
                 
                // stop if close enough to any entities 
                // also, gather overall target direction 
                foreach (var target in detected) 
                { 
                    var tdist = Vector3D.Distance(target.Position, boundingBox.Center);  
                    if (minDist == -1 || tdist < minDist) 
                    { 
                        minDist = tdist; 
                        x = target.Position.X; 
                        y = target.Position.Y; 
                        z = target.Position.Z; 
                    } 
                } 
                 
                CurrentTargetLocation = new Vector3D(x, y, z); 
            }
            else
            {
                CurrentTargetLocation = Me.Position; 
            }
        }
        else
        {
            CurrentTargetLocation = Me.Position; 
        }
         
        if (backdown <= 0) 
        { 
            // check cameras for forcing backdown 
            if (!RaycastCheckTerrain(GridTerminalSystem, reference)) 
                backdown = 50; 
 
            // stop wander-tan from tipping over 
            var remoteControls = new List<IMyShipController>();  
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(remoteControls, (IMyShipController x) => x.CubeGrid == Me.CubeGrid);  
 
            if (remoteControls.Count() > 0) 
            { 
                var gv = remoteControls.First().GetTotalGravity();  
                var fv = Me.WorldMatrix.Forward;  
                var angle = Math.Acos(fv.Dot(gv) / (fv.Length() * gv.Length()));  
                if (angle > Math.PI * 3/4 || angle < Math.PI * 1/4) 
                    backdown = 50; 
            } 
             
            double minDist = -1; 
             
            var centerPoint = boundingBox.Center; 
            var distToCTL = Vector3D.Distance(CurrentTargetLocation, centerPoint); 
            if (minDist == -1 || distToCTL < minDist) 
                minDist = distToCTL; 
            if (minDist <= STOP_DISTANCE) 
            { 
                if (detected.Count() == 0) 
                { 
                    Stop(); 
                    sitDown = true; 
                } 
                else if (!GetSitDown()) 
                { 
                    Rotate(reference, CurrentTargetLocation); 
                } 
            } 
            else if (minDist <= 100) 
            { 
                sitDown = false; 
                if (!GetSitDown()) 
                { 
                    Move(reference, CurrentTargetLocation); 
                } 
            } 
            else 
            { 
                Stop(); 
                sitDown = true; 
            } 
        } 
    } 
 
    foreach (var line in TextLog) 
    { 
        Echo(line);  
    } 
} 
 
public bool GetSitDown() 
{ 
    return sitDown || forceSitDown; 
} 
 
// returns whether or not suspension is present 
public bool HasSuspension() 
{ 
    return RotorsSuspension != null && RotorsSuspension.Count() > 0; 
} 
   
// populates (or re-populates) the list of wheels and suspension rotors 
public void GatherWheelsAndSuspension(IMyGridTerminalSystem grid, IMyCubeBlock reference) 
{ 
    // clear wheels and suspension 
    RotorsRight.Clear(); 
    RotorsLeft.Clear(); 
    RotorsSuspension.Clear(); 
    
    var rotors = new List<IMyMotorStator>();  
    grid.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CubeGrid != reference.CubeGrid); 
    if (rotors.Count() > 0) 
    { 
        // it has suspension! first, gather the wheels 
        foreach (var rotor in rotors) 
        { 
            var localRotorPosition = Vector3D.Transform(rotor.GetPosition(), MatrixD.Invert(reference.WorldMatrix));  
            double angle = Math.Atan2(-localRotorPosition.X, -localRotorPosition.Z);  
    
            if (angle > 0) 
                RotorsRight.Add(rotor);  
            else 
                RotorsLeft.Add(rotor);  
        } 
    } 
    
    // now sort out wheels that are connected to the main grid 
    rotors = new List<IMyMotorStator>();  
    grid.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CubeGrid == reference.CubeGrid); 
    bool hasSuspension = RotorsLeft.Count() > 0 || RotorsRight.Count() > 0; 
      
    foreach (var rotor in rotors) 
    { 
        if (hasSuspension) 
        { 
            RotorsSuspension.Add(rotor); 
        } 
        else 
        { 
            var localRotorPosition = Vector3D.Transform(rotor.GetPosition(), MatrixD.Invert(reference.WorldMatrix));  
            double angle = Math.Atan2(-localRotorPosition.X, -localRotorPosition.Z);  
  
            if (angle > 0) 
                RotorsRight.Add(rotor); 
            else 
                RotorsLeft.Add(rotor);  
        } 
    } 
}     
    
// sets up suspension 
public void SetUpSuspension(IMyGridTerminalSystem grid, IMyCubeBlock reference) 
{ 
    if (HasSuspension()) 
    { 
        foreach (var rotor in RotorsSuspension) 
        { 
            var localRotorPosition = Vector3D.Transform(rotor.GetPosition(), MatrixD.Invert(reference.WorldMatrix));  
            double angle = Math.Atan2(-localRotorPosition.X, -localRotorPosition.Z);  
    
            // stop wander-tan from tipping over 
            var controllers = new List<IMyShipController>();  
            grid.GetBlocksOfType<IMyShipController>(controllers, (IMyShipController x) => x.CubeGrid == reference.CubeGrid); 
             
            bool inverted = false; 
            if (angle >= 0 && angle <= Math.PI) 
            { 
                inverted = rotor.Orientation.Up != reference.Orientation.Forward; 
            } 
            else 
            { 
                inverted = rotor.Orientation.Up == reference.Orientation.Forward; 
            } 
             
            float velocity = 10f; 
            if (inverted) 
                velocity = -velocity; 
            if (GetSitDown()) 
                velocity = -velocity; 
             
            rotor.SetValueFloat("Velocity", velocity); 
        } 
    } 
} 
    
// stops wander-tan completely 
public void Stop() 
{ 
    CurrentRPM = 0; 
    
    foreach (var rotor in RotorsRight) 
        rotor.SetValueFloat("Velocity", 0);  
    foreach (var rotor in RotorsLeft) 
        rotor.SetValueFloat("Velocity", 0); 
} 
  
// directs wander-tan to face towards a certain point in 3D space 
public void Rotate(IMyCubeBlock reference, Vector3D target) 
{ 
    Move(reference, target, 0, 0.1, 0.1); 
} 
  
// directs wander-tan to rotate towards a certain angle relative to her current facing direction 
public void Rotate(double angle) 
{ 
    Move(angle, 0, 0.1, 0.1); 
} 
  
// directs wander-tan to move towards a certain point in 3D space 
public void Move(IMyCubeBlock reference, Vector3D target, float maxRPM = MAX_RPM, double startTurn = START_TURN, double endTurn = END_TURN) 
{ 
    var localTargetPosition = Vector3D.Transform(target, MatrixD.Invert(reference.WorldMatrix));  
    double angle = -Math.Atan2(-localTargetPosition.X, -localTargetPosition.Z); 
    Move(-angle, maxRPM, startTurn, endTurn); 
} 
    
// directs wander-tan to move towards a certain angle relative to her current facing direction. positive = to the right 
// angle is given in radians 
//        0 
// -π/2       π/2 
//        π 
public void Move(double angle, float maxRPM = MAX_RPM, double startTurn = START_TURN, double endTurn = END_TURN) 
{ 
    // stop if either side has no wheels 
    bool stopLeft = false; 
    bool stopRight = false; 
    if (RotorsLeft.Count() == 0) 
    { 
        stopLeft = true; 
    } 
    if (RotorsRight.Count() == 0) 
    { 
        stopRight = true; 
    } 
    if (stopLeft || stopRight) 
    { 
        Stop(); 
        return; 
    } 
        
    var RPM_ToUse = CurrentRPM; 
        
    // move in reverse 
    if (angle == Math.PI || angle == -Math.PI) 
    { 
        if (CurrentRPM > -maxRPM / 2f) 
            CurrentRPM -=RPM_RAMPUP; 
        if (CurrentRPM <= -maxRPM / 2f) 
            CurrentRPM = -maxRPM / 2f; 
            
        foreach (var rotor in RotorsRight) 
            rotor.SetValueFloat("Velocity", -RPM_ToUse); 
        foreach (var rotor in RotorsLeft) 
            rotor.SetValueFloat("Velocity", RPM_ToUse); 
    } 
    else 
    { 
        // ramp up speed to avoid popping wheelies 
        if (CurrentRPM < maxRPM) 
            CurrentRPM += RPM_RAMPUP; 
    
        if (CurrentRPM > maxRPM) 
            CurrentRPM = maxRPM; 
    
        // turn left 
        if (angle <= -startTurn) 
        { 
            if (angle <= -endTurn) 
                RPM_ToUse = MAX_RPM; 
            foreach (var rotor in RotorsRight) 
                rotor.SetValueFloat("Velocity", -RPM_ToUse);  
            foreach (var rotor in RotorsLeft) 
            { 
                if (angle <= -endTurn) 
                    rotor.SetValueFloat("Velocity", -RPM_ToUse);  
                else 
                    rotor.SetValueFloat("Velocity", -(float)(-((2 * RPM_ToUse) / (endTurn - startTurn) * angle + RPM_ToUse)));  
            } 
        } 
        // turn right 
        else if (angle >= startTurn) 
        { 
            if (angle >= endTurn) 
                RPM_ToUse = MAX_RPM; 
            foreach (var rotor in RotorsRight) 
            { 
                if (angle >= endTurn) 
                    rotor.SetValueFloat("Velocity", RPM_ToUse);  
                else 
                    rotor.SetValueFloat("Velocity", (float)((2 * RPM_ToUse) / (endTurn - startTurn) * angle + RPM_ToUse));  
            } 
            foreach (var rotor in RotorsLeft) 
                rotor.SetValueFloat("Velocity", RPM_ToUse);  
        } 
        // go straight 
        else 
        { 
            foreach (var rotor in RotorsRight) 
                rotor.SetValueFloat("Velocity", RPM_ToUse);  
            foreach (var rotor in RotorsLeft) 
                rotor.SetValueFloat("Velocity", -RPM_ToUse);  
        } 
    } 
} 
    
// checks for terrain ahead using raycasting and returns true if the terrain is okay, or false if it isn't 
public bool RaycastCheckTerrain(IMyGridTerminalSystem grid, IMyCubeBlock reference) 
{ 
    // get forward-facing, upright cameras 
    var cameras = new List<IMyCameraBlock>(); 
    grid.GetBlocksOfType<IMyCameraBlock>(cameras, (IMyCameraBlock x) => x.CubeGrid == reference.CubeGrid && !(x.CustomData.Contains("nwt")));// && x.Orientation.Forward == reference.Orientation.Forward && x.Orientation.Up == reference.Orientation.Up); 
    
    // calculate distances 
    float RAYCAST_HI_DISTANCE = (float)(RAYCAST_DISTANCE / Math.Cos(RAYCAST_HI_ANGLE * Math.PI / 180)); 
    float RAYCAST_LO_DISTANCE = (float)(RAYCAST_DISTANCE / Math.Cos(RAYCAST_LO_ANGLE * Math.PI / 180)); 
    
    foreach (var camera in cameras) 
    { 
        // set able to raycast 
        camera.EnableRaycast = true; 
    
        // ensure both raycasts can be done 
        if (camera.CanScan(RAYCAST_HI_DISTANCE + RAYCAST_LO_DISTANCE)) 
        { 
            var detectedHi = camera.Raycast(RAYCAST_HI_DISTANCE, RAYCAST_HI_ANGLE); 
            var detectedLo = camera.Raycast(RAYCAST_LO_DISTANCE, RAYCAST_LO_ANGLE); 
    
            bool wasDetectedHi = detectedHi.Type == MyDetectedEntityType.SmallGrid || 
                                 detectedHi.Type == MyDetectedEntityType.LargeGrid || 
                                 detectedHi.Type == MyDetectedEntityType.Asteroid || 
                                 detectedHi.Type == MyDetectedEntityType.Planet; 
            bool wasDetectedLo = detectedLo.Type == MyDetectedEntityType.SmallGrid || 
                                 detectedLo.Type == MyDetectedEntityType.LargeGrid || 
                                 detectedLo.Type == MyDetectedEntityType.Asteroid || 
                                 detectedLo.Type == MyDetectedEntityType.Planet; 
             
            // if nothing was detected low, STOP! 
            if (!wasDetectedLo) 
            { 
                return false; 
            } 
            // if both were detected, do more calculations 
            else if (wasDetectedHi && wasDetectedLo) 
            { 
                // calculate distances of both 
                var distHi = Math.Cos(RAYCAST_HI_ANGLE * Math.PI / 180) * Vector3D.Distance(camera.GetPosition(), (Vector3D)(detectedHi.HitPosition)); 
                var distLo = Math.Cos(RAYCAST_LO_ANGLE * Math.PI / 180) * Vector3D.Distance(camera.GetPosition(), (Vector3D)(detectedLo.HitPosition)); 
    
                if (distHi >= RAYCAST_MINIMUM_DISTANCE && distLo >= RAYCAST_MINIMUM_DISTANCE) 
                { 
                    // retrieve LCDs 
                    var LCDs = new List<IMyTextPanel>();  
                    grid.GetBlocksOfType<IMyTextPanel>(LCDs, (IMyTextPanel x) => x.CustomData.Contains("debug")); 
                    foreach (var LCD in LCDs) 
                    { 
                        LCD.WritePublicText(distHi.ToString() + "\n" + distLo.ToString() + "\n" + (Math.Abs(distHi - distLo)).ToString() + "\n" + (detectedHi.EntityId == detectedLo.EntityId).ToString() + "\n" + 
                        (Math.Abs(distHi - distLo) <= RAYCAST_TOLERANCE).ToString());  
                    } 
    
                    // see if high and low distances are within the tolerance of each other (flat wall) 
                    if (Math.Abs(distHi - distLo) <= RAYCAST_TOLERANCE) 
                    { 
                        if (raycastConsistencyTracker < RAYCAST_CONSECUTIVE_FAILS) 
                        { 
                            raycastConsistencyTracker++; 
                            return true; 
                        } 
                        else 
                            return false; 
                    } 
                } 
            } 
        } 
    } 
    
    // no problems encountered 
    raycastConsistencyTracker = 0; 
    return true; 
} 
 
public static List<string> TextLog;  
   
public static void Log(object text) 
{ 
    if (TextLog.Count() == 0) 
        TextLog.Add(""); 
    TextLog.Add(text.ToString()); 
    if (TextLog.Count() > 10) 
        TextLog.RemoveAt(1); 
} 
   
public static void Warn(object text) 
{ 
    if (TextLog.Count() == 0) 
        TextLog.Add(""); 
    TextLog[0] = text.ToString(); 
}
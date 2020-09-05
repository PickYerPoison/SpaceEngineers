// wander-tan features:
// any terminal block: put "nwt" in the custom data to forbid wander-tan from using it
// control block (cockpit, passenger seat, remote control, etc): enables detection of downward direction and forward-facing direction. if there are multiple that face in different directions, put "wt-facing" in the custom data of one that faces forward.
//                                                               in compatible modes, also enables user steering.
// sensor: enables detection of entities
// camera (forward-facing and right-side-up): enables wall detection
// LCD screen: enables qt face~

// version tracking/data loading
public const double CODE_VERSION = 1.0;
  
// movement constants
public const float MAX_VELOCITY = 5f;
public const float MIN_VELOCITY = 1f;
public const float MAX_RPM = 60f;
public const float MAX_ACCELERATION = 0.1f;
public const float MIN_ACCELERATION = 0.1f;
public const float BUFFER_DISTANCE = 5f;
public const float RPM_RAMPUP = MAX_RPM / 30f;
public const float DANGEROUS_VELOCITY = 10f;
public static float STOP_DISTANCE = 2f;
public const float ANGLE_ADJUST = 0.01f;
public const int BACKDOWN_DEFAULT = 100;
 
// turret constants
public const float TURRET_MAX_ROT_SPEED = 10f;
public const float TURRET_MAXIMUM_INPUT = 10f;
 
// raycasting constants
public const float RAYCAST_HI_ANGLE = -10f;
public const float RAYCAST_LO_ANGLE = -11f;
public float RAYCAST_DISTANCE = STOP_DISTANCE;
public const float RAYCAST_TOLERANCE = 0.05f;
public const float RAYCAST_MINIMUM_DISTANCE = 0.001f;
public const float RAYCAST_CONSECUTIVE_FAILS = 3;

// vitals/reference
public IMyGridTerminalSystem LocalGrid;
public IMyCubeBlock ReferenceBlock;
public IMyShipController Controller;
public List<SuspensionWrapper> Wheels = new List<SuspensionWrapper>();
public Vector3D CenterOfMass = new Vector3D();
public MatrixD CenterOfMassMatrix = new MatrixD();
public List<IMyLightingBlock> Lights = new List<IMyLightingBlock>(); 

// face stuff
public enum Mouths {line, w, A, tilde, o }
public enum Eyes {o, brackets, x }
public Mouths mouth;
public Eyes eyes;
public int blushcolor; 
public char[] face;
   
// movement variables
public Vector3D CurrentTargetLocation = new Vector3D(); 
public float CurrentRPM = 0f; 
public int backdown = 0;
public Vector3D CurrentCourseCorrectionOffset;
public bool courseCorrecting = false;
public Route currentRoute;
public bool followingRoute = false;
public bool patrolLoop = true;
public bool forwardInRoute = true;
public int waitingAtStopCountdown = 0;
public int suspensionReworkCountdown = 0;
public float TargetVelocity = MAX_VELOCITY;
   
// behavioral flags
public bool detectEntities = false;
public bool detectPlayers = false;
public bool detectMonsters = false;
public bool detectSmallGrids = false;
public bool detectLargeGrids = false;
public bool enableFollowBehavior = false;
public bool followLargerVehicles = false;
public bool stopNearBases = false;
public bool enableToolSafety = false;
public bool chaseSingleTarget = false;
public bool allowUserControl = false;
public bool allowAutonomousBehavior = false;
public bool followRoutes = false;
public bool allowReversingTurn = false;
   
// raycasting variables
public int raycastConsistencyTracker;
  
// persistence data
public PersistentData persistentData;
public bool hasLoadedData = false;
 
// route recording data
public bool isRecordingRoute = false;
public int routeRecordCountdown = 0;
 
// store tools that need to be re-enabled
public List<IMyFunctionalBlock> toolsToReenable = new List<IMyFunctionalBlock>();
public List<Turret> Turrets = new List<Turret>();
  
public enum Modes
{
    // basic modes
    Vehicle,        // only reacts to user input; autonomous behavior disabled.
    Follow,         // follows friendlies.
    Ram,            // follows hostiles.
    Convoy,         // follows vehicles.
    Patrol,         // patrols along a preset route. use "patrolLoop" to determine if it loops or reverses at the end.      NOT YET IMPLEMENTED
   
    // hybrid/advanced modes
    Escort,         // hybrid of vehicle/patrol: uses vehicle when in range of a station, uses follow outside of them.
    Hunt,           // hybrid of ram/patrol: patrols along a set route, but chases after hostiles. returns to patrol route afterwards.       NOT YET IMPLEMENTED
}
public Modes currentMode = Modes.Vehicle;
public Modes lastMode = Modes.Vehicle;

public Program()
{
    // begin update frequency
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    
    // initialize text log
    TextLog = new List<string>(); 
     
    // set up her pretty face owo
    blushcolor = 0; 
    face = new char[3]; 
    face[0] = 'o'; 
    face[1] = '_'; 
    face[2] = 'o'; 
     
    // set up backdown
    backdown = 0; 
     
    // track raycast consistency
    raycastConsistencyTracker = 0;
     
    // is course correction occurring?
    courseCorrecting = false;
    CurrentCourseCorrectionOffset = new Vector3D(0, 0, 0);
     
    // set up initial target to current position
    CurrentTargetLocation = Me.Position;
     
    // create persistent route
    currentRoute = new Route();
     
    // set up vitals
    LocalGrid = GridTerminalSystem;
    ReferenceBlock = Me;
    GatherTurrets(GridTerminalSystem, Me);
     
    // set up mode
    SetMode(currentMode);
     
    // set up persistent data
    persistentData = new PersistentData();
    
    // update parts
    UpdateReferences();
    UpdateLights();
    UpdateWheels();
    UpdatePersistentData();
    UpdateRoute();
    
    // update route
    currentRoute.MatchToClosestWaypoint(ReferenceBlock.GetPosition());
    
    // clear warnings
    Warn("");
}

public void Main(string argument)
{
    if (argument == "")
    {
        Warn("");
        
        // update parts
        UpdateReferences();
        UpdateLights();
        UpdateWheels();
        UpdatePersistentData();
        UpdateRoute();
        
        // prepare to blush!
        bool blush = false;
         
        // set up stop distance to properly reflect current grid size
        var boundingBox = Me.CubeGrid.WorldAABB;
        STOP_DISTANCE = (float)(boundingBox.Size.Length() / 2 * 1.1);
        if (Controller != null)
        {
            var velocity = Controller.GetShipSpeed();
            if (velocity > 6)
            {
                STOP_DISTANCE *= (float)(velocity / 6);
            }
        }
        STOP_DISTANCE += BUFFER_DISTANCE;
         
        foreach (var turret in Turrets)
        {
            turret.Move(new Vector2(0, 0));
        }
         
        if (detectEntities && enableToolSafety)
        {
            foreach (var tool in toolsToReenable)
            {
                tool.Enabled = true;
            }
            toolsToReenable.Clear();
             
            // find all tools
            var tools = new List<IMyShipToolBase>();
            GridTerminalSystem.GetBlocksOfType<IMyShipToolBase>(tools, (IMyShipToolBase x) => x.Enabled && !(x.CustomData.Contains("nwt"))); 
             
            if (tools.Count() > 0)
            {
                var detected = new List<MyDetectedEntityInfo>();
                 
                // get the sensors
                var sensors = new List<IMySensorBlock>(); 
                GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, (IMySensorBlock x) => x.CubeGrid == Me.CubeGrid && !(x.CustomData.Contains("nwt"))); 
 
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
                            if (d.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)
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
                    }
                     
                    // check whether to disable tools
                    if (detected.Count() > 0)
                    {
                        foreach (var tool in tools)
                        {
                            foreach (var target in detected)
                            {
                                if (Vector3D.Distance(tool.GetPosition(), target.Position) <= 5)
                                {
                                    tool.Enabled = false;
                                    toolsToReenable.Add(tool);
                                }
                                 
                                if (!(tool.Enabled))
                                    break;
                            }
                        }
                    }
                }
            }
        }
         
        if (Controller != null && Controller.IsUnderControl && allowUserControl)
        {
            foreach (var turret in Turrets)
            {
                turret.Move(Controller.RotationIndicator);
            }
            
            foreach (var wheel in Wheels)
            {
                wheel.PropulsionOverride = 0f;
                wheel.SteerOverride = 0f;
            }
   
            face[0] = '>'; 
            face[1] = '3'; 
            face[2] = 'o';
   
            foreach (var light in Lights)
            {
                light.SetValue("Color", new Color(255, 255, 255));
            }
        }
        else if (allowAutonomousBehavior)
        {
            if (backdown >= 0)
            {
                backdown--;
   
                face[0] = '@'; 
                face[1] = 'A'; 
                face[2] = '@'; 
   
                if (backdown == 0)
                {
                    Stop();
                }
            }
             
            bool nearBase = false;
            bool inBase = false;
            var detected = new List<MyDetectedEntityInfo>();
            if (detectEntities)
            {
                // get the face sensors
                var sensors = new List<IMySensorBlock>(); 
                GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, (IMySensorBlock x) => x.CubeGrid == ReferenceBlock.CubeGrid && !(x.CustomData.Contains("nwt"))); 
   
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
                            bool detectThisEntity = false;
                            if (detectPlayers && d.Type == MyDetectedEntityType.CharacterHuman && d.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)
                                detectThisEntity = true;
                            else if (detectMonsters && (d.Type == MyDetectedEntityType.CharacterHuman || d.Type == MyDetectedEntityType.CharacterOther) && d.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                                detectThisEntity = true;
                            else if (detectSmallGrids && d.Type == MyDetectedEntityType.SmallGrid && (d.Relationship == MyRelationsBetweenPlayerAndBlock.Owner || d.Relationship == MyRelationsBetweenPlayerAndBlock.FactionShare))
                                detectThisEntity = true;
                            else if ((detectLargeGrids || stopNearBases) && d.Type == MyDetectedEntityType.LargeGrid)
                                detectThisEntity = true;
                             
                            if (detectThisEntity)
                            {
                                foreach (var t in detected)
                                {
                                    if (d.EntityId == t.EntityId)
                                        inList = true;
                                     
                                    if (inList)
                                        break;
                                }
 
                                if (!inList)
                                {
                                    detected.Add(d);
                                }
                            }
                        }
                    }
                     
                    // separate detected entities into bases and non-bases
                    var detectedBases = new List<MyDetectedEntityInfo>();
                    if (stopNearBases)
                    {
                        int detectedEntityIndex = 0;
                        while (detectedEntityIndex < detected.Count())
                        {
                            if (detected[detectedEntityIndex].Type == MyDetectedEntityType.LargeGrid)
                            {
                                detectedBases.Add(detected[detectedEntityIndex]);
                                detected.RemoveAt(detectedEntityIndex);
                            }
                            else
                                detectedEntityIndex++;
                        }
                     
                        // now invalidate any entities that are inside a base's bounding box
                        detectedEntityIndex = 0;
                        while (detectedEntityIndex < detected.Count())
                        {
                            bool inABase = false;
                            foreach (var detectedBase in detectedBases)
                            {
                                if (detected[detectedEntityIndex].BoundingBox.Intersects(detectedBase.BoundingBox))
                                {
                                    detected.RemoveAt(detectedEntityIndex);
                                    inABase = true;
                                    break;
                                }
                            }
                             
                            if (!inABase)
                                detectedEntityIndex++;
                        }
                    }
                     
                    // stop near bases if necessary
                    if (detectedBases.Count() > 0)
                    {
                        // get projected bounding sphere
                        var nearBaseBoundingBox = boundingBox.GetInflated(0.75) + ReferenceBlock.WorldMatrix.GetOrientation().Forward * 2;
                        var inBaseBoundingBox = new BoundingSphereD(boundingBox.Center, nearBaseBoundingBox.Size.Length() / 5);
                         
                        // detect if in a base
                        foreach (var target in detectedBases)
                        {
                            if (!inBase && inBaseBoundingBox.Intersects(target.BoundingBox))
                            {
                                inBase = true;
                                Warn("In base!");
                            }
                             
                            if (!nearBase && nearBaseBoundingBox.Intersects(target.BoundingBox))
                            {
                                nearBase = true;
                                Warn("Near base!");
                            }
                        }
                    }
                     
                    // check whether to stop or if no entities were detected
                    if (detected.Count() > 0)
                    {
                        // stop following the current route
                        followingRoute = false;
                        
                        int targetsUsed = 0;
                        double minDist = -1;
                        double x = 0;
                        double y = 0;
                        double z = 0;
                        
                        bool needToStop = false;
                        
                        TargetVelocity = MAX_VELOCITY;
                        var minimumBoundingBoxSize = ReferenceBlock.CubeGrid.WorldAABB.Volume / 2;
                        foreach (var target in detected)
                        {
                            bool doDetectTarget = true;
                            
                            if (target.Velocity.Length() < 0.1 && target.Type == MyDetectedEntityType.CharacterOther)
                            {
                                doDetectTarget = false;
                            }
                            else if (target.Type == MyDetectedEntityType.SmallGrid && target.BoundingBox.Volume < minimumBoundingBoxSize)
                            {
                                doDetectTarget = false;
                            }
                            
                            if (doDetectTarget == true)
                            {
                                var tdist = Vector3D.Distance(target.Position, boundingBox.Center); 
                                if (minDist == -1 || tdist < minDist)
                                {
                                    minDist = tdist;
                                    if (minDist < STOP_DISTANCE * 2)
                                    {
                                        TargetVelocity = Math.Max(MIN_VELOCITY, Math.Min(MAX_VELOCITY, (float)(target.Velocity.Length() * 1.5)));
                                    }
                                    if (chaseSingleTarget)
                                    {
                                        x = target.Position.X;
                                        y = target.Position.Y;
                                        z = target.Position.Z;
                                        targetsUsed = 1;
                                    }
                                }
                                
                                if (!chaseSingleTarget)
                                {
                                    x += target.Position.X;
                                    y += target.Position.Y;
                                    z += target.Position.Z;
                                    targetsUsed++;
                                }
                            }
                        }
                        
                        if (needToStop == true)
                        {
                            CurrentTargetLocation = boundingBox.Center;
                        }
                        else if (targetsUsed > 0)
                        {
                            if (!chaseSingleTarget)
                            {
                                x /= targetsUsed;
                                y /= targetsUsed;
                                z /= targetsUsed;
                            }
                            
                            // if only one target, match its velocity
                            CurrentTargetLocation = new Vector3D(x, y, z);
                        }
                    }
                    else if (followRoutes)
                    {
                        // return to following a route
                        if (!followingRoute)
                        {
                            currentRoute.MatchToClosestWaypoint(ReferenceBlock.GetPosition());
                        }
                        followingRoute = true;
                    }
                }
            }
   
            if (backdown <= 0)
            {
                // check cameras for forcing backdown
                if (!RaycastCheckTerrain(GridTerminalSystem, ReferenceBlock))
                {
                    backdown = BACKDOWN_DEFAULT;
                }
                
                if (Controller != null)
                {
                    var gv = Controller.GetTotalGravity(); 
                    var fv = ReferenceBlock.WorldMatrix.Forward; 
                    var angle = Math.Acos(fv.Dot(gv) / (fv.Length() * gv.Length())); 
                    if (angle > Math.PI * 3/4 || angle < Math.PI * 1/4)
                        backdown = BACKDOWN_DEFAULT;
                }
                
                // follow route if set to do that
                if (followingRoute)
                {
                    if (waitingAtStopCountdown == 0)
                    {
                        var dist = Vector3D.Distance(ReferenceBlock.GetPosition(), CurrentTargetLocation);
                        if (dist <= BUFFER_DISTANCE && followingRoute)
                        {
                            // set countdown timer
                            waitingAtStopCountdown = currentRoute.GetCurrentWaypoint().WaitTime;
                            bool keepGoing;
                            if (forwardInRoute)
                            {
                                keepGoing = currentRoute.GotoNextWaypoint();
                            }
                            else
                            {
                                keepGoing = currentRoute.GotoPreviousWaypoint();
                            }
                            
                            // end of the route?
                            if (!keepGoing)
                            {
                                // if in a patrol mode, keep patrolling
                                if (followRoutes)
                                {
                                    if (patrolLoop)
                                    {
                                        if (forwardInRoute)
                                        {
                                            currentRoute.GotoFirstWaypoint();
                                        }
                                        else
                                        {
                                            currentRoute.GotoLastWaypoint();
                                        }
                                    }
                                    else
                                    {
                                        forwardInRoute = !forwardInRoute;
                                    }
                                    Log("Following route. Current waypoint: " + currentRoute.WaypointIndex().ToString());
                                }
                                // otherwise, finish and turn off route following
                                else
                                {
                                    followingRoute = false;
                                    CurrentTargetLocation = ReferenceBlock.GetPosition();
                                    Log("Reached end of route.");
                                }
                            }
                            else
                            {
                                CurrentTargetLocation = currentRoute.GetCurrentWaypoint().Position;
                            }
                        }
                    }
                }
                
                bool stop = false;
                double minDist = -1;
                
                var distanceToCompareWith = STOP_DISTANCE;
                if (followingRoute == true)
                {
                    distanceToCompareWith = BUFFER_DISTANCE / 2;
                }
                var centerPoint = boundingBox.Center;
                var distToCTL = Vector3D.Distance(CurrentTargetLocation, centerPoint);
                if (minDist == -1 || distToCTL < minDist)
                    minDist = distToCTL;
                if (minDist <= distanceToCompareWith)
                {
                    stop = true; 
                    face[0] = '>'; 
                    face[2] = '<';
                    blush = true;
                }
                else if (minDist <= distanceToCompareWith * 4)
                {
                    face[0] = 'o'; 
                    face[1] = 'w'; 
                    face[2] = 'o'; 
                }
                else if (minDist <= 100)
                {
                    face[1] = 'A';
                }
                else
                {
                    if (followingRoute)
                    {
                        //currentRoute.MatchToClosestWaypoint(ReferenceBlock.GetPosition());
                        CurrentTargetLocation = currentRoute.GetCurrentWaypoint().Position;
                        
                        face[0] = '\'';
                        face[1] = '_';
                        face[2] = '\'';
                    }
                    else
                    {
                        stop = true;
                        face[0] = '.';
                        face[2] = '.';
                        CurrentTargetLocation = ReferenceBlock.GetPosition();
                    }
                }
                
                // stop if needed
                if (stop)// || (inBase && !nearBase))
                {
                    Stop();
                    if (detected.Count() == 0)
                    {
                        face[0] = '.'; 
                        face[1] = 'z'; 
                        face[2] = 'Z'; 
                    }
                }
                else
                {
                    MoveAndSteer();
                }
            }
   
            foreach (var light in Lights)
            {
                if (blush)
                {
                    if (blushcolor < 255)
                        blushcolor += 2; 
                    light.SetValue("Color", new Color(blushcolor, blushcolor / 2, blushcolor / 2, blushcolor)); 
                }
                else
                {
                    if (blushcolor > 3)
                        blushcolor -= 4; 
                    else
                        blushcolor = 0; 
                    light.SetValue("Color", new Color(blushcolor, blushcolor / 2, blushcolor / 2, blushcolor)); 
                }
            }
        }
        else
        {
            Stop();
   
            blush = false; 
   
            face[0] = ';'; 
            face[1] = '_'; 
            face[2] = ';'; 
        }
         
        if (Controller != null)
        {
            if (Controller.GetShipVelocities().LinearVelocity.Length() > MAX_VELOCITY)
            {
                CurrentRPM -= RPM_RAMPUP;
            }
            else if (Controller.GetShipVelocities().LinearVelocity.Length() < -MAX_VELOCITY)
            {
                CurrentRPM += RPM_RAMPUP;
            }
        }
         
        // retrieve LCDs
        var LCDs = new List<IMyTextPanel>(); 
        GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(LCDs, (IMyTextPanel x) => x.CubeGrid == ReferenceBlock.CubeGrid && !(x.CustomData.Contains("nwt")) && !(x.CustomData.Contains("debug"))); 
        string facestring = " " + face[0] + face[1] + face[2]; 
   
        foreach (var LCD in LCDs)
        {
            LCD.WriteText(facestring); 
        }
    }
    else if (argument.Contains("-"))
    {
        var args = argument.ToLower().Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var arg in args)
        {
            var argSplit = arg.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (argSplit.Count() > 0)
            {
                if (argSplit[0] == "park")
                {
                    if (argSplit.Count() == 1)
                    {
                        TextLog.Clear();
                        Log("-park on/off/toggle");
                        Log("on: park mode on.");
                        Log("off: park mode off.");
                        Log("toggle: toggle park mode.");
                    }
                    else
                    {
                        if (argSplit[1] == "toggle")
                        {
                            Modes tMode = currentMode;
                            SetMode(lastMode);
                            lastMode = tMode;
                        }
                        else if (argSplit[1] == "on")
                        {
                            if (currentMode != Modes.Vehicle)
                            {
                                Modes tMode = currentMode;
                                SetMode(lastMode);
                                lastMode = tMode;
                            }
                        }
                        else if (argSplit[1] == "off")
                        {
                            if (currentMode == Modes.Vehicle)
                            {
                                Modes tMode = currentMode;
                                SetMode(lastMode);
                                lastMode = tMode;
                            }
                        }
                        
                        if (currentMode == Modes.Vehicle)
                        {
                            Log("Park set ON.");
                        }
                        else
                        {
                            Log("Park set OFF.");
                        }
                    }
                }
                else if (argSplit[0] == "route")
                {
                    if (argSplit.Count() == 1)
                    {
                        TextLog.Clear();
                        Log("-route record/start/finish/clear");
                        Log("record: add a waypoint.");
                        Log("start: start adding waypoints.");
                        Log("finish: stop adding waypoints.");
                        Log("delete: delete the last waypoint.");
                        Log("clear: clear all waypoints.");
                    }
                    else
                    {
                        if (argSplit[1] == "record")
                        {
                            if (followRoutes)
                            {
                                Log("Can't record a waypoint while on a route!");
                            }
                            else
                            {
                                currentRoute.AddWaypoint(ReferenceBlock.GetPosition());
                                Log("Recorded waypoint.");
                            }
                        }
                        else if (argSplit[1] == "start")
                        {
                            if (followRoutes)
                            {
                                Log("Can't record a route while on a route!");
                            }
                            else
                            {
                                isRecordingRoute = true;
                                routeRecordCountdown = 0;
                                Log("Started recording waypoints.");
                            }
                        }
                        else if (argSplit[1] == "finish")
                        {
                            isRecordingRoute = false;
                            Log("Finished recording waypoints.");
                        }
                        else if (argSplit[1] == "delete")
                        {
                            currentRoute.ClearLastWaypoint();
                            Log("Removed the last waypoint.");
                        }
                        else if (argSplit[1] == "clear")
                        {
                            currentRoute.ClearWaypoints();
                            Log("Removed all waypoints.");
                        }
                    }
                }
                else if (argSplit[0] == "mode")
                {
                    if (argSplit.Count() == 1)
                    {
                        TextLog.Clear();
                        Log("-mode MODE");
                        Log("MODE: the mode to set as active. valid are:");
                        Log("Vehicle, Follow, Ram, Patrol, Escort, Hunt");
                    }
                    else
                    {
                        bool parked = currentMode == Modes.Vehicle && lastMode != Modes.Vehicle;
                        switch (argSplit[1])
                        {
                            case "vehicle": SetMode(Modes.Vehicle); lastMode = Modes.Vehicle; break;
                            case "follow": SetMode(Modes.Follow); lastMode = Modes.Vehicle; break;
                            case "ram": SetMode(Modes.Ram); lastMode = Modes.Vehicle; break;
                            case "convoy": SetMode(Modes.Convoy); lastMode = Modes.Vehicle; break;
                            case "patrol": SetMode(Modes.Patrol); lastMode = Modes.Vehicle; break;
                            case "escort": SetMode(Modes.Escort); lastMode = Modes.Vehicle; break;
                            case "hunt": SetMode(Modes.Hunt); lastMode = Modes.Vehicle; break;
                        }
                         
                        if (parked)
                            Main("-park on");
                    }
                }
                else if (argSplit[0] == "debug")
                {
                    if (argSplit.Count() > 1)
                    {
                        if (argSplit[1] == "target")
                        {
                            Log(Vector3D.Distance(Me.GetPosition(), CurrentTargetLocation));
                        }
                    }
                }
            }
        }
    }
    
    foreach (var line in TextLog)
    {
        Echo(line); 
    }
}

public void UpdateReferences()
{
    // set up reference used for various code functions
    IMyCubeBlock ReferenceBlock = Me;
    Controller = null;
    
    // find if there are any ship controllers (cockpits, remote control blocks, etc)
    var controllers = new List<IMyShipController>();
    LocalGrid.GetBlocksOfType<IMyShipController>(controllers, (IMyShipController x) => x.CubeGrid == ReferenceBlock.CubeGrid && !(x.CustomData.Contains("nwt")));
    if (controllers.Count() > 0)
    {
        Controller = controllers[0];
        foreach (var c in controllers)
        {
            if (c.CustomData.Contains("wt-facing"))
            {
                Controller = c;
                break;
            }
        }
    }
    
    if (Controller == null)
    {
        CenterOfMass = ReferenceBlock.GetPosition();
    }
    else
    {
        CenterOfMass = Controller.CenterOfMass;
    }
    
    CenterOfMassMatrix = MatrixD.CreateWorld(CenterOfMass, ReferenceBlock.WorldMatrix.Forward, ReferenceBlock.WorldMatrix.Up);
}

public void UpdatePersistentData()
{
    if (hasLoadedData)
    {
        SavePersistentData(Me);
    }
    else
    {
        hasLoadedData = LoadPersistentData(Me);
    }
}

public void UpdateRoute()
{
    // count down stop waiting ticker if needed
    if (waitingAtStopCountdown > 0)
        waitingAtStopCountdown--;
     
    // record route stuff
    if (isRecordingRoute)
    {
        if (followRoutes)
            isRecordingRoute = false;
        else
        {
            if (routeRecordCountdown > 0)
                routeRecordCountdown--;
            else
            {
                currentRoute.AddWaypoint(ReferenceBlock.GetPosition());
                routeRecordCountdown = 50;
            }
        }
    }
}

public void UpdateLights()
{
    GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(Lights, (IMyLightingBlock x) => x.CubeGrid == ReferenceBlock.CubeGrid && !(x.CustomData.Contains("nwt"))); 
}

public void UpdateWheels()
{
    var wheels = new List<IMyMotorSuspension>();
    GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(wheels, (IMyMotorSuspension x) => x.CubeGrid == ReferenceBlock.CubeGrid && !(x.CustomData.Contains("nwt"))); 
    
    Wheels.Clear();
    foreach (var wheel in wheels)
    {
        Base6Directions.Direction blockOrientation = ReferenceBlock.Orientation.TransformDirectionInverse(wheel.Orientation.Up);
        if (blockOrientation == Base6Directions.Direction.Left || blockOrientation == Base6Directions.Direction.Right)
        {
            var newWheel = new SuspensionWrapper(wheel, blockOrientation);
            double steeringAngle = Math.Abs(GetAngleFromCoM(wheel.GetPosition()));
            if (steeringAngle > Math.PI / 2)
            {
                steeringAngle -= Math.PI / 2;
            }
            if (IsFrontSide(wheel.GetPosition()) == false)
            {
                steeringAngle = Math.PI / 2 - steeringAngle;
            }
            newWheel.MaxSteerAngle = steeringAngle;
            Wheels.Add(newWheel);
        }
    }
}

public void MoveAndSteer()
{
    if (Controller == null)
    {
        Stop();
    }
    else
    {
        // move
		Controller.HandBrake = false;
        
        if (backdown > 0)
        {
            foreach (var wheel in Wheels)
            {
                CurrentTargetLocation = CenterOfMass;
                wheel.PropulsionOverride = -MIN_ACCELERATION * -wheel.PropulsionSign;
            }
        }
        else
        {
            double directionalVelocity = Vector3D.Dot(Controller.GetShipVelocities().LinearVelocity, CenterOfMassMatrix.Forward);
            float propulsionOverride = 0f;
            if (directionalVelocity < TargetVelocity)
            {
                propulsionOverride = MAX_ACCELERATION;
            }
            else
            {
                propulsionOverride = -MAX_ACCELERATION;
            }
            
            // steer
            var angle = GetAngleFromCoM(CurrentTargetLocation);
            
            // this bit lets her execute three-point turns
            if (allowReversingTurn == true && IsFrontSide(CurrentTargetLocation) == false)
            {
                propulsionOverride = -propulsionOverride;
                angle = -angle;
            }
            
            // apply settings to each wheel
            foreach (var wheel in Wheels)
            {
                double specificAngle = 0;
                if (IsFrontSide(wheel.Obj.GetPosition()) == true)
                {
                    specificAngle = -angle;
                }
                else
                {
                    specificAngle = angle;
                }
                
                var totalVelocity = Controller.GetShipVelocities().LinearVelocity.Length() + Controller.GetShipVelocities().AngularVelocity.Length();

                if (totalVelocity > DANGEROUS_VELOCITY)
                {
                    float maxAngle = (float)(wheel.MaxSteerAngle * (DANGEROUS_VELOCITY / totalVelocity));
                    
                    if (specificAngle > maxAngle)
                    {
                        specificAngle = maxAngle;
                    }
                    else if (specificAngle < -maxAngle)
                    {
                        specificAngle = -maxAngle;
                    }
                    
                    wheel.SteerOverride = (float)specificAngle;
                    
                    /*if (Math.Abs(wheel.SteerOverride - specificAngle) < ANGLE_ADJUST)
                    {
                        wheel.SteerOverride = (float)specificAngle;
                    }
                    else
                    {
                        if (wheel.SteerOverride > specificAngle)
                        {
                            wheel.SteerOverride -= ANGLE_ADJUST;
                        }
                        else if (wheel.SteerOverride < specificAngle)
                        {
                            wheel.SteerOverride += ANGLE_ADJUST;
                        }
                    }*/
                }
                else
                {
                    wheel.SteerOverride = (float)specificAngle;
                }
                wheel.PropulsionOverride = propulsionOverride * -wheel.PropulsionSign;
            }
        }
    }
}

public void Stop()
{
    if (Controller != null)
    {
        Controller.HandBrake = true;
    }
    
    foreach (var wheel in Wheels)
    {
        wheel.PropulsionOverride = 0f;
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
   
            // if a higher (stable) item was detected and a lower one was not, stop!
            if (wasDetectedHi && !wasDetectedLo)
            {
                //Log("Roofwall");
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
                        LCD.WriteText(distHi.ToString() + "\n" + distLo.ToString() + "\n" + (Math.Abs(distHi - distLo)).ToString() + "\n" + (detectedHi.EntityId == detectedLo.EntityId).ToString() + "\n" +
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

public void GatherTurrets(IMyGridTerminalSystem grid, IMyCubeBlock reference)
{
    // clear turrets
    Turrets.Clear();
     
    var rotors = new List<IMyMotorStator>();
    grid.GetBlocksOfType<IMyMotorStator>((rotors), (IMyMotorStator x) => !(x.CustomData.Contains("nwt")) && x.CustomData.Contains("wt-turret"));
     
    // match up the rotors
    int rotorIndex = 0;
    while (rotorIndex < rotors.Count())
    {
        var thisRotor = rotors[rotorIndex];
        if (thisRotor.CubeGrid == reference.CubeGrid)
        {
            var match = System.Text.RegularExpressions.Regex.Match(thisRotor.CustomData, "wt-turret\\s+(\\w+)");
            if (match.Success)
            {
                string turretName = match.Groups[1].Value;
                int matchingRotorIndex = 0;
                while (matchingRotorIndex < rotors.Count())
                {
                    if (matchingRotorIndex == rotorIndex)
                    {
                        matchingRotorIndex++;
                    }
                    else
                    {
                        var otherRotor = rotors[matchingRotorIndex];
                        var otherMatch = System.Text.RegularExpressions.Regex.Match(otherRotor.CustomData, "wt-turret\\s+(\\w+)");
                        if (otherMatch.Success)
                        {
                            if (otherMatch.Groups[1].Value == turretName)
                            {
                                break;
                            }
                            else
                            {
                                matchingRotorIndex++;
                            }
                        }
                        else
                        {
                            rotors.RemoveAt(matchingRotorIndex);
                        }
                    }
                }
                 
                if (matchingRotorIndex != rotors.Count())
                {
                    if (thisRotor.Orientation.Up == reference.Orientation.Up ||
                        thisRotor.Orientation.Up == Base6Directions.GetFlippedDirection(reference.Orientation.Up) ||
                        Base6Directions.GetFlippedDirection(thisRotor.Orientation.Up) == reference.Orientation.Up)
                    {
                        Turrets.Add(new Turret(turretName, rotors[matchingRotorIndex], thisRotor, TURRET_MAX_ROT_SPEED));
                    }
                    else
                    {
                        Turrets.Add(new Turret(turretName, thisRotor, rotors[matchingRotorIndex], TURRET_MAX_ROT_SPEED));
                    }
                     
                    rotors.RemoveAt(Math.Max(rotorIndex, matchingRotorIndex));
                    rotors.RemoveAt(Math.Min(rotorIndex, matchingRotorIndex));
                    if (matchingRotorIndex < rotorIndex)
                        rotorIndex--;
                }
                else
                {
                    rotorIndex++;
                }
            }
            else
            {
                rotors.RemoveAt(rotorIndex);
            }
        }
        else
        {
            rotorIndex++;
        }
    }
}

public void SetMode(Modes mode)
{
    detectEntities = false;
    detectPlayers = false;
    detectMonsters = false;
    detectSmallGrids = false;
    detectLargeGrids = false;
    enableFollowBehavior = false;
    stopNearBases = false;
    allowUserControl = false;
    allowAutonomousBehavior = false;
    followRoutes = false;
    enableToolSafety = false;
    chaseSingleTarget = false;
    followingRoute = false;
    allowReversingTurn = false;
    
    currentMode = mode;
    switch (mode)
    {
        case Modes.Vehicle:
            stopNearBases = true;
            allowUserControl = true;
            Log("Mode changed to Vehicle.");
            break;
        case Modes.Follow:
            detectEntities = true;
            detectPlayers = true;
            enableFollowBehavior = true;
            allowUserControl = true;
            allowAutonomousBehavior = true;
            allowReversingTurn = true;
            chaseSingleTarget = true;
            Log("Mode changed to Follow.");
            break;
        case Modes.Ram:
            detectEntities = true;
            detectMonsters = true;
            detectLargeGrids = true;
            stopNearBases = true;
            enableFollowBehavior = true;
            allowUserControl = true;
            allowAutonomousBehavior = true;
            enableToolSafety = true;
            chaseSingleTarget = true;
            Log("Mode changed to Ram.");
            break;
        case Modes.Convoy:
            detectEntities = true;
            detectSmallGrids = true;
            enableFollowBehavior = true;
            allowUserControl = true;
            allowAutonomousBehavior = true;
            Log("Mode changed to Convoy.");
            break;
        case Modes.Escort:
            detectEntities = true;
            detectPlayers = true;
            detectLargeGrids = true;
            enableFollowBehavior = true;
            stopNearBases = true;
            allowUserControl = true;
            allowAutonomousBehavior = true;
            Log("Mode changed to Escort.");
            break;
        case Modes.Patrol:
            followingRoute = true;
            allowAutonomousBehavior = true;
            followRoutes = true;
            Log("Mode changed to Patrol.");
            
            currentRoute.MatchToClosestWaypoint(Me.GetPosition());
            CurrentTargetLocation = currentRoute.GetCurrentWaypoint().Position;
            break;
        case Modes.Hunt:
            followingRoute = true;
            detectEntities = true;
            detectMonsters = true;
            enableFollowBehavior = true;
            allowAutonomousBehavior = true;
            followRoutes = true;
            chaseSingleTarget = true;
            enableToolSafety = true;
            Log("Mode changed to Hunt.");
            
            currentRoute.MatchToClosestWaypoint(Me.GetPosition());
            CurrentTargetLocation = currentRoute.GetCurrentWaypoint().Position;
            break;
        default:
            Log("Invalid mode specified.");
            break;
    }
}
  
public bool LoadPersistentData(IMyTerminalBlock store)
{
    string error = PersistentDataLoader.LoadData(store.CustomData, out persistentData);
     
    if (error != "")
    {
        Warn("Error loading persistent data: " + error);
        return false;
    }
    else
    {
        SetMode(persistentData.Mode);
        lastMode = Modes.Vehicle;
        if (persistentData.Parked)
            Main("-park on");
        followingRoute = persistentData.FollowingRoute;
        currentRoute = persistentData.Route;
        if (followRoutes)
        {
            currentRoute.MatchToClosestWaypoint(Me.GetPosition());
            CurrentTargetLocation = currentRoute.GetCurrentWaypoint().Position;
            Log("Following route. Current waypoint: " + currentRoute.WaypointIndex().ToString());
        }
        return true;
    }
}
  
public void SavePersistentData(IMyTerminalBlock store)
{
    // verify that old data is not newer than the code
    if (persistentData.Version > CODE_VERSION)
    {
        Warn("WARNING: Data version is higher than code version. Data saving disabled.");
        return;
    }
      
    persistentData.Version = CODE_VERSION;
    persistentData.Parked = currentMode == Modes.Vehicle && lastMode != Modes.Vehicle;
    persistentData.Mode = persistentData.Parked ? lastMode : currentMode;
    persistentData.FollowingRoute = followingRoute;
    persistentData.Route = currentRoute;
      
    store.CustomData = PersistentDataLoader.SaveData(persistentData, store.CustomData);
}

public double GetAngleFromCoM(Vector3D point)
{
	var localPosition = Vector3D.Transform(point, MatrixD.Invert(CenterOfMassMatrix)); 
	return Math.Atan2(-localPosition.X, -localPosition.Z);
}

public bool IsFrontSide(Vector3D point)
{
    var angle = GetAngleFromCoM(point);
    
	bool front = angle > -Math.PI / 2 && angle < Math.PI / 2;
	
	return front;
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

public class Route
{
    public struct Waypoint
    {
        public Vector3D Position;
        public int WaitTime;
           
        public Waypoint(Vector3D position, int waitTime)
        {
            Position = position;
            WaitTime = waitTime;
        }
    }
       
    List<Waypoint> m_waypoints;
    double m_minDist;
    int m_currentWaypoint;
       
    public Route() : this(STOP_DISTANCE) { }
       
    public Route(double minDist)
    {
        m_waypoints = new List<Waypoint>();
        m_minDist = minDist;
        m_currentWaypoint = 0;
    }
   
    public void GotoFirstWaypoint()
    {
        m_currentWaypoint = 0;
    }
       
    public void GotoLastWaypoint()
    {
        m_currentWaypoint = m_waypoints.Count() - 1;
    }
       
    // sets the current waypoint marker to point to the waypoint that's nearest to a given position and returns that distance
    public double MatchToClosestWaypoint(Vector3D position)
    {
        if (m_waypoints.Count() > 0)
        {
            m_currentWaypoint = 0;
            double bestMatchDist = Vector3D.Distance(GetCurrentWaypoint().Position, position);
            for (int i = 1; i < m_waypoints.Count(); i++)
            {
                var dist = Vector3D.Distance(m_waypoints[i].Position, position);
                if (dist < bestMatchDist)
                {
                    m_currentWaypoint = i;
                    bestMatchDist = dist;
                }
            }
            return bestMatchDist;
        }
        else
            return -1;
    }
   
    // returns the current waypoint
    public Waypoint GetCurrentWaypoint()
    {
        if (m_waypoints.Count() == 0)
            return default(Waypoint);
        else
            return m_waypoints[m_currentWaypoint];
    }
   
    // advances the waypoint ticker, if possible. returns if successful (false if on the last waypoint).
    public bool GotoNextWaypoint()
    {
        //Log("Waypoint " + (m_currentWaypoint + 1) + "/" + m_waypoints.Count());
        m_currentWaypoint++;
        if (m_currentWaypoint >= m_waypoints.Count())
        {
            m_currentWaypoint = m_waypoints.Count() - 1;
            return false;
        }
        else
            return true;
    }
   
    // regresses to the previous waypoint. returns if successful (false if on the first waypoint).
    public bool GotoPreviousWaypoint()
    {
        m_currentWaypoint--;
        if (m_currentWaypoint < 0)
        {
            m_currentWaypoint = 0;
            return false;
        }
        else
            return true;
    }
   
    public void SetMinimumDistance(double minDist)
    {
        m_minDist = minDist;
    }
       
    public void AddWaypoint(Vector3D point, int waitTime = 0)
    {
        m_waypoints.Add(new Waypoint(point, waitTime));
    }
    
    public void ClearLastWaypoint()
    {
        if (m_waypoints.Count() > 0)
        {
            m_waypoints.Remove(m_waypoints.Last());
        }
    }
   
    public void ClearWaypoints()
    {
        m_waypoints.Clear();
    }
   
    // cleans the route by eliminating points that are within the minimum distance of each other
    // bool findShortcuts determines if all points between two points near each other will be cut.
    //      if it is true, the route will attempt to eliminate loops.
    //      if it is false, the route will only remove points that are too close to the directly previous point.
    public void CleanRoute(bool findShortcuts = true)
    {
        // iterate through each point
        int initialWaypointIndex = 0;
        while (initialWaypointIndex < m_waypoints.Count() - 1)
        {
            // track if a removal was performed
            bool removalPerformed = false;
   
            // differ behavior when finding shortcuts
            if (!findShortcuts)
            {
                // if not finding shortcuts, just check the next waypoint
                if (Vector3D.Distance(m_waypoints[initialWaypointIndex].Position, m_waypoints[initialWaypointIndex + 1].Position) <= m_minDist)
                {
                    // remove the waypoint and track that
                    m_waypoints.RemoveAt(initialWaypointIndex + 1);
                    removalPerformed = true;
                }
            }
            else
            {
                // work backwards to eliminate the largest detours first
                int finalWaypointIndex;
                for (finalWaypointIndex = m_waypoints.Count() - 1; finalWaypointIndex > initialWaypointIndex; finalWaypointIndex--)
                {
                    // check if this point is close enough to the initial one
                    if (Vector3D.Distance(m_waypoints[initialWaypointIndex].Position, m_waypoints[finalWaypointIndex].Position) <= m_minDist)
                    {
                        // remove all waypoints between these two, not including the initial one
                        for (int i = finalWaypointIndex; i > initialWaypointIndex; i--)
                        {
                            m_waypoints.RemoveAt(i);
                        }
                        removalPerformed = true;
                        break;
                    }
                }
            }
   
            // advance if no removal was performed here
            if (!removalPerformed)
                initialWaypointIndex++;
        }
    }
   
    public int Count()
    {
        return m_waypoints.Count();
    }
       
    public int WaypointIndex()
    {
        return m_currentWaypoint;
    }
       
    public List<string> SaveToLines()
    {
        var lines = new List<string>();
        foreach (var waypoint in m_waypoints)
        {
            lines.Add(waypoint.Position.X + "," +
                        waypoint.Position.Y + "," + 
                        waypoint.Position.Z + "," +
                        waypoint.WaitTime);
        }
        return lines;
    }
       
    public void LoadFromLines(List<string> lines)
    {
        // clear current waypoints
        ClearWaypoints();
           
        //  add new waypoints
        foreach (var line in lines)
        {
            // validate and add waypoint
            var data = line.Split(',');
            double x, y, z;
            int waitTime;
            if (data.Count() == 4 &&
                double.TryParse(data[0], out x) &&
                double.TryParse(data[1], out y) && 
                double.TryParse(data[2], out z) &&
                int.TryParse(data[3], out waitTime))
            {
                /*Log(data[0]);
                Log(x);
                Log(data[1]);
                Log(y);
                Log(data[2]);
                Log(z);
                Log(data[3]);
                Log(waitTime);*/
                AddWaypoint(new Vector3D(x, y, z), waitTime);
            }
        }
    }
}

public struct PersistentData
{
    public double Version;
    public Route Route;
    public Modes Mode;
    public bool Parked;
    public bool FollowingRoute;
}
  
public static class PersistentDataLoader
{
    static string dataBeginMarker = "PERSISTENT_DATA_BEGIN";
    static string dataEndMarker = "PERSISTENT_DATA_END";
    static string versionMarker = "version:";
    static string waypointMarker = "waypoint:";
    static string modeMarker = "mode:";
    static string parkedMarker = "parked:";
    static string followingRouteMarker = "following_route:";
      
    enum ErrorTypes
    {
        NoError,
        DataParseError,
        NoDataFoundError,
        DataDidNotBeginError,
        VersionNotFoundError,
        VersionParseError,
        VersionMismatchError,
        UnknownError
    }
       
    // creates the string conversion of a PersistentData struct and inserts it into custom data
    public static string SaveData(PersistentData pData, string text)
    {
        // first, get converted data
        var cData = SaveData(pData);
           
        // if no text, just save right here
        if (text == "")
        {
            return cData;
        }
           
        // if there is text, break out any existing PersistentData
        var lines = System.Text.RegularExpressions.Regex.Split(text, "\r\n|\r|\n");
        var newLines = new List<string>();
        var sb = new StringBuilder();
        if (lines.Count() < 500)
        {
            // look for beginning of data persistence
            bool hasStarted = false;
            foreach (var line in lines)
            {
                if (hasStarted)
                {
                    if (line == dataEndMarker)
                    {
                        hasStarted = false;
                    }
                }
                else if (line == dataBeginMarker)
                {
                    hasStarted = true;
                }
                else
                {
                    newLines.Add(line);
                }
            }
             
            for (int i = 0; i < newLines.Count(); i++)
            {
                if (i == newLines.Count() - 1)
                {
                    sb.Append(newLines[i]);
                }
                else
                {
                    sb.AppendLine(newLines[i]);
                }
            }
        }
        else
        {
            sb.Append(text);
        }
         
        sb.Append(cData);
         
        //text = System.Text.RegularExpressions.Regex.Replace(text, "\r?\n?" + dataBeginMarker + ".*" + dataEndMarker, "", System.Text.RegularExpressions.RegexOptions.Singleline);
        return sb.ToString();
    }
       
    // returns the string conversion of a PersistentData struct
    public static string SaveData(PersistentData pData)
    {
        // begin line strings
        var lines = new List<string>();
        lines.Add(dataBeginMarker);
           
        // save version info
        lines.Add(versionMarker + pData.Version.ToString());
           
        // save waypoints info
        var waypoints = pData.Route.SaveToLines();
        foreach (var waypoint in waypoints)
        {
            lines.Add(waypointMarker + waypoint);
        }
           
        // save behaviors
        lines.Add(modeMarker + pData.Mode.ToString());
        lines.Add(parkedMarker + pData.Parked.ToString());
        lines.Add(followingRouteMarker + pData.FollowingRoute.ToString());
           
        // add ending
        lines.Add(dataEndMarker);
           
        // send back string to append to custom data
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            sb.AppendLine(line);
        }
        return sb.ToString();
    }
       
    // loads data from a custom data string into an existing PersistentData struct
    public static string LoadData(string text, out PersistentData pData)
    {
        // prepare struct for use by guaranteeing all fields are filled out
        pData.Version = 0;
        pData.Route = new Route();
        pData.Mode = Modes.Vehicle;
        pData.Parked = false;
        pData.FollowingRoute = false;
         
        string error = "";
        var data = new List<string>();
          
        if (text == "")
        {
            var sb = new StringBuilder();
            sb.AppendLine(dataBeginMarker);
            sb.Append(versionMarker);
            sb.AppendLine(CODE_VERSION.ToString());
            sb.Append(dataEndMarker);
            text = sb.ToString();
        }
         
        // split into lines
        var lines = System.Text.RegularExpressions.Regex.Split(text, "\r\n|\r|\n");
         
        // don't mess it all up!!!
        if (lines.Count() > 500)
            return GetErrorMessage(ErrorTypes.DataParseError);
         
        // look for beginning of data persistence
        bool hasStarted = false;
        foreach (var line in lines)
        {
            if (hasStarted)
            {
                if (line == dataEndMarker)
                {
                    break;
                }
                else
                {
                    data.Add(line);
                }
            }
            else if (line == dataBeginMarker)
            {
                hasStarted = true;
            }
        }
         
        // detect loading errors
        /*if (!hasStarted)
        {
            return GetErrorMessage(ErrorTypes.DataDidNotBeginError);
        }
        else if (data.Count() == 0)
        {
            return GetErrorMessage(ErrorTypes.NoDataFoundError);
        }*/
        // instead of erroring out on a poor load, assume data just doesn't exist
        if (data.Count() == 0)
        {
            data.Add(CODE_VERSION.ToString());
        }
         
        // load version
        error = LoadVersion(data, out pData);
        if (error != "")
            return error;
         
        pData.Route = new Route();
        pData.Mode = Modes.Vehicle;
        pData.Parked = false;
        pData.FollowingRoute = false;
         
        // prepare data that must be finalized post-search
        var waypointLines = new List<string>();
         
        // load general data
        foreach (var line in data)
        {
            // load waypoints
            if (line.StartsWith(waypointMarker))
            {
                waypointLines.Add(line.Substring(waypointMarker.Count()));
            }
            // load mode
            else if (line.StartsWith(modeMarker))
            {
                Enum.TryParse(line.Substring(modeMarker.Count()), out pData.Mode);
            }
            // load parked
            else if (line.StartsWith(parkedMarker))
            {
                bool.TryParse(line.Substring(parkedMarker.Count()), out pData.Parked);
            }
            // load following route
            else if (line.StartsWith(followingRouteMarker))
            {
                bool.TryParse(line.Substring(followingRouteMarker.Count()), out pData.FollowingRoute);
            }
        }
         
        // finalize data where necessary
        pData.Route.LoadFromLines(waypointLines);
         
        // no errors occurred!
        return "";
    }
       
    // loads the version from the data
    // returns: a string containing an errors encountered (empty if none)
    static string LoadVersion(List<string> data, out PersistentData pData)
    {
        pData = new PersistentData();
          
        if (data.Count() == 0)
            return GetErrorMessage(ErrorTypes.NoDataFoundError);
          
        string versionLine = "";
        foreach (var line in data)
        {
            if (line.StartsWith(versionMarker))
            {
                versionLine = line;
                break;
            }
        }
          
        // check version specification exists
        if (versionLine == "")
            return GetErrorMessage(ErrorTypes.VersionNotFoundError);
           
        // attempt to parse version
        string versionString = versionLine.Substring(versionMarker.Count());
        double version;
        if (!(double.TryParse(versionString, out version)))
            return GetErrorMessage(ErrorTypes.VersionParseError, versionString);
           
        // verify code is same or newer version
        // NOTE: this doesn't affect loading, but newer data won't be saved over
        /*if (CODE_VERSION < version)
            return GetErrorMessage(ErrorTypes.VersionMismatchError, CODE_VERSION.ToString() + " < " + version.ToString());*/
           
        // no errors - set version
        pData.Version = version;
        return "";
    }
       
    static string GetErrorMessage(string type, string details = "")
    {
        string errorMessage = type;
        if (details != "")
        {
            errorMessage += " { " + details + " }";
        }
        return errorMessage;
    }
       
    static string GetErrorMessage(ErrorTypes type, string details = "")
    {
        switch (type)
        {
            case ErrorTypes.NoError: return GetErrorMessage("", details);
            case ErrorTypes.DataParseError: return GetErrorMessage("An error occurred while parsing the custom data.", details);
            case ErrorTypes.NoDataFoundError: return GetErrorMessage("No data was given.", details);
            case ErrorTypes.DataDidNotBeginError: return GetErrorMessage("Could not find the beginning of the data.", details);
            case ErrorTypes.VersionNotFoundError: return GetErrorMessage("Could not find the data version.", details);
            case ErrorTypes.VersionParseError: return GetErrorMessage("Could not parse the data version.", details);
            case ErrorTypes.VersionMismatchError: return GetErrorMessage("Data version is newer than code version.", details);
            default: return GetErrorMessage("Unknown error.", details);
        }
    }
}
 
public class Turret
{
    IMyMotorStator m_pitch, m_yaw;
    bool m_pitchInverted, m_yawInverted;
    float m_rotSpeed;
    string m_name;
     
    public Turret(string name, IMyMotorStator pitch, IMyMotorStator yaw, float rotSpeed)
    {
        m_name = name;
        m_pitch = pitch;
        m_yaw = yaw;
        m_pitchInverted = m_pitch.CustomData.Contains("invert");
        m_yawInverted = m_yaw.CustomData.Contains("invert");
        m_rotSpeed = rotSpeed;
    }
     
    public void Move(Vector2 angles)
    {
        float pitchSpeed = 0;
        float yawSpeed = 0;
         
        pitchSpeed = m_rotSpeed * (angles.X / TURRET_MAXIMUM_INPUT);
        if (m_pitchInverted)
            pitchSpeed = -pitchSpeed;
        m_pitch.SetValueFloat("Velocity", pitchSpeed);
         
            yawSpeed = m_rotSpeed * (angles.Y / TURRET_MAXIMUM_INPUT);
        if (m_yawInverted)
            yawSpeed = -yawSpeed;
        m_yaw.SetValueFloat("Velocity", yawSpeed);
    }
}

/*

This wrapper was made by Wanderer as part of Driver Assist System (DAS)
https://steamcommunity.com/sharedfiles/filedetails/?id=1089115113

*/
public class SuspensionWrapper
{
    public IMyMotorSuspension Obj {get;}
    public Base6Directions.Direction OrientationInVehicle {get;}
    public Vector3D WheelPositionAgainstCoM {get;set;}
    public Vector3D WheelPositionAgainstRef {get;set;}
    public double WheelPositionAgainstVelocity {get;set;}
    public double HeightOffsetMin {get;}
    public double HeightOffsetMax {get;}
    public double HeightOffsetRange {get;}
    public double WheelRadius {get;}
    public double PropulsionSign {get;}
    public bool IsSubgrid {get;}
    public double LeftMaxSteerAngle;
    public double RightMaxSteerAngle;
    public double TurnRadiusCurrent;
    public double TurnRadiusLeftMin;
    public double TurnRadiusRightMin;
    public double WeightDistributionRatio;
    public double BrakeFrictionDistributionRatio;
    public double SpeedLimit {get {return Obj.GetValueFloat("Speed Limit");} set {Obj.SetValueFloat("Speed Limit",(float)value);}}
    public double PropulsionOverride {get {return Obj.GetValueFloat("Propulsion override");} set {Obj.SetValueFloat("Propulsion override",(float)value);}}
    public double SteerOverride {get {return Obj.GetValueFloat("Steer override");} set {Obj.SetValueFloat("Steer override",(float)value);}}
    public double Power {get {return Obj.Power;} set {Obj.Power=(float)value;}}
    public double Friction {get {return Obj.Friction;} set {Obj.Friction=(float)value;}}
    public double Strength {get {return Obj.Strength;} set {Obj.Strength=(float)value;}}
    public double Height {get {return Obj.Height;} set {Obj.Height=(float)value;}}
    public double MaxSteerAngle {get {return Obj.MaxSteerAngle;} set {Obj.MaxSteerAngle=(float)value;}}
    
    public SuspensionWrapper(IMyMotorSuspension suspension,Base6Directions.Direction orientation,bool subgrid=false)
    {
        Obj=suspension;
        OrientationInVehicle=orientation;
        IsSubgrid=subgrid;
        if(orientation==Base6Directions.Direction.Left)
            PropulsionSign=-1;
        else if(orientation==Base6Directions.Direction.Right)
            PropulsionSign=1;
        HeightOffsetMin=suspension.GetMinimum<float>("Height");
        HeightOffsetMax=suspension.GetMaximum<float>("Height");
        HeightOffsetRange=HeightOffsetMax-HeightOffsetMin;
        
        if(suspension.CubeGrid.GridSizeEnum==MyCubeSize.Small)
        {
            if(suspension.BlockDefinition.SubtypeName.Contains("5x5")) WheelRadius=1.25;
            else if(suspension.BlockDefinition.SubtypeName.Contains("3x3")) WheelRadius=0.75;
            else if(suspension.BlockDefinition.SubtypeName.Contains("2x2")) WheelRadius=0.5;// modded
            else if(suspension.BlockDefinition.SubtypeName.Contains("1x1")) WheelRadius=0.25;
            else // some other modded wheels
                WheelRadius=suspension.IsAttached ? suspension.Top.WorldVolume.Radius*0.79/MathHelper.Sqrt2 : 0;
        }
        else
        {
            if(suspension.BlockDefinition.SubtypeName.Contains("5x5")) WheelRadius=6.25;
            else if(suspension.BlockDefinition.SubtypeName.Contains("3x3")) WheelRadius=3.75;
            else if(suspension.BlockDefinition.SubtypeName.Contains("2x2")) WheelRadius=2.5;// modded
            else if(suspension.BlockDefinition.SubtypeName.Contains("1x1")) WheelRadius=1.25;
            else // some other modded wheels
                WheelRadius=suspension.IsAttached ? suspension.Top.WorldVolume.Radius*0.79/MathHelper.Sqrt2 : 0;
        }
    }
    
    public Vector3 GetVelocityAtPoint(IMyShipController anchor)
    {
        Vector3 value=Vector3D.Zero;
        if(Obj.IsAttached)
        {
            Vector3 v=Obj.Top.GetPosition()-anchor.CenterOfMass;
            value=anchor.GetShipVelocities().LinearVelocity+anchor.GetShipVelocities().AngularVelocity.Cross(v);
        }
        return value;
    }
    
    public bool AddTopPart()
    {
        Obj.ApplyAction("Add Top Part");
        return Obj.IsAttached;
    }
    
    public void UpdateLocalPosition(IMyShipController anchor,Vector3D focalPointRef)
    {
        if(Obj.IsAttached)
        {
            Vector3D temp1,temp2;
            temp2=Obj.Top.GetPosition()-anchor.CenterOfMass;
            temp1.X=anchor.WorldMatrix.Right.Dot(temp2);
            temp1.Y=anchor.WorldMatrix.Up.Dot(temp2);
            temp1.Z=anchor.WorldMatrix.Backward.Dot(temp2);
            WheelPositionAgainstCoM=temp1;
            temp2=Obj.Top.GetPosition()-focalPointRef;
            temp1.X=anchor.WorldMatrix.Right.Dot(temp2);
            temp1.Y=anchor.WorldMatrix.Up.Dot(temp2);
            temp1.Z=anchor.WorldMatrix.Backward.Dot(temp2);
            WheelPositionAgainstRef=temp1;
        }
        else
            WheelPositionAgainstRef=WheelPositionAgainstCoM=Vector3D.Zero;
    }
    
    public void UpdatePositionVelocity(Vector3D velocity)
    {
        if(Obj.IsAttached)
            WheelPositionAgainstVelocity=velocity.Dot(WheelPositionAgainstCoM);
        else
            WheelPositionAgainstVelocity=0;
    }
}
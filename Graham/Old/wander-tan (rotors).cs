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
public const float MAX_VELOCITY = 30f;
public const float MAX_RPM = 60f;
public static float STOP_DISTANCE = 5f; 
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
public bool SUSPENSION_INVERT = false;      // in case you want inward-facing wheels for some reason
 
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
   
// face stuff
public enum Mouths {line, w, A, tilde, o }
public enum Eyes {o, brackets, x }
public Mouths mouth;
public Eyes eyes;
public int blushcolor; 
public char[] face;
   
// movement variables
public Vector3D CurrentTargetLocation; 
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
public bool doNotActYet = true;
   
// behavioral flags
public bool detectEntities = false;
public bool detectPlayers = false;              // this behavior unsupported when detectEntities=false
public bool detectMonsters = false;             // this behavior unsupported when detectEntities=false
public bool detectSmallGrids = false;           // this behavior unsupported when detectEntities=false
public bool detectLargeGrids = false;           // this behavior unsupported when detectEntities=false
public bool enableFollowBehavior = false;       // this behavior unsupported when detectEntities=false
public bool stopNearBases = false;              // this behavior unsupported when detectEntities=false
public bool enableToolSafety = false;           // this behavior unsupported when detectEntities=false
public bool chaseSingleEnemy = false;           // this behavior unsupported when detectEntities=false
public bool allowUserControl = false;
public bool allowAutonomousBehavior = false;
public bool automaticSuspensionAdjustment = false;
public bool followRoutes = false;
   
// raycasting variables
public int raycastConsistencyTracker;
  
// persistence data
public PersistentData persistentData;
public int persistentDataUpdateCountdown = 100;
public bool hasLoadedData = false;
 
// route recording data
public bool isRecordingRoute = false;
public int routeRecordCountdown = 0;
 
// store tools that need to be re-enabled
public List<IMyFunctionalBlock> toolsToReenable;
  
public List<IMyMotorStator> RotorsLeft, RotorsRight, RotorsSuspension;
public List<Turret> Turrets;
  
public enum Modes
{
    // basic modes
    Vehicle,        // only reacts to user input; autonomous behavior disabled.
    Follow,         // follows friendlies.
    Ram,            // follows hostiles.
    Patrol,         // patrols along a preset route. use "patrolLoop" to determine if it loops or reverses at the end.      NOT YET IMPLEMENTED
   
    // hybrid/advanced modes
    Escort,         // hybrid of vehicle/patrol: uses vehicle when in range of a station, uses follow outside of them.
    Hunt,           // hybrid of ram/patrol: patrols along a set route, but chases after hostiles. returns to patrol route afterwards.       NOT YET IMPLEMENTED
}
public Modes currentMode;
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
     
    // initial chassis scanning/setup
    RotorsLeft = new List<IMyMotorStator>();
    RotorsRight = new List<IMyMotorStator>();
    RotorsSuspension = new List<IMyMotorStator>();
    Turrets = new List<Turret>();
    GatherWheelsAndSuspension(GridTerminalSystem, Me);
    GatherTurrets(GridTerminalSystem, Me);
     
    // set up tool safety stuff
    toolsToReenable = new List<IMyFunctionalBlock>();
     
    // set up mode
    SetMode(Modes.Escort);
     
    // set up persistent data
    persistentData = new PersistentData();
     
    // clear warnings
    Warn("");
     
    // don't act!
    doNotActYet = (currentMode != Modes.Vehicle);
}
 
public void Main(string argument)
{
    if (argument == "")
    {
        Warn("");
        
        // set up reference used for various code functions
        IMyCubeBlock reference = Me;
        
        // find if there are any ship controllers (cockpits, remote control blocks, etc)
        var controllers = new List<IMyShipController>();
        IMyShipController controller = null;
        GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers, (IMyShipController x) => x.CubeGrid == Me.CubeGrid && !(x.CustomData.Contains("nwt")));
        if (controllers.Count() > 0)
        {
            reference = controllers[0];
            foreach (var c in controllers)
            {
                if (c.IsUnderControl)
                {
                    reference = c;
                    controller = c;
                    break;
                }
                  
                if (c.CustomData.Contains("wt-facing"))
                {
                    reference = c;
                }
            }
        }
        
        // count down to persistent data update
        persistentDataUpdateCountdown--;
          
        // save persistent data if countdown is at the right time
        if (persistentDataUpdateCountdown == 0)
        {
            if (hasLoadedData)
            {
                SavePersistentData(Me);
            }
            else
            {
                hasLoadedData = LoadPersistentData(Me);
            }
            persistentDataUpdateCountdown = 10;
        }
        
        if (doNotActYet)
        {
            Warn("Not acting!");
            if ((allowUserControl && controller != null) || followRoutes)
            {
                doNotActYet = false;
            }
            else
            {
                var detected = new List<MyDetectedEntityInfo>();
                // get the face sensors
                var sensors = new List<IMySensorBlock>(); 
                GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, (IMySensorBlock x) => x.CubeGrid == Me.CubeGrid && !(x.CustomData.Contains("nwt"))); 
                 
                // check if there are detected entities
                foreach (var sensor in sensors)
                {
                    var thisDetected = new List<MyDetectedEntityInfo>(); 
                    sensor.DetectedEntities(thisDetected);
                    foreach (var d in thisDetected)
                    {
                        if (d.Type == MyDetectedEntityType.CharacterHuman)
                        {
                            doNotActYet = false;
                            break;
                        }
                    }
                    if (!doNotActYet)
                        break;
                }
            }
             
            Stop();
            return;
        }
        
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
                    currentRoute.AddWaypoint(reference.GetPosition());
                    routeRecordCountdown = 50;
                }
            }
        }
        
        // prepare to blush!
        bool blush = false; 
         
        //Warn(reference.Orientation.Forward == Base6Directions.GetFlippedDirection(Me.Orientation.Forward));
         
        // set up stop distance to properly reflect current grid size
        var boundingBox = Me.CubeGrid.WorldAABB;
        //Log(boundingBox.Size.Length()); 
        STOP_DISTANCE = (float)(boundingBox.Size.Length() / 2 * 1.1);
        if (controllers.Count() > 0)
        {
            var velocity = controllers[0].GetShipSpeed();
            if (velocity > 6)
            {
                STOP_DISTANCE *= (float)(velocity / 6);
            }
        }
        //Log(STOP_DISTANCE);
           
        if (suspensionReworkCountdown <= 0)
        {
            GatherWheelsAndSuspension(GridTerminalSystem, reference);
            SetUpSuspension(GridTerminalSystem, reference);
            GatherTurrets(GridTerminalSystem, reference);
            suspensionReworkCountdown = SUSPENSION_COUNTDOWN;
        }
        else
            suspensionReworkCountdown--;
         
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
                            if (d.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)//d.Type == MyDetectedEntityType.CharacterHuman)
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
         
        if (controller != null && allowUserControl)
        {
            if (controller.MoveIndicator.X == 0 && controller.MoveIndicator.Z == 0)
            {
                Stop();
                courseCorrecting = false;
            }
            else
            {
                double angle = Math.Atan2(-controller.MoveIndicator.X, -controller.MoveIndicator.Z);
                // attempt course correction
                if (angle == 0)
                {
                    if (courseCorrecting)
                    {
                        Move(Me, Vector3D.Add(Me.GetPosition(), CurrentCourseCorrectionOffset));
                    }
                    else
                    {
                        courseCorrecting = true;
                        CurrentCourseCorrectionOffset = Vector3D.Transform(controller.MoveIndicator, Me.WorldMatrix.GetOrientation()); 
                        Move(reference, Vector3D.Add(Me.GetPosition(), CurrentCourseCorrectionOffset));
                    }
                }
                else
                {
                    courseCorrecting = false;
                    Move(angle);
                }
            }
             
            if (controller != null)
            {
                foreach (var turret in Turrets)
                {
                    turret.Move(controller.RotationIndicator);
                }
            }
   
            face[0] = '>'; 
            face[1] = '3'; 
            face[2] = 'o';
   
            // retrieve lights
            var lights = new List<IMyLightingBlock>(); 
            GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights, (IMyLightingBlock x) => x.CubeGrid == Me.CubeGrid && !(x.CustomData.Contains("nwt"))); 
            foreach (var light in lights)
            {
                light.SetValue("Color", new Color(255, 255, 255));
            }
        }
        else if (allowAutonomousBehavior)
        {
            if (backdown > 0)
            {
                backdown--;
   
                Move(Math.PI);
   
                face[0] = '@'; 
                face[1] = 'A'; 
                face[2] = '@'; 
   
                if (backdown == 0)
                    Stop();
            }
             
            bool nearBase = false;
            bool inBase = false;
            var detected = new List<MyDetectedEntityInfo>();
            if (detectEntities)
            {
                // get the face sensors
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
                            bool detectThisEntity = false;
                            if (detectPlayers && d.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)//d.Type == MyDetectedEntityType.CharacterHuman)
                                detectThisEntity = true;
                            else if (detectMonsters && d.Velocity.Length() > 0.1 && d.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)//d.Type == MyDetectedEntityType.CharacterOther && d.Velocity.Length() > 0.1)
                                detectThisEntity = true;
                            else if (detectSmallGrids && d.Type == MyDetectedEntityType.SmallGrid)
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
                                    detected.Add(d);
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
                        var nearBaseBoundingBox = boundingBox.GetInflated(0.75) + reference.WorldMatrix.GetOrientation().Forward * 2;
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
                                if (chaseSingleEnemy)
                                {
                                    x = target.Position.X;
                                    y = target.Position.Y;
                                    z = target.Position.Z;
                                }
                            }
                            
                            if (!chaseSingleEnemy)
                            {
                                x += target.Position.X;
                                y += target.Position.Y;
                                z += target.Position.Z;
                            }
                        }
                        
                        if (!chaseSingleEnemy)
                        {
                            x /= detected.Count();
                            y /= detected.Count();
                            z /= detected.Count();
                        }
                        CurrentTargetLocation = new Vector3D(x, y, z);
                    }
                    else if (followRoutes)
                    {
                        // return to following a route
                        if (!followingRoute)
                            currentRoute.MatchToClosestWaypoint(reference.GetPosition());
                        followingRoute = true;
                    }
                }
            }
   
            if (backdown <= 0)
            {
                // check cameras for forcing backdown
                if (!RaycastCheckTerrain(GridTerminalSystem, reference))
                    backdown = 50;
   
                // stop wander-tan from tipping over
                var remoteControls = new List<IMyShipController>(); 
                GridTerminalSystem.GetBlocksOfType<IMyShipController>(remoteControls, (IMyShipController x) => x.CubeGrid == Me.CubeGrid && !(x.CustomData.Contains("nwt"))); 
   
                if (remoteControls.Count() > 0)
                {
                    var gv = remoteControls.First().GetTotalGravity(); 
                    var fv = Me.WorldMatrix.Forward; 
                    var angle = Math.Acos(fv.Dot(gv) / (fv.Length() * gv.Length())); 
                    if (angle > Math.PI * 3/4 || angle < Math.PI * 1/4)
                        backdown = 50;
                }
   
                // turn off antennas
                /*var beacons = new List<IMyBeacon>(); 
                GridTerminalSystem.GetBlocksOfType<IMyBeacon>(beacons, (IMyBeacon x) => x.CubeGrid == Me.CubeGrid && !(x.CustomData.Contains("nwt"))); 
                var radioAntennas = new List<IMyRadioAntenna>();
                GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(radioAntennas, (IMyRadioAntenna x) => x.CubeGrid == Me.CubeGrid && !(x.CustomData.Contains("nwt")));
                var antennas = new List<IMyFunctionalBlock>();
                foreach (var b in beacons)
                {
                    antennas.Add(b);
                }
                foreach (var r in radioAntennas)
                {
                    antennas.Add(r);
                }
                if (antennas.Count() > 0)
                {
                    var antenna = antennas.First(); 
                    if (inBase || nearBase)
                    {
                        if (inBase)
                        {
                            antenna.SetValue("CustomName", new StringBuilder("In base!"));
                        }
                        else
                        {
                            antenna.SetValue("CustomName", new StringBuilder("Near base!")); 
                        }
                        antenna.GetActionWithName("OnOff_On").Apply(antenna); 
                    }
                    else
                    {
                        antenna.GetActionWithName("OnOff_Off").Apply(antenna); 
                    }
                }*/
                
                //Log(reference.GetPosition());
                //Log(CurrentTargetLocation);
                // follow route if set to do that
                if (followingRoute)
                {
                    Warn("Following route!");
                    
                    if (waitingAtStopCountdown == 0)
                    {
                        //Log(reference.GetPosition());
                        //Log(CurrentTargetLocation);
                        //Log(currentRoute.GetCurrentWaypoint().Position);
                        var dist = Vector3D.Distance(reference.GetPosition(), CurrentTargetLocation);
                        if (dist <= STOP_DISTANCE * 2 && followingRoute)
                        {
                            //Log("Hit a waypoint!");
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
                            
                            //Log("End of route? " + endOfRoute);
                            
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
                                }
                                // otherwise, finish and turn off route following
                                else
                                {
                                    followingRoute = false;
                                    CurrentTargetLocation = reference.GetPosition();
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
                
                var centerPoint = boundingBox.Center;
                var distToCTL = Vector3D.Distance(CurrentTargetLocation, centerPoint);
                if (minDist == -1 || distToCTL < minDist)
                    minDist = distToCTL;
                if (minDist <= STOP_DISTANCE)
                {
                    stop = true; 
                    face[0] = '>'; 
                    face[2] = '<';
                    blush = true;
                }
                else if (minDist <= STOP_DISTANCE * 4)
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
                    stop = true;
                    //Log("Distance REALLY HIGH: " + minDist);
                    face[0] = '.';
                    face[1] = '.';
                    if (followingRoute)
                    {
                        currentRoute.MatchToClosestWaypoint(reference.GetPosition());
                        CurrentTargetLocation = currentRoute.GetCurrentWaypoint().Position;
                    }
                    else
                    {
                        CurrentTargetLocation = reference.GetPosition();
                    }
                }
                
                // stop if needed
                if (stop)// || (inBase && !nearBase))
                {
                    Stop();
                    if (detected.Count() == 0)
                    {
                        /*if (antennas.Count() > 0)
                        {
                            var antenna = antennas.First(); 
                            antenna.SetValue("CustomName", new StringBuilder("Where did you go?! ;_;")); 
                            antenna.GetActionWithName("OnOff_On").Apply(antenna); 
                        }*/
   
                        face[0] = '.'; 
                        face[1] = 'z'; 
                        face[2] = 'Z'; 
                    }
                }
                else if (nearBase)
                {
                    Rotate(reference, CurrentTargetLocation);
                }
                else
                {
                    Move(reference, CurrentTargetLocation);
                }
            }
   
            // retrieve lights
            var lights = new List<IMyLightingBlock>(); 
            GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights, (IMyLightingBlock x) => x.CubeGrid == Me.CubeGrid && !(x.CustomData.Contains("nwt")));
            foreach (var light in lights)
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
         
        if (controller != null)
        {
            if (controller.GetShipVelocities().LinearVelocity.Length() > MAX_VELOCITY)
            {
                CurrentRPM -= RPM_RAMPUP;
            }
            else if (controller.GetShipVelocities().LinearVelocity.Length() < -MAX_VELOCITY)
            {
                CurrentRPM += RPM_RAMPUP;
            }
        }
         
        // retrieve LCDs
        var LCDs = new List<IMyTextPanel>(); 
        GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(LCDs, (IMyTextPanel x) => x.CubeGrid == Me.CubeGrid && !(x.CustomData.Contains("nwt")) && !(x.CustomData.Contains("debug"))); 
        string facestring = " " + face[0] + face[1] + face[2]; 
   
        foreach (var LCD in LCDs)
        {
            LCD.WritePublicText(facestring); 
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
                    }
                }
                else if (argSplit[0] == "route")
                {
                    if (argSplit.Count() == 1)
                    {
                        TextLog.Clear();
                        Log("-route record/finish/clear");
                        Log("record: start adding waypoints.");
                        Log("finish: stop adding waypoints.");
                        Log("clear: clear all waypoints.");
                    }
                    else
                    {
                        if (argSplit[1] == "record")
                        {
                            if (followRoutes)
                            {
                                Log("Can't record a route while on a route!");
                            }
                            else
                            {
                                isRecordingRoute = true;
                                routeRecordCountdown = 0;
                            }
                        }
                        else if (argSplit[1] == "finish")
                        {
                            isRecordingRoute = false;
                        }
                        else if (argSplit[1] == "clear")
                        {
                            currentRoute.ClearWaypoints();
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
    grid.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CubeGrid != reference.CubeGrid && !(x.CustomData.Contains("nwt")) && !(x.CustomData.Contains("wt-turret"))); 
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
    grid.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CubeGrid == reference.CubeGrid && !(x.CustomData.Contains("nwt")) && !(x.CustomData.Contains("wt-turret")));
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
            grid.GetBlocksOfType<IMyShipController>(controllers, (IMyShipController x) => x.CubeGrid == reference.CubeGrid && !(x.CustomData.Contains("nwt"))); 
   
            float torque = SUSPENSION_TORQUE_VALUE * RotorsSuspension.Count();
            /*if (controllers.Count() > 0)
            {
                // calculate pull of natural gravity
                var nv = controllers.First().GetNaturalGravity();
                var Fg = nv.Length() * controllers.First().CalculateShipMass().TotalMass;
                var Ft = Fg / RotorsSuspension.Count();
   
                // calculate pull of artificial gravity
                var av = controllers.First().GetArtificialGravity();
                var amasses = new List<IMyVirtualMass>(); 
                grid.GetBlocksOfType<IMyVirtualMass>(amasses, (IMyVirtualMass x) => x.VirtualMass > 0 && !(x.CustomData.Contains("nwt")));
                float vmass = 0;
                foreach (var mass in amasses)
                {
                    vmass += mass.VirtualMass;
                }
                var Fa = av.Length() * vmass;
                Ft += Fa / RotorsSuspension.Count();
   
                torque = (float)(Ft * SUSPENSION_TORQUE_MOD);
            }*/
   
            rotor.SetValueFloat("Torque", torque);
            bool inverted = false;
            if (angle >= 0 && angle <= Math.PI)
            {
                inverted = rotor.Orientation.Up != reference.Orientation.Forward;
            }
            else
            {
                inverted = rotor.Orientation.Up == reference.Orientation.Forward;
            }
               
            if (SUSPENSION_INVERT)
                inverted = !inverted;
               
            if (inverted)
            {
                rotor.SetValueFloat("Velocity", -SUSPENSION_RPM);
                rotor.SetValueFloat("LowerLimit", -SUSPENSION_UPPER_LIMIT); 
                rotor.SetValueFloat("UpperLimit", -SUSPENSION_LOWER_LIMIT);
            }
            else
            {
                rotor.SetValueFloat("Velocity", SUSPENSION_RPM);
                rotor.SetValueFloat("LowerLimit", SUSPENSION_LOWER_LIMIT); 
                rotor.SetValueFloat("UpperLimit", SUSPENSION_UPPER_LIMIT);
            }
        }
           
           
        double avgAngle = 0;
           
        foreach (var rotor in RotorsSuspension)
        {
            avgAngle += Math.Abs(rotor.Angle * 180 / Math.PI);
        }
          
        var suspensionDiff = (SUSPENSION_UPPER_LIMIT - SUSPENSION_LOWER_LIMIT) / 5;
          
        avgAngle /= RotorsSuspension.Count() / 2;
          
        if (avgAngle - SUSPENSION_LOWER_LIMIT <= suspensionDiff)
        {
            SUSPENSION_TORQUE_MOD += SUSPENSION_TORQUE_MOD_ADJUST;
        }
        else if (SUSPENSION_UPPER_LIMIT - avgAngle <= suspensionDiff)
        {
            SUSPENSION_TORQUE_MOD -= SUSPENSION_TORQUE_MOD_ADJUST;
        }
           
        if (SUSPENSION_TORQUE_MOD < SUSPENSION_TORQUE_MOD_ADJUST)
            SUSPENSION_TORQUE_MOD = SUSPENSION_TORQUE_MOD_ADJUST;
        else if (SUSPENSION_TORQUE_MOD > SUSPENSION_TORQUE_MOD_ADJUST * 30)
            SUSPENSION_TORQUE_MOD = SUSPENSION_TORQUE_MOD_ADJUST * 30;
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
        face[0] = '>';
        stopLeft = true;
    }
    if (RotorsRight.Count() == 0)
    {
        face[2] = '<';
        stopRight = true;
    }
    if (stopLeft || stopRight)
    {
        if (stopLeft && stopRight)
            face[1] = '_';
        Stop();
        return;
    }
       
    var RPM_ToUse = SUSPENSION_INVERT ? -CurrentRPM : CurrentRPM;
       
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
    automaticSuspensionAdjustment = false;
    followRoutes = false;
    enableToolSafety = false;
    chaseSingleEnemy = false;
    followingRoute = false;
       
    currentMode = mode;
    switch (mode)
    {
        case Modes.Vehicle:
            stopNearBases = true;
            allowUserControl = true;
            automaticSuspensionAdjustment = true;
            break;
        case Modes.Follow:
            detectEntities = true;
            detectPlayers = true;
            detectLargeGrids = true;
            stopNearBases = true;
            enableFollowBehavior = true;
            allowUserControl = true;
            allowAutonomousBehavior = true;
            automaticSuspensionAdjustment = true;
            break;
        case Modes.Ram:
            detectEntities = true;
            detectMonsters = true;
            detectLargeGrids = true;
            stopNearBases = true;
            enableFollowBehavior = true;
            allowUserControl = true;
            allowAutonomousBehavior = true;
            automaticSuspensionAdjustment = true;
            enableToolSafety = true;
            chaseSingleEnemy = true;
            break;
        case Modes.Escort:
            detectEntities = true;
            detectPlayers = true;
            detectLargeGrids = true;
            enableFollowBehavior = true;
            stopNearBases = true;
            allowUserControl = true;
            allowAutonomousBehavior = true;
            automaticSuspensionAdjustment = true;
            break;
        case Modes.Patrol:
            followingRoute = true;
            allowAutonomousBehavior = true;
            automaticSuspensionAdjustment = true;
            followRoutes = true;
            
            currentRoute.MatchToClosestWaypoint(Me.GetPosition());
            CurrentTargetLocation = currentRoute.GetCurrentWaypoint().Position;
            break;
        case Modes.Hunt:
            followingRoute = true;
            detectEntities = true;
            detectMonsters = true;
            enableFollowBehavior = true;
            allowAutonomousBehavior = true;
            automaticSuspensionAdjustment = true;
            followRoutes = true;
            chaseSingleEnemy = true;
            enableToolSafety = true;
            
            currentRoute.MatchToClosestWaypoint(Me.GetPosition());
            CurrentTargetLocation = currentRoute.GetCurrentWaypoint().Position;
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
            CurrentTargetLocation = currentRoute.GetCurrentWaypoint().Position;
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
        pData.Mode = Modes.Escort;
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
        pData.Mode = Modes.Escort;
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
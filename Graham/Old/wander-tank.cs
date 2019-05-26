public const float MAX_VELOCITY = 60f;
public const float SPEED_MOD = 5f;
public const float STOP_DISTANCE = 10f; 
public const double START_TURN = 0; 
public const double END_TURN = Math.PI / 4; 
public const float TORQUE_VALUE = 8000f;
public const float TURRET_ROT_SPEED = 10f;
public const double TURRET_MINIMUM_INPUT = 5f;

public float CurrentVelocity = 0f; 
public enum Mouths { line, w, A, tilde, o } 
public enum Eyes { o, brackets, x } 
public Mouths mouth; 
public Eyes eyes; 
public int blushcolor; 
public char[] face; 
public int backdown;
public float runningX = 0;
public float runningY = 0;
 
List<IMyMotorStator> RotorsLeft, RotorsRight, RotorsSuspension;
List<Turret> Turrets;
 
public static List<string> TextLog; 
  
public static void Log(object text) 
{ 
    TextLog.Add(text.ToString());
    if (TextLog.Count() > 20)
        TextLog.RemoveAt(0);
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
        m_pitchInverted = m_pitch.CustomData.Contains("inverted");
        m_yawInverted = m_yaw.CustomData.Contains("inverted");
        m_rotSpeed = rotSpeed;
    }
    
    public void Move(Vector2 angles)
    {
        float pitchSpeed = 0;
        float yawSpeed = 0;
        
        if (angles.X < -TURRET_MINIMUM_INPUT || angles.X > TURRET_MINIMUM_INPUT)
            pitchSpeed = m_rotSpeed * (float)(angles.X / TURRET_MINIMUM_INPUT);
        if (m_pitchInverted)
            pitchSpeed = -pitchSpeed;
        m_pitch.SetValueFloat("Velocity", pitchSpeed);
        
        if (angles.Y < -TURRET_MINIMUM_INPUT || angles.Y > TURRET_MINIMUM_INPUT)
            yawSpeed = m_rotSpeed * (float)(angles.Y / TURRET_MINIMUM_INPUT);
        if (m_yawInverted)
            yawSpeed = -yawSpeed;
        m_yaw.SetValueFloat("Velocity", yawSpeed);
    }
}

public Program() 
{ 
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
    
    RotorsLeft = new List<IMyMotorStator>();
    RotorsRight = new List<IMyMotorStator>();
    RotorsSuspension = new List<IMyMotorStator>();
    Turrets = new List<Turret>();
    
    GatherWheelsAndSuspension(Me, GridTerminalSystem);
} 

// returns whether or not suspension is present
public bool HasSuspension()
{
    return RotorsSuspension != null && RotorsSuspension.Count() > 0;
}

public void GatherTurrets(IMyCubeBlock reference, IMyGridTerminalSystem grid)
{
    Log("Gathering turrets.");
    
    // clear turrets
    Turrets.Clear();
    
    var rotors = new List<IMyMotorStator>(); 
    grid.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CustomData.Contains("turret"));
    if (rotors.First().CubeGrid == reference.CubeGrid)
        Turrets.Add(new Turret("turret1", rotors[1], rotors[0], TURRET_ROT_SPEED));
    else
        Turrets.Add(new Turret("turret1", rotors[0], rotors[1], TURRET_ROT_SPEED));
}

// populates (or re-populates) the list of wheels and suspension rotors
public void GatherWheelsAndSuspension(IMyCubeBlock reference, IMyGridTerminalSystem grid)
{
    Log("Gathering wheels and suspension.");
    
    // clear wheels and suspension
    RotorsRight.Clear();
    RotorsLeft.Clear();
    RotorsSuspension.Clear();
    
    var rotors = new List<IMyMotorStator>(); 
    grid.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CubeGrid != reference.CubeGrid && !(x.CustomData.Contains("turret")));
    if (rotors.Count() > 0) 
    {
        // it has suspension! first, gather the wheels 
        foreach (var rotor in rotors) 
        {
            var localRotorPosition = Vector3D.Transform(rotor.GetPosition(), MatrixD.Invert(reference.WorldMatrix)); 
            double angle = Math.Atan2(-localRotorPosition.X, -localRotorPosition.Z); 
             
            if (angle < 0) 
                RotorsRight.Add(rotor); 
            else 
                RotorsLeft.Add(rotor); 
        } 
         
        // gather the suspension 
        grid.GetBlocksOfType<IMyMotorStator>(RotorsSuspension, (IMyMotorStator x) => x.CubeGrid == reference.CubeGrid && !(x.CustomData.Contains("turret")));
    }
    else
    {
        rotors = new List<IMyMotorStator>(); 
        grid.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CubeGrid == reference.CubeGrid && !(x.CustomData.Contains("turret"))); 
        foreach (var rotor in rotors) 
        { 
            if (rotor.Orientation.Up == Base6Directions.GetFlippedDirection(reference.Orientation.Left)) 
                RotorsRight.Add(rotor); 
            else if (rotor.Orientation.Up == reference.Orientation.Left) 
                RotorsLeft.Add(rotor); 
        } 
    } 
}

// sets up suspension
public void SetUpSuspension(IMyCubeBlock reference, IMyGridTerminalSystem grid)
{
    Log("Setting up suspension.");
    
    if (HasSuspension())
    {
        foreach (var rotor in RotorsSuspension) 
        {
            var localRotorPosition = Vector3D.Transform(rotor.GetPosition(), MatrixD.Invert(reference.WorldMatrix)); 
            double angle = Math.Atan2(-localRotorPosition.X, -localRotorPosition.Z); 
            
            // stop wander-tan from tipping over 
            var remoteControls = new List<IMyShipController>(); 
            grid.GetBlocksOfType<IMyShipController>(remoteControls, (IMyShipController x) => x.CubeGrid == reference.CubeGrid); 
             
            float torque = TORQUE_VALUE * RotorsSuspension.Count(); 
            if (remoteControls.Count() > 0) 
            { 
                var gv = remoteControls.First().GetTotalGravity(); 
                torque *= (float)(gv.Length() / 10);
            } 
             
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
            if (inverted)
            {
                rotor.SetValueFloat("Velocity", -5.23f); 
                rotor.SetValueFloat("LowerLimit", -10f); 
                rotor.SetValueFloat("UpperLimit", 0f); 
            } 
            else 
            { 
                rotor.SetValueFloat("Velocity", 5.23f); 
                rotor.SetValueFloat("LowerLimit", 0f); 
                rotor.SetValueFloat("UpperLimit", 10f); 
            }
        }
    }
}

// stops wander-tan completely
public void Stop()
{
    Log("Stopping.");
    CurrentVelocity = 0;
    
    foreach (var rotor in RotorsRight) 
        rotor.SetValueFloat("Velocity", 0); 
    foreach (var rotor in RotorsLeft) 
        rotor.SetValueFloat("Velocity", 0);
}

// directs wander-tan to move towards a certain angle, relative to her current facing direction.
// angle is given in radians
//        0
// -π/2       π/2
//        π
public void Move(double angle, float maxVelocity = MAX_VELOCITY, double startTurn = START_TURN, double endTurn = END_TURN)
{
    if (CurrentVelocity < maxVelocity)
        CurrentVelocity += SPEED_MOD;
    
    if (CurrentVelocity > maxVelocity)
        CurrentVelocity = maxVelocity;
    
    Log("Moving at angle " + angle.ToString());
    if (angle == Math.PI || angle == -Math.PI)
    {
        foreach (var rotor in RotorsRight) 
            rotor.SetValueFloat("Velocity", CurrentVelocity / 2f); 
        foreach (var rotor in RotorsLeft) 
            rotor.SetValueFloat("Velocity", -CurrentVelocity / 2f);
    }
    else if (angle <= -startTurn) 
    { 
        foreach (var rotor in RotorsRight) 
            rotor.SetValueFloat("Velocity", -CurrentVelocity); 
        foreach (var rotor in RotorsLeft) 
        { 
            if (angle <= -endTurn) 
                rotor.SetValueFloat("Velocity", -CurrentVelocity); 
            else 
                rotor.SetValueFloat("Velocity", -(float)(-((2 * CurrentVelocity) / (endTurn - startTurn) * angle + CurrentVelocity))); 
        } 
    } 
    else if (angle >= startTurn) 
    { 
        foreach (var rotor in RotorsRight) 
        { 
            if (angle >= endTurn) 
                rotor.SetValueFloat("Velocity", CurrentVelocity); 
            else 
                rotor.SetValueFloat("Velocity", (float)((2 * CurrentVelocity) / (endTurn - startTurn) * angle + CurrentVelocity)); 
        } 
        foreach (var rotor in RotorsLeft) 
            rotor.SetValueFloat("Velocity", CurrentVelocity); 
    } 
    else 
    { 
        foreach (var rotor in RotorsRight)
            rotor.SetValueFloat("Velocity", -CurrentVelocity); 
        foreach (var rotor in RotorsLeft) 
            rotor.SetValueFloat("Velocity", CurrentVelocity); 
    } 
}

public void Main(string argument) 
{
    bool blush = false; 
    
    var controllers = new List<IMyShipController>();
    IMyShipController controller = null;
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers, (IMyShipController x) => x.CubeGrid == Me.CubeGrid);
    foreach (var c in controllers)
    {
        if (c.IsUnderControl)
        {
            controller = c;
            break;
        }
    }
    
    if (argument == "")
    {
        GatherWheelsAndSuspension(Me, GridTerminalSystem);
        SetUpSuspension(Me, GridTerminalSystem);
        
        if (controller != null)
        {
            if (controller.MoveIndicator.Length() == 0)
            {
                Stop();
            }
            else
            {
                double angle = Math.Atan2(-controller.MoveIndicator.X, -controller.MoveIndicator.Z);
                Move(angle);
            }
            
            GatherTurrets(Me, GridTerminalSystem);
            foreach (var turret in Turrets)
            {
                turret.Move(controller.RotationIndicator);
            }
            
            runningX += controller.RotationIndicator.X;
            runningY += controller.RotationIndicator.Y;
            
            
            face[0] = '>'; 
            face[1] = '3'; 
            face[2] = 'o';
            
            // retrieve LCDs 
            var LCDs = new List<IMyTextPanel>(); 
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(LCDs);//, (IMyTextPanel x) => x.CubeGrid == Me.CubeGrid); 
            string facestring = " " + face[0] + face[1] + face[2]; 
             
            foreach (var LCD in LCDs) 
            { 
                LCD.WritePublicText(facestring);
                LCD.WritePublicText(runningX.ToString() + '\n' + runningY.ToString());
            }
            
            // retrieve lights 
            var lights = new List<IMyLightingBlock>(); 
            GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights, (IMyLightingBlock x) => x.CubeGrid == Me.CubeGrid); 
            foreach (var light in lights) 
            {
                light.SetValue("Color", new Color(255, 255, 255));
            } 
            
        }
        else
        {
            // get the face sensor 
            var sensors = new List<IMySensorBlock>(); 
            GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, (IMySensorBlock x) => x.CubeGrid == Me.CubeGrid); 
             
            if (sensors.Count() > 0) 
            {
                var sensor = sensors.First(); 
                
                if (backdown > 0) 
                { 
                    backdown--;  
                    
                    Move(Math.PI / 2);
                    
                    face[0] = '>'; 
                    face[1] = '_'; 
                    face[2] = '<'; 
                } 
                else 
                {
                    // stop wander-tan from tipping over 
                    var remoteControls = new List<IMyShipController>(); 
                    GridTerminalSystem.GetBlocksOfType<IMyShipController>(remoteControls, (IMyShipController x) => x.CubeGrid == Me.CubeGrid); 
                     
                    if (remoteControls.Count() > 0) 
                    { 
                        var gv = remoteControls.First().GetTotalGravity(); 
                        var fv = sensor.WorldMatrix.Forward; 
                        var angle = Math.Acos(fv.Dot(gv) / (fv.Length() * gv.Length())); 
                        if (angle > Math.PI * 3/4 || angle < Math.PI * 1/4) 
                            backdown = 50; 
                    } 
                     
                    // check if there are detected entities 
                    var detected = new List<MyDetectedEntityInfo>(); 
                    sensor.DetectedEntities(detected); 
                     
                    if (detected.Count() > 0) 
                    {
                        // turn off antennas
                        var antennas = new List<IMyBeacon>(); 
                        GridTerminalSystem.GetBlocksOfType<IMyBeacon>(antennas, (IMyBeacon x) => x.CubeGrid == Me.CubeGrid); 
                        if (antennas.Count() > 0) 
                        { 
                            var antenna = antennas.First(); 
                            antenna.GetActionWithName("OnOff_Off").Apply(antenna); 
                        } 
                        
                        /*
                        // move players to the top of the list
                        for (int i = 0; i < detected.Count(); i++) 
                        { 
                            if (detected[i].Type == MyDetectedEntityType.CharacterHuman) 
                            { 
                                detected.Insert(0, detected[i]); 
                                detected.RemoveAt(i + 1); 
                            } 
                        } 
                        */
                        
                        // stop if close enough to any entities
                        bool stop = false;
                        double minDist = -1;
                        foreach (var t in detected) 
                        { 
                            var tdist = Vector3D.Distance(t.Position, sensor.GetPosition()); 
                            if (minDist == -1 || tdist < minDist)
                                minDist = tdist;
                        }
                        
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
                        else 
                        { 
                            face[1] = 'A'; 
                        } 
                        
                        // gather overall target direction
                        if (stop)
                            Stop();
                        else
                        {
                            double direction = 0;
                            foreach (var target in detected)
                            {
                                var localTargetPosition = Vector3D.Transform(target.Position, MatrixD.Invert(sensor.WorldMatrix)); 
                                double angle = -Math.Atan2(-localTargetPosition.X, -localTargetPosition.Z);
                                direction += angle;
                            }
                        
                            direction /= detected.Count();
                            
                            Move(-direction);
                        }
                    } 
                    else 
                    { 
                        var antennas = new List<IMyBeacon>(); 
                        GridTerminalSystem.GetBlocksOfType<IMyBeacon>(antennas, (IMyBeacon x) => x.CubeGrid == Me.CubeGrid); 
                        if (antennas.Count() > 0) 
                        { 
                            var antenna = antennas.First(); 
                            antenna.SetValue("CustomName", new StringBuilder("Where did you go?! ;_;")); 
                            antenna.GetActionWithName("OnOff_On").Apply(antenna); 
                        } 
                         
                        face[0] = '.'; 
                        face[1] = 'z'; 
                        face[2] = 'Z'; 
                         
                        Stop();
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
             
            // retrieve LCDs 
            var lights = new List<IMyLightingBlock>(); 
            GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights, (IMyLightingBlock x) => x.CubeGrid == Me.CubeGrid); 
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
             
            // retrieve LCDs 
            var LCDs = new List<IMyTextPanel>(); 
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(LCDs, (IMyTextPanel x) => x.CubeGrid == Me.CubeGrid); 
            string facestring = " " + face[0] + face[1] + face[2]; 
             
            foreach (var LCD in LCDs) 
            { 
                LCD.WritePublicText(facestring); 
            }
        }
    }
    else if (argument.ToLower() == "setup")
    {
        GatherWheelsAndSuspension(Me, GridTerminalSystem);
        SetUpSuspension(Me, GridTerminalSystem);
    }
    else if (argument.ToLower() == "move 180" || argument.ToLower() == "move -180")
    {
        Move(Math.PI);
    }
    else if (argument.ToLower().Contains("move "))
    {
        Move(double.Parse(argument.ToLower().Replace("move ", "")) * (Math.PI / 180));
    }
    else if (argument.ToLower() == "stop")
    {
        Stop();
    }
    else if (argument.ToLower() == "reset")
    {
        runningX = 0;
        runningY = 0;
    }
    foreach (var line in TextLog) 
    { 
        Echo(line); 
    } 
}
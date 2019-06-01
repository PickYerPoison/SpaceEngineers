// yaw: 2800 = 360
// pitch: 720 = 90

// 2800 = 360 for all?

public const float TURRET_MAX_YAW_SPEED = 2f;
public const float TURRET_MAX_PITCH_SPEED = 1f;

public const float ANGLE_TOLERANCE = (float)Math.PI / 16;

public double RunningX = 0;
public double RunningY = 0;

List<Turret> Turrets;

// shared ini parser instance
public static MyIni _ini = new MyIni();

public static double GetAngleDifference(double r1, double r2)
{
    var a = r1 - r2;
    a += (a>Math.PI) ? -2*Math.PI : (a<-Math.PI) ? 2*Math.PI: 0;
    return a;
}

public class Turret
{
    List<IMyMotorStator> m_rotors;
    double m_pitchSpeed;
    double m_yawSpeed;
    string m_name;
    public double m_desiredPitch;
    public double m_desiredYaw;
    
    public float Yaw
    {
        get
        {
            foreach (var rotor in m_rotors)
            {
                if (rotor.CustomData.Contains("yaw"))
                    return rotor.Angle;
            }
            
            return 0;
        }
    }
    
    public float Pitch
    {
        get
        {
            foreach (var rotor in m_rotors)
            {
                if (rotor.CustomData.Contains("pitch"))
                    return rotor.Angle;
            }
            
            return 0;
        }
    }
    
    public Turret(string name, double pitchSpeed, double yawSpeed)
    {
        m_name = name;
        m_pitchSpeed = pitchSpeed;
        m_yawSpeed = yawSpeed;
        m_desiredPitch = 0;
        m_desiredYaw = 0;
        m_rotors = new List<IMyMotorStator>();
    }
    
    public void AddRotation(Vector2 input)
    {
        m_desiredPitch -= input.X;
        if (m_desiredPitch > Math.PI)
        {
            m_desiredPitch = Math.PI;
        }
        else if (m_desiredPitch < -Math.PI)
        {
            m_desiredPitch = -Math.PI;
        }
        
        m_desiredYaw -= input.Y;
        if (m_desiredYaw > Math.PI)
        {
            m_desiredYaw -= 2*Math.PI;
        }
        else if (m_desiredYaw < -Math.PI)
        {
            m_desiredYaw += 2*Math.PI;
        }
    }
    
    public void Update()
    {
        foreach (var rotor in m_rotors)
        {
            bool iniParsed = false;
            iniParsed = _ini.TryParse(rotor.CustomData);
            
            if (iniParsed == true && _ini.ContainsKey("turret", "type"))
            {
                float speed = 0;
                float neededDistance = 0;
                
                // direction
                if (_ini.Get("turret", "type").ToString().ToLower() == "yaw")
                {
                    neededDistance = (float)GetAngleDifference(rotor.Angle, m_desiredYaw);
                    
                    if (Math.Abs(neededDistance) > ANGLE_TOLERANCE)
                    {
                        if (Math.Abs(neededDistance) < m_yawSpeed)
                        {
                            speed = neededDistance;
                        }
                        else
                        {
                            if (neededDistance > 0)
                            {
                                speed = (float)m_yawSpeed;
                            }
                            else
                            {
                                speed = -(float)m_yawSpeed;
                            }
                        }
                    }
                }
                else if (_ini.Get("turret", "type").ToString().ToLower() == "pitch")
                {
                    neededDistance = (float)GetAngleDifference(rotor.Angle, m_desiredPitch);
                    
                    if (Math.Abs(neededDistance) > ANGLE_TOLERANCE)
                    {
                        if (Math.Abs(neededDistance) < m_pitchSpeed)
                        {
                            speed = -neededDistance;
                        }
                        else
                        {
                            if (neededDistance > 0)
                            {
                                speed = -(float)m_pitchSpeed;
                            }
                            else
                            {
                                speed = (float)m_pitchSpeed;
                            }
                        }
                    }
                }
                
                // inverted
                if (_ini.ContainsKey("turret", "inverted") && _ini.Get("turret", "inverted").ToBoolean() == true)
                {
                    speed = -speed;
                }
                rotor.SetValueFloat("Velocity", speed);
            }
        }
    }
    
    public void ResetRotation()
    {
        m_desiredPitch = 0;
        m_desiredYaw = 0;
    }
    
    public void AddRotor(IMyMotorStator rotor)
    {
        if (m_rotors.Contains(rotor) == false)
        {
            m_rotors.Add(rotor);
        }
    }
    
    public string Name
    {
        get
        {
            return m_name;
        }
    }
}

public Program() 
{ 
    // begin update frequency
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    Turrets = new List<Turret>();
    
    GatherTurrets(GridTerminalSystem);
    
    foreach (var turret in Turrets)
    {
        Echo(turret.Name);
    }
}

public void GatherTurrets(IMyGridTerminalSystem grid)
{
    // clear turrets
    Turrets.Clear();
    var TurretNames = new List<string>();
    
    var rotors = new List<IMyMotorStator>(); 
    grid.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CustomData.Contains("[turret]"));
    foreach (var rotor in rotors)
    {
        bool iniParsed = false;
        iniParsed = _ini.TryParse(rotor.CustomData);
        
        if (iniParsed == true && _ini.ContainsKey("turret", "name") == true)
        {
            var name = _ini.Get("turret", "name").ToString();
            
            if (TurretNames.Contains(name) == true)
            {
                foreach (var turret in Turrets)
                {
                    if (turret.Name == name)
                    {
                        turret.AddRotor(rotor);
                        break;
                    }
                }
            }
            else
            {
                var turret = new Turret(name, TURRET_MAX_PITCH_SPEED, TURRET_MAX_YAW_SPEED);
                turret.AddRotor(rotor);
                Turrets.Add(turret);
                TurretNames.Add(name);
            }
        }
    }
}

public void Main(string argument) 
{
    var controlledTurrets = new List<string>();
    var controllers = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers, (IMyShipController x) => x.CustomData.Contains("[turret]"));
    foreach (var controller in controllers)
    {
        bool iniParsed = false;
        iniParsed = _ini.TryParse(controller.CustomData);
        
        if (iniParsed == true && _ini.ContainsKey("turret", "name") == true)
        {
            var name = _ini.Get("turret", "name").ToString();
            
            if (controller.IsUnderControl)
            {
                controlledTurrets.Add(name);
                foreach (var turret in Turrets)
                {
                    if (turret.Name == name)
                    {
                        turret.AddRotation(TranslateRotation(controller.RotationIndicator));
                    }
                }
            }
            else
            {
                RunningX = 0;
                RunningY = 0;
                
                if (controlledTurrets.Contains(name) == false)
                {
                    foreach (var turret in Turrets)
                    {
                        if (turret.Name == name)
                        {
                            turret.ResetRotation();
                        }
                    }
                }
            }
        }
    }
    
    foreach (var turret in Turrets)
    {
        turret.Update();
        
        Echo(turret.m_desiredPitch.ToString());
        Echo(turret.m_desiredYaw.ToString());
        
        Echo("");
        
        Echo(turret.Pitch.ToString());
        Echo(turret.Yaw.ToString());
        
        Echo("");
        
        Echo(Math.Abs(GetAngleDifference(turret.m_desiredPitch, turret.Pitch)).ToString());
        Echo(Math.Abs(GetAngleDifference(turret.m_desiredYaw, turret.Yaw)).ToString());
        
    }
}

public static Vector2 TranslateRotation(Vector2 input)
{
    return Vector2.Multiply(input, (float)(2 * Math.PI / 2800));
}
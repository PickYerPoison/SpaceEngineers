public const float TURRET_MAX_ROT_SPEED = 10f;
public const float PISTON_MAX_MOV_SPEED = 10f;
public const float TURRET_MAXIMUM_INPUT = 10f;
public const float PISTON_MAXIMUM_INPUT = 10f;

List<Turret> Turrets;

// shared ini parser instance
public static MyIni _ini = new MyIni();

public class Turret
{
    List<IMyMotorStator> m_rotors;
    List<IMyPistonBase> m_pistons;
    float m_rotSpeed;
    float m_moveSpeed;
    string m_name;
    
    public Turret(string name, float rotSpeed, float moveSpeed)
    {
        m_name = name;
        m_rotSpeed = rotSpeed;
        m_moveSpeed = moveSpeed;
        m_rotors = new List<IMyMotorStator>();
        m_pistons = new List<IMyPistonBase>();
    }
    
    public void Rotate(Vector2 input)
    {
        float pitchSpeed = m_rotSpeed * (input.X / TURRET_MAXIMUM_INPUT);
        float yawSpeed = m_rotSpeed * (input.Y / TURRET_MAXIMUM_INPUT);
        
        foreach (var rotor in m_rotors)
        {
            bool iniParsed = false;
            iniParsed = _ini.TryParse(rotor.CustomData);
            
            if (iniParsed == true)
            {
                float speed = 0;
                
                // direction
                if (_ini.ContainsKey("turret", "type") && _ini.Get("turret", "type").ToString().ToLower() == "yaw")
                {
                    speed = yawSpeed;
                }
                else if (_ini.ContainsKey("turret", "type") && _ini.Get("turret", "type").ToString().ToLower() == "pitch")
                {
                    speed = pitchSpeed;
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
    
    public void Move(Vector3 input)
    {
        float upSpeed = m_moveSpeed * (input.Y / PISTON_MAXIMUM_INPUT);
        float rightSpeed = m_moveSpeed * (input.X / PISTON_MAXIMUM_INPUT);
        float forwardSpeed = -m_moveSpeed * (input.Z / PISTON_MAXIMUM_INPUT);
        
        foreach (var piston in m_pistons)
        {
            bool iniParsed = false;
            iniParsed = _ini.TryParse(piston.CustomData);
            
            if (iniParsed == true)
            {
                float speed = 0;
                
                // direction
                if (_ini.ContainsKey("turret", "type") && (_ini.Get("turret", "type").ToString().ToLower() == "up" || _ini.Get("turret", "type").ToString().ToLower() == "down"))
                {
                    speed = upSpeed;
                }
                else if (_ini.ContainsKey("turret", "type") && (_ini.Get("turret", "type").ToString().ToLower() == "left" || _ini.Get("turret", "type").ToString().ToLower() == "right"))
                {
                    speed = rightSpeed;
                }
                else if (_ini.ContainsKey("turret", "type") && (_ini.Get("turret", "type").ToString().ToLower() == "forward" || _ini.Get("turret", "type").ToString().ToLower() == "backward"))
                {
                    speed = forwardSpeed;
                }
                
                // inverted
                if (_ini.ContainsKey("turret", "inverted") && _ini.Get("turret", "inverted").ToBoolean() == true)
                {
                    speed = -speed;
                }
                piston.SetValueFloat("Velocity", speed);
            }
        }
    }
    
    public void AddRotor(IMyMotorStator rotor)
    {
        if (m_rotors.Contains(rotor) == false)
        {
            m_rotors.Add(rotor);
        }
    }
    
    public void AddPiston(IMyPistonBase piston)
    {
        if (m_pistons.Contains(piston) == false)
        {
            m_pistons.Add(piston);
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
        
        if (iniParsed == true)
        {
            if (_ini.ContainsKey("turret", "name") == true)
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
                    var turret = new Turret(name, TURRET_MAX_ROT_SPEED, PISTON_MAX_MOV_SPEED);
                    turret.AddRotor(rotor);
                    Turrets.Add(turret);
                    TurretNames.Add(name);
                }
            }
        }
    }
    
    var pistons = new List<IMyPistonBase>(); 
    grid.GetBlocksOfType<IMyPistonBase>(pistons, (IMyPistonBase x) => x.CustomData.Contains("[turret]"));
    foreach (var piston in pistons)
    {
        bool iniParsed = false;
        iniParsed = _ini.TryParse(piston.CustomData);
        
        if (iniParsed == true)
        {
            if (_ini.ContainsKey("turret", "name") == true)
            {
                var name = _ini.Get("turret", "name").ToString();
                
                if (TurretNames.Contains(name) == true)
                {
                    foreach (var turret in Turrets)
                    {
                        if (turret.Name == name)
                        {
                            turret.AddPiston(piston);
                            break;
                        }
                    }
                }
                else
                {
                    var turret = new Turret(name, TURRET_MAX_ROT_SPEED, PISTON_MAX_MOV_SPEED);
                    turret.AddPiston(piston);
                    Turrets.Add(turret);
                    TurretNames.Add(name);
                }
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
        
        if (iniParsed == true)
        {
            if (_ini.ContainsKey("turret", "name") == true)
            {
                var name = _ini.Get("turret", "name").ToString();
                
                if (controller.IsUnderControl)
                {
                    controlledTurrets.Add(name);
                    foreach (var turret in Turrets)
                    {
                        if (turret.Name == name)
                        {
                            turret.Move(controller.MoveIndicator);
                            turret.Rotate(controller.RotationIndicator);
                        }
                    }
                }
                else if (controlledTurrets.Contains(name) == false)
                {
                    foreach (var turret in Turrets)
                    {
                        if (turret.Name == name)
                        {
                            turret.Move(new Vector3(0, 0, 0));
                            turret.Rotate(new Vector2(0, 0));
                        }
                    }
                }
            }
        }
    }
}
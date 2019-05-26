// NOTE: PLACE ROTORS WITH 180 FACING DOWNWARDS!

List<Rotor> Rotors;
IMyShipController Controller;

public class Rotor
{
    IMyMotorStator m_stator;
    
    float m_desiredAngle;
    float m_maxVelocity;
    float m_desiredVelocity;
    float m_velocityIncrement;
    double m_angleTolerance;
    double m_alignmentTolerance;
    bool inverted;
    
    public Rotor(IMyMotorStator rotor)
    {
        m_stator = rotor;
        inverted = rotor.CustomData.Contains("inverted");
        m_desiredAngle = 0;
        m_maxVelocity = 20f;
        m_desiredVelocity = 0f;
        m_velocityIncrement = 5f;
        m_angleTolerance = 0.005;
        m_alignmentTolerance = 0.1;
    }
    
    public double GetPercentAligned()
    {
        var radsToAngle = Math.Abs(GetRadiansToAngle());
        if (radsToAngle > m_alignmentTolerance)
        {
            return 0;
        }
        else
        {
            return 1.0 - (radsToAngle / m_alignmentTolerance);
        }
    }
    
    public void SetDesiredAngle(float desiredAngle)
    {
        if (m_stator.LowerLimitRad != float.MinValue && m_stator.UpperLimitRad != float.MaxValue)
        {
            if (desiredAngle < m_stator.LowerLimitRad)
            {
                desiredAngle += (float)Math.PI;
            }
            else if (desiredAngle > m_stator.UpperLimitRad)
            {
                desiredAngle -= (float)Math.PI;
            }
        }
        m_desiredAngle = desiredAngle;
    }
    
    public void Update()
    {
        var radiansToAngle = GetRadiansToAngle();
        if (Math.Abs(radiansToAngle) >= m_angleTolerance)
        {
            m_desiredVelocity = GetDesiredVelocity(radiansToAngle);
        }
        else
        {
            m_desiredVelocity = 0;
        }
        
        if (Math.Abs(Velocity - m_desiredVelocity) <= m_velocityIncrement)
        {
            Velocity = m_desiredVelocity;
        }
        else if (Velocity > m_desiredVelocity)
        {
            Velocity -= m_velocityIncrement;
        }
        else if (Velocity < m_desiredVelocity)
        {
            Velocity += m_velocityIncrement;
        }
    }
    
    public float Velocity
    {
        get
        {
            return m_stator.GetValueFloat("Velocity");
        }
        set
        {
            m_stator.SetValueFloat("Velocity", value);
        }
    }
    
    public float Angle
    {
        get
        {
            return Stator.Angle;
        }
    }
    
    public IMyMotorStator Stator
    {
        get
        {
            return m_stator;
        }
    }
    
    int GetDirectionToAngle()
    {
        int direction = 0;
        if (Angle < m_desiredAngle)
        {
            direction = 1;
        }
        else
        {
            direction = -1;
        }
        
        if (Math.Abs(Angle - m_desiredAngle) > Math.PI)
        {
            direction = -direction;
        }
        
        return direction;
    }
    
    float GetRadiansToAngle()
    {
        double desiredAngle = m_desiredAngle;
        int direction = GetDirectionToAngle();
        
        double distance = 0;
        if (direction > 0 && m_desiredAngle < Angle)
        {
            desiredAngle += Math.PI * 2;
        }
        else if (direction < 0 && m_desiredAngle > Angle)
        {
            desiredAngle -= Math.PI * 2;
        }
        
        distance = desiredAngle - Angle;
        
        return (float)distance;
    }
    
    float GetDesiredVelocity(double radiansToTravel)
    {
        var percentOfVelocity = radiansToTravel / (Math.PI / 2);
        var velocity = percentOfVelocity * m_maxVelocity;
        return (float)velocity;
    }
}

public Program()
{
    // begin update frequency
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    
    var rotors = new List<IMyMotorStator>();
    GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(rotors);
    Rotors = new List<Rotor>();
    foreach (var rotor in rotors)
    {
        if (rotor.CustomData.Contains("no-rotor") == false)
        {
            Rotors.Add(new Rotor(rotor));
        }
    }
        
    var controllers = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);
    Controller = controllers[0];
}

public void Main(string argument, UpdateType updateType)
{
        
    var controllers = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);
    Controller = controllers[0];
    
    bool isUserForce = false;

    //var opposingForce = Controller.GetNaturalGravity();
    var opposingForce = new Vector3D();
    
    if (Controller.DampenersOverride == true)
    {
        var gravityForce = Controller.GetNaturalGravity(); // 60 ticks per second
        var velocityForce = Controller.GetShipVelocities().LinearVelocity;
        
        opposingForce += gravityForce;
        opposingForce += velocityForce / velocityForce.Length() * gravityForce.Length();
        
        opposingForce /= opposingForce.Length();
    }

    var userForce = new Vector3D(0, 0, 0);

    if (Controller.MoveIndicator.Y > 0)
    {
        userForce += Controller.WorldMatrix.Down;
    }
    else if (Controller.MoveIndicator.Y < 0)
    {
        userForce += Controller.WorldMatrix.Up;
    }

    if (Controller.MoveIndicator.Z < 0)
    {
        userForce += Controller.WorldMatrix.Backward;
    }
    else if (Controller.MoveIndicator.Z > 0)
    {
        userForce += Controller.WorldMatrix.Forward;
    }

    if (Controller.MoveIndicator.X > 0)
    {
        userForce += Controller.WorldMatrix.Left;
    }
    else if (Controller.MoveIndicator.X < 0)
    {
        userForce += Controller.WorldMatrix.Right;
    }
    
    isUserForce = userForce.Length() > 0;
    
    if (isUserForce)
    {
        var userForceNormal = userForce / userForce.Length();
        opposingForce -= opposingForce.Dot(userForceNormal) * userForceNormal;
        opposingForce += userForce / 2;
    }
        
    // orient rotors
    foreach (var rotor in Rotors)
    {
        var rotorMatrix = rotor.Stator.WorldMatrix.Forward;
        var angle = Math.Acos(rotorMatrix.Dot(opposingForce) / (rotorMatrix.Length() * opposingForce.Length()));
        var direction =  rotor.Stator.WorldMatrix.Left.Dot(opposingForce);
        if (direction > 0)
        {
            angle = Math.PI * 2 - angle;
        }
        //Echo(angle.ToString());
        rotor.SetDesiredAngle((float)angle);
        rotor.Update();
    }
}
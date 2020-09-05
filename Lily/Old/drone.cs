// NOTE: PLACE ROTORS WITH 180 FACING DOWNWARDS!

List<Rotor> Rotors;
List<IMyThrust> Thrusters;
IMyShipController Controller;
Vector3D previousVelocities;

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
        m_maxVelocity = 60f;
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
        Rotors.Add(new Rotor(rotor));
    }
    
    Thrusters = new List<IMyThrust>();
    GridTerminalSystem.GetBlocksOfType<IMyThrust>(Thrusters);
    
    var controllers = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);
    Controller = controllers[0];
    
    previousVelocities = Controller.GetShipVelocities().LinearVelocity;
}

public void Main(string argument, UpdateType updateType)
{
    var effectiveMass = Controller.CalculateShipMass().TotalMass;
    
    //var opposingForce = Controller.GetTotalGravity() * effectiveMass;
    Echo(Controller.GetNaturalGravity().Length().ToString());
    
    //var opposingForce = new Vector3D(0, 0, 0);
    
    var opposingForce = (previousVelocities - Controller.GetShipVelocities().LinearVelocity) * 30;// * effectiveMass;
    Echo(opposingForce.Length().ToString());
    opposingForce *= effectiveMass;
    
    var userForce = new Vector3D(0, 0, 0);
    
    if (Controller.MoveIndicator.Y > 0)
    {
        userForce += Controller.WorldMatrix.Down * effectiveMass;
    }
    else if (Controller.MoveIndicator.Y < 0)
    {
        userForce += Controller.WorldMatrix.Up * effectiveMass;
    }
    
    if (Controller.MoveIndicator.Z < 0)
    {
        userForce += Controller.WorldMatrix.Backward * effectiveMass;
    }
    else if (Controller.MoveIndicator.Z > 0)
    {
        userForce += Controller.WorldMatrix.Forward * effectiveMass;
    }
    
    if (Controller.MoveIndicator.X > 0)
    {
        userForce += Controller.WorldMatrix.Left * effectiveMass;
    }
    else if (Controller.MoveIndicator.X < 0)
    {
        userForce += Controller.WorldMatrix.Right * effectiveMass;
    }
    
    opposingForce -= userForce;
    
    var opposingForceDirection = opposingForce / opposingForce.Length();
    
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
    
    // figure out percentage to put thrusters at
    double maxEffectiveThrust = 0;
    foreach (var thruster in Thrusters)
    {
        var dotProduct = opposingForceDirection.Dot(thruster.WorldMatrix.Forward);
        if (dotProduct > 0)
        {
            maxEffectiveThrust += thruster.MaxEffectiveThrust * dotProduct;
        }
        //Echo(opposingForceDirection.Dot(thruster.WorldMatrix.Forward).ToString());
    }
    
    float thrusterPercent = (float)(opposingForce.Length() / maxEffectiveThrust);
    if (thrusterPercent > 1)
    {
        thrusterPercent = 1;
    }
    Echo(thrusterPercent.ToString());
    
    foreach (var thruster in Thrusters)
    {
        var dotProduct = opposingForceDirection.Dot(thruster.WorldMatrix.Forward);
        if (dotProduct > 0)
        {
            thruster.ThrustOverridePercentage = thrusterPercent * (float)dotProduct;
        }
        else
        {
            thruster.ThrustOverridePercentage = 0;
        }
    }
    
    previousVelocities = Controller.GetShipVelocities().LinearVelocity;
}
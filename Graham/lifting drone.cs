const float TURRET_MAX_ROT_SPEED = 10f;
const float TURRET_MAXIMUM_INPUT = 10f;
const float GYRO_STRENGTH = 10;
const double ANGLE_TOLERANCE = 0.001;

List<Turret> Turrets;
List<IMyGyro> gyroscopes;
IMyShipController mainController;
Vector3D targetForward;
double rotationAngle;

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
        if (m_pitch != null)
        {
            m_pitchInverted = m_pitch.CustomData.Contains("inverted");
        }
        if (m_yaw != null)
        {
            m_yawInverted = m_yaw.CustomData.Contains("inverted");
        }
        m_rotSpeed = rotSpeed;
    }
    
    public void Move(Vector2 angles)
    {
        float pitchSpeed = 0;
        float yawSpeed = 0;
        
        if (m_pitch != null)
        {
            pitchSpeed = m_rotSpeed * (angles.X / TURRET_MAXIMUM_INPUT);
            if (m_pitchInverted)
                pitchSpeed = -pitchSpeed;
            m_pitch.SetValueFloat("Velocity", pitchSpeed);
        }
        
        if (m_yaw != null)
        {
            yawSpeed = m_rotSpeed * (angles.Y / TURRET_MAXIMUM_INPUT);
            if (m_yawInverted)
                yawSpeed = -yawSpeed;
            m_yaw.SetValueFloat("Velocity", yawSpeed);
        }
    }
    
    public float Yaw
    {
        get
        {
            if (m_yaw != null)
            {
                return m_yaw.Angle;
            }
            else
            {
                return 0;
            }
        }
    }
    
    public float Pitch
    {
        get
        {
            if (m_pitch != null)
            {
                return m_pitch.Angle;
            }
            else
            {
                return 0;
            }
        }
    }
}

public Program() {
	
	// The constructor, called only once every session and
	// always before any other method is called. Use it to
	// initialize your script.
	//	
	// The constructor is optional and can be removed if not
	// needed.
	
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
    
    Turrets = new List<Turret>();
	
	List<IMyShipController> allControllers = new List<IMyShipController>();
	GridTerminalSystem.GetBlocksOfType<IMyShipController>(allControllers);
	foreach(IMyShipController controller in allControllers) {
		if(controller is IMyRemoteControl && (controller as IMyRemoteControl).GetValueBool("MainRemoteControl")) {
			mainController = controller;
			Echo("Main controller is " + mainController.CustomName);
			break;
		}
		if(controller is IMyCockpit && (controller as IMyCockpit).GetValueBool("MainCockpit")) {
			mainController = controller;
			Echo("Main cockpit is " + mainController.CustomName);
			break;
		}
	}
	if(mainController == null) {
		mainController = allControllers[0];
	}
	
	gyroscopes = new List<IMyGyro>();
	GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyroscopes);
	
	targetForward = mainController.WorldMatrix.Forward;
}



public void Save() {
	
	// Called when the program needs to save its state. Use
	// this method to save your state to the Storage field
	// or some other means.
	//
	// This method is optional and can be removed if not
	// needed.
}



public void Main(string argument, UpdateType updateType) {
	
	// The main entry point of the script, invoked every time
	// one of the programmable block's Run actions are invoked.
	//
	// The method itself is required, but the argument above
	// can be removed if not needed.
	
	Echo(mainController.Orientation.ToString());
	
	MatrixD worldToLocalOrientation = MatrixD.Invert(mainController.WorldMatrix.GetOrientation());
	
	Vector3D gravity = Vector3D.Normalize(mainController.GetNaturalGravity());
	Vector3D shipDown = mainController.WorldMatrix.Down;
	
	Echo("Target forward: " + vectorString(targetForward));
	Vector3D idealForward = gravity.Cross(targetForward.Cross(gravity));
	MatrixD idealWorldMatrix = MatrixD.CreateWorld(Vector3D.Zero, idealForward, -gravity);
	
	setGyroscopes(idealWorldMatrix);
    
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
        GatherTurrets(Me, GridTerminalSystem);
        if (controller != null)
        {
            foreach (var turret in Turrets)
            {
                if (rotationAngle > 0.01 && controller.RotationIndicator.X == 0 && controller.RotationIndicator.Y == 0)
                {
                    if (turret.Yaw < Math.PI)
                    {
                        turret.Move(new Vector2(0, (float)(-Math.Min(turret.Yaw / Math.PI * TURRET_MAXIMUM_INPUT, TURRET_MAXIMUM_INPUT))));
                    }
                    else
                    {
                        turret.Move(new Vector2(0, (float)(Math.Min((Math.PI * 2 - turret.Yaw) / Math.PI * TURRET_MAXIMUM_INPUT, TURRET_MAXIMUM_INPUT))));
                    }
                }
                else
                {
                    turret.Move(controller.RotationIndicator);
                }
            }
        }
        else
        {
            foreach (var turret in Turrets)
            {
                turret.Move(new Vector2(0, 0));
            }
        }
    }
    else if (argument == "align")
    {
        var cameras = new List<IMyCameraBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>(cameras);
        if (cameras.Count() > 0)
        {
            targetForward = cameras[0].WorldMatrix.Forward;
        }
    }
}

public string vectorString(Vector3D vector) {
	return "[" + vector.X + ", " + vector.Y + ", " + vector.Z + "]";
}

public void setGyroscopes(MatrixD targetOrientation)
{
	Echo("Setting gyroscopes");		
	MatrixD currentOrientation = mainController.WorldMatrix.GetOrientation();
	
	MatrixD rotationMatrix =  targetOrientation * MatrixD.Transpose(currentOrientation);
	rotationAngle = Math.Acos((rotationMatrix.M11 + rotationMatrix.M22 + rotationMatrix.M33 - 1) / 2);
	
	Echo("Angle error: " + rotationAngle + "\n");
	Vector3D rotationAxis = new Vector3D(rotationMatrix.M32 - rotationMatrix.M23, rotationMatrix.M13 - rotationMatrix.M31, rotationMatrix.M21 - rotationMatrix.M12);
	//Echo("Rotation axis: " + vectorString(rotationAxis) + "\n");
	if(rotationAngle > ANGLE_TOLERANCE) {
		rotationAxis *= rotationAngle / (2 * Math.Sin(rotationAngle));
	} else {
		rotationAxis = Vector3D.Zero;
	}
	
	foreach(IMyGyro gyroscope in gyroscopes) {
		Matrix gyroOrientation;
		gyroscope.Orientation.GetMatrix(out gyroOrientation);
		
		Vector3D gyroRotationAxis = Vector3D.Transform(rotationAxis, gyroOrientation);
		
		gyroscope.SetValueFloat("Pitch", (float)(-GYRO_STRENGTH * gyroRotationAxis.X)); //Pitch appears to be somehow reversed?
		gyroscope.SetValueFloat("Yaw", (float)(GYRO_STRENGTH * gyroRotationAxis.Y));
		gyroscope.SetValueFloat("Roll", (float)(GYRO_STRENGTH * gyroRotationAxis.Z));
	}
}

public void GatherTurrets(IMyCubeBlock reference, IMyGridTerminalSystem grid)
{
    // clear turrets
    Turrets.Clear();
    
    var rotors = new List<IMyMotorStator>(); 
    grid.GetBlocksOfType<IMyMotorStator>(rotors);
    if (rotors.First().CubeGrid == reference.CubeGrid)
        Turrets.Add(new Turret("turret1", rotors[1], rotors[0], TURRET_MAX_ROT_SPEED));
    else
        Turrets.Add(new Turret("turret1", rotors[0], rotors[1], TURRET_MAX_ROT_SPEED));
}
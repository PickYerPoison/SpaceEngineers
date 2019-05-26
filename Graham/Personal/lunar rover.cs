const float TURRET_MAX_ROT_SPEED = 10f;
const float TURRET_MAXIMUM_INPUT = 10f;
Vector3D BASE_ANTENNA_POSITION = new Vector3D(17818, 130364, -106448);
int flashCounter = 0;

const double MAX_HEIGHT = 0.26f;
const double MIN_HEIGHT = -0.32f;
const float HEIGHT_DELTA = 0.01f;

IMyMotorSuspension WheelFrontLeft;
IMyMotorSuspension WheelFrontRight;
IMyMotorSuspension WheelRearLeft;
IMyMotorSuspension WheelRearRight;

double desiredFrontLeft;
double desiredFrontRight;
double desiredRearLeft;
double desiredRearRight;

public Program() {
    
    // The constructor, called only once every session and
    // always before any other method is called. Use it to
    // initialize your script.
    //    
    // The constructor is optional and can be removed if not
    // needed.
    
    Runtime.UpdateFrequency = UpdateFrequency.Update1;

    desiredFrontLeft = MIN_HEIGHT;
    desiredFrontRight = MIN_HEIGHT;
    desiredRearLeft = MIN_HEIGHT;
    desiredRearRight = MIN_HEIGHT;
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
    
    var controllers = new List<IMyShipController>();
    IMyShipController controller = null;
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);
    if (controllers.Count() > 0)
    {
        controller = controllers[0];
    }
    
    if (argument == "")
    {
        if (controller != null)
        {
            var rotors = new List<IMyMotorStator>(); 
            GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CustomData.Contains("turret") && (x.CustomData.Contains("pitch") || (x.CustomData.Contains("yaw"))));
            
            if (rotors.Count() > 0)
            {
                if (controller.RotationIndicator.X != 0 || controller.RotationIndicator.Y != 0)
                {
                    foreach (var rotor in rotors)
                    {
                        bool isPitch = rotor.CustomData.ToLower().Contains("pitch");
                        bool inverted = rotor.CustomData.ToLower().Contains("inverted");
                        float angle = 0f;
                        if (isPitch == true)
                        {
                            angle = controller.RotationIndicator.X;
                        }
                        else
                        {
                            angle = controller.RotationIndicator.Y;
                        }
                            
                        float velocity = TURRET_MAX_ROT_SPEED * (angle / TURRET_MAXIMUM_INPUT);
                        if (inverted == true)
                            velocity = -velocity;
                        rotor.SetValueFloat("Velocity", velocity);
                    }
                }
                else
                {
                    foreach (var rotor in rotors)
                    {
                        rotor.SetValueFloat("Velocity", 0f);
                    }
                }
                
                var lights = new List<IMyLightingBlock>(); 
                GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights);
                if (controller.IsUnderControl == true)
                {
                    var primaryRotor = rotors[0];
                    var rotorAngle = primaryRotor.Angle;
                    // rotor is backwards...
                    if (rotorAngle > Math.PI)
                    {
                        rotorAngle = (float)(-(Math.PI * 2 - rotorAngle));
                    }
                    
                    foreach (var light in lights)
                    {
                        var localTargetPosition = Vector3D.Transform(light.GetPosition(), MatrixD.Invert(primaryRotor.WorldMatrix));
                        double angle = Math.Round(Math.Atan2(-localTargetPosition.X, -localTargetPosition.Z), 2);
                        if (angle > 0)
                        {
                            angle = (float)(Math.PI - angle);
                        }
                        else
                        {
                            angle = (float)(-Math.PI - angle);
                        }
                        
                        var cutoffAngle = Math.PI * 2/3;
                        var distDiff = Math.Abs(angle - rotorAngle);
                        if (distDiff <= cutoffAngle)
                        {
                            light.Enabled = true;
                            light.Intensity = (float)(9.5f * (1 - distDiff / cutoffAngle) + 0.5f);
                        }
                        else
                        {
                            light.Enabled = false;
                            light.Intensity = 0.5f;
                        }
                    }
                    
                    WheelFrontLeft = null;
                    WheelFrontRight = null;
                    WheelRearLeft = null;
                    WheelRearRight = null;
                    
                    var wheels = new List<IMyMotorSuspension>();
                    GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(wheels);
                    
                    foreach (var wheel in wheels)
                    {
                        var localWheelPosition = Vector3D.Transform(wheel.GetPosition(), MatrixD.Invert(MatrixD.CreateWorld(primaryRotor.GetPosition(), controller.WorldMatrix.Forward, controller.WorldMatrix.Up))); 
                        double angle = Math.Atan2(-localWheelPosition.X, -localWheelPosition.Z); 
           
                        if (angle > 0)
                        {
                            if (angle < Math.PI / 2)
                            {
                                WheelFrontLeft = wheel;
                            }
                            else
                            {
                                WheelRearLeft = wheel;
                            }
                        }
                        else
                        {
                            if (angle > -Math.PI / 2)
                            {
                                WheelFrontRight = wheel;
                            }
                            else
                            {
                                WheelRearRight = wheel;
                            }
                        }
                    }
                    
                    var cameras = new List<IMyCameraBlock>();
                    GridTerminalSystem.GetBlocksOfType<IMyCameraBlock>(cameras);
                    if ((controller.HandBrake == true || controller.MoveIndicator.Y > 0) && cameras.Count() > 0)
                    {
                        if (controller.RotationIndicator.Y != 0)
                        {
                            var localTargetPosition = Vector3D.Transform(cameras[0].GetPosition(), MatrixD.Invert(primaryRotor.WorldMatrix));
                            var heightModify = new Vector2D(localTargetPosition.Z, localTargetPosition.X);
                            heightModify.Normalize();
                            heightModify *= controller.RotationIndicator.X / TURRET_MAXIMUM_INPUT;
                            
                            // pitch
                            desiredFrontLeft += HEIGHT_DELTA * heightModify.X;
                            desiredFrontRight += HEIGHT_DELTA * heightModify.X;
                            desiredRearLeft += HEIGHT_DELTA * -heightModify.X;
                            desiredRearRight += HEIGHT_DELTA * -heightModify.X;
                            
                            // roll
                            desiredFrontLeft += HEIGHT_DELTA * heightModify.Y;
                            desiredRearLeft += HEIGHT_DELTA * heightModify.Y;
                            desiredFrontRight += HEIGHT_DELTA * -heightModify.Y;
                            desiredRearRight += HEIGHT_DELTA * -heightModify.Y;
                        }
                    }
                    else if (controller.MoveIndicator.Y < 0)
                    {
                        desiredFrontRight = (MIN_HEIGHT + MAX_HEIGHT) / 2;
                        desiredRearRight = (MIN_HEIGHT + MAX_HEIGHT) / 2;
                        desiredFrontLeft = (MIN_HEIGHT + MAX_HEIGHT) / 2;
                        desiredRearLeft = (MIN_HEIGHT + MAX_HEIGHT) / 2;
                    }
                    else
                    {
                        desiredFrontRight = MIN_HEIGHT;
                        desiredRearRight = MIN_HEIGHT;
                        desiredFrontLeft = MIN_HEIGHT;
                        desiredRearLeft = MIN_HEIGHT;
                    }
                    
                    desiredFrontLeft = Math.Max(Math.Min(desiredFrontLeft, MAX_HEIGHT), MIN_HEIGHT);
                    desiredFrontRight = Math.Max(Math.Min(desiredFrontRight, MAX_HEIGHT), MIN_HEIGHT);
                    desiredRearLeft = Math.Max(Math.Min(desiredRearLeft, MAX_HEIGHT), MIN_HEIGHT);
                    desiredRearRight = Math.Max(Math.Min(desiredRearRight, MAX_HEIGHT), MIN_HEIGHT);
                    
                    UpdateSuspension(WheelFrontLeft, desiredFrontLeft);
                    UpdateSuspension(WheelFrontRight, desiredFrontRight);
                    UpdateSuspension(WheelRearLeft, desiredRearLeft);
                    UpdateSuspension(WheelRearRight, desiredRearRight);
                }
                else
                {
                    foreach (var light in lights)
                    {
                        light.Enabled = false;
                    }
                    
                    desiredFrontRight = MIN_HEIGHT;
                    desiredRearRight = MIN_HEIGHT;
                    desiredFrontLeft = MIN_HEIGHT;
                    desiredRearLeft = MIN_HEIGHT;
                }
            }
        }
        
        var textLine1 = "";
        var textLine2 = "";
		bool flashLine2 = false;
        bool showLine2 = true;
        
        var antennas = new List<IMyRadioAntenna>();
        GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(antennas);
        if (antennas.Count() > 0)
        {
            int antennaDistance = (int)(Math.Round(Vector3D.Distance(antennas[0].GetPosition(), BASE_ANTENNA_POSITION), 0));
            textLine1 = antennaDistance.ToString() + "m/" + ((int)(Math.Round(antennas[0].Radius, 0))).ToString() + "m";
        }
        
		var blocks = new List<IMyTerminalBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, (IMyTerminalBlock x) => x.CustomData.Contains("minor") == true || x.CustomData.Contains("critical") == true);
        textLine2 = "NOMINAL STATE";
		foreach (var block in blocks)
		{
			if (block.IsFunctional == false)
			{
                if (block.CustomData.Contains("minor"))
                {
                    flashLine2 = false;
                    textLine2 = "DAMAGE";
                }
                else
                {
                    flashLine2 = true;
                    textLine2 = "CRITICAL DAMAGE";
                    break;
                }
			}
		}
        
        if (flashLine2 == true)
        {
			flashCounter++;
			if (flashCounter > 100)
			{
				flashCounter = 0;
			}
            
            if (flashCounter < 50)
            {
                showLine2 = true;
            }
            else
            {
                showLine2 = false;
            }
        }
		
		if (showLine2 == false)
		{
			textLine2 = "";
		}
		
        var screens = new List<IMyTextPanel>();
        GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(screens);
        foreach (var screen in screens)
        {
            screen.WritePublicText(textLine1 + "\n" + textLine2);
        }
    }
    else if (argument == "reset")
    {
        BASE_ANTENNA_POSITION = Me.GetPosition();
    }
}


void UpdateSuspension(IMyMotorSuspension suspension, double desiredHeight)
{
    if (suspension != null)
    {
        if (suspension.Height < desiredHeight)
        {
            suspension.SetValueFloat("Height", suspension.Height + HEIGHT_DELTA);
        }
        else if (suspension.Height > desiredHeight)
        {
            suspension.SetValueFloat("Height", suspension.Height - HEIGHT_DELTA);
        }
    }
}
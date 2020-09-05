float mod = 1f;
int countdown = 0;

public void Main(string argument)
{
    if (countdown > 0)
        countdown--;
    
    // find if there are any ship controllers (cockpits, remote control blocks, etc)
    var controllers = new List<IMyShipController>();
    IMyShipController controller = null;
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);
    if (controllers.Count() > 0)
    {
        controller = controllers[0];
    }
    
    var rotors = new List<IMyMotorStator>(); 
    GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(rotors);
    
    var gyroscopes = new List<IMyGyro>();
	GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyroscopes);
    
    if (controller == null)
    {
        foreach (var rotor in rotors)
        {
            rotor.SetValueFloat("Velocity", 0f);
        }
        
        foreach (var gyroscope in gyroscopes)
        {
            gyroscope.Pitch = 0;
            gyroscope.Yaw = 0;
            gyroscope.Roll = 0;
        }
    }
    else
    {
        foreach (var rotor in rotors)
        {
            rotor.SetValueFloat("Velocity", controller.MoveIndicator.Z * 30);
        }
        
        if (countdown == 0)
        {
            countdown = 5;
            MatrixD worldToLocalOrientation = MatrixD.Invert(controller.WorldMatrix.GetOrientation());
            Vector3D gravity = Vector3D.Normalize(controller.GetArtificialGravity());
            Vector3D localTarget = Vector3D.Transform(gravity, worldToLocalOrientation);
            // Echo(controller.WorldMatrix.GetOrientation().Down.ToString());
            // Echo(gravity.ToString());
            // Echo(localTarget.ToString());
            
            Matrix controllerOrientation;
            controller.Orientation.GetMatrix(out controllerOrientation);
            
            foreach(var gyroscope in gyroscopes)
            {
                Matrix gyroOrientation;
                gyroscope.Orientation.GetMatrix(out gyroOrientation);
                Vector3D gyroDown = Vector3D.Transform(Vector3D.Down, MatrixD.Transpose(gyroOrientation) * MatrixD.Transpose(controllerOrientation));
                Vector3D gyroTarget = Vector3D.Transform(localTarget, MatrixD.Transpose(gyroOrientation) * MatrixD.Transpose(controllerOrientation));
                Vector3D crossProduct = gyroDown.Cross(gyroTarget);
                // Echo(gyroscope.CustomName + " error: " + crossProduct.Length());
                
                gyroscope.Pitch = (float)(crossProduct.Y * -1 * mod);// + Math.Max(Math.Min(-controller.RotationIndicator.X, 1), -1);
                gyroscope.Yaw = (float)(crossProduct.Z * -1 * mod);
                //gyroscope.Roll = (float)(crossProduct.X * 1 * mod);
                gyroscope.Roll = Math.Max(Math.Min(controller.RotationIndicator.Y, 1), -1);
                
                Echo(gyroscope.Pitch.ToString());
                Echo(gyroscope.Yaw.ToString());
                Echo(gyroscope.Roll.ToString());
            }
        }
    }
}
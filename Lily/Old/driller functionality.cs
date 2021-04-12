const float DESIRED_VELOCITY = 1.0f;
const float OVERRIDE_DELTA = 0.5f;

bool descend = false;

List<IMyThrust> UpThrusters = new List<IMyThrust>();
IMyShipController Controller = null;

public Program()
{
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
    
    var thrustersGroup = GridTerminalSystem.GetBlockGroupWithName("Thrusters (Up)");
    thrustersGroup.GetBlocksOfType<IMyThrust>(UpThrusters);
    
    var controllers = new List<IMyShipController>();
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);
    if (controllers.Count() > 0)
    {
        Controller = controllers.First();
    }
    else
    {
        Echo("Could not find controller!");
    }
}

public void Main(string argument, UpdateType updateType)
{
    if (argument.ToLower() == "lock" || argument.ToLower() == "unlock" || argument.ToLower() == "toggle lock")
    {
        var wheels = new List<IMyMotorSuspension>();
        GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(wheels);
        foreach (var wheel in wheels)
        {
            if (argument.ToLower() == "lock")
            {
                wheel.Friction = 100;
                wheel.Height = -1.5f;
            }
            else if (argument.ToLower() == "unlock")
            {
                wheel.Friction = 0;
                wheel.Height = 0;
            }
            else if (argument.ToLower() == "toggle lock")
            {
                if (wheel.Friction == 0)
                {
                    wheel.Friction = 100;
                    wheel.Height = -1.5f;
                }
                else
                {
                    wheel.Friction = 0;
                    wheel.Height = 0;
                }
            }
        }
    }
    else if (argument.ToLower() == "descend")
    {
        descend = !descend;
        
        if (descend == false)
        {
            foreach (var thruster in UpThrusters)
            {
                thruster.ThrustOverridePercentage = 0;
            }
        }
        
        Echo(descend.ToString());
    }
    else if (argument == "")
    {
        if (Controller != null)
        {
            if (descend == true)
            {
                var upVelocity = Controller.GetShipVelocities().LinearVelocity.Y;
                Echo(upVelocity.ToString());
                float overrideAmount = 0.01f;
                if (upVelocity > DESIRED_VELOCITY)
                {
                    overrideAmount = 1.0f;
                }
                
                Echo(overrideAmount.ToString());
                Echo(UpThrusters.Count().ToString());
                foreach (var thruster in UpThrusters)
                {
                    thruster.ThrustOverridePercentage = overrideAmount;
                }
            }
        }
    }
}
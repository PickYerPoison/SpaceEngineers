// Maximum acceleration before cutoff in m/s^2
double MAXIMUM_ACCELERATION = 0.5;

// Maximum velocity before cutoff in m/s
double MAXIMUM_VELOCITY = 10;

List<IMyThrust> Thrusters = new List<IMyThrust>();
IMyShipController Controller = null;
IMyProgrammableBlock AnimationTerminal = null;
double previousVelocity;

public Program()
{
    // begin update frequency
    Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10;
    
    previousVelocity = 0;
}

public void Main(string argument, UpdateType updateSource)
{
    if ((updateSource & UpdateType.Update10) != 0)
    {
        Thrusters.Clear();
        Controller = null;
        AnimationTerminal = null;
        
        GridTerminalSystem.GetBlocksOfType<IMyThrust>(Thrusters);
        
        var terminals = new List<IMyProgrammableBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyProgrammableBlock>(terminals, (IMyProgrammableBlock x) => x.CustomName.ToLower().Contains("animation"));
        if (terminals.Count() > 0)
        {
            AnimationTerminal = terminals[0];
        }
        
        var controllers = new List<IMyShipController>();
        GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers, (IMyShipController x) => x.CustomData.ToLower().Contains("primary"));
        if (controllers.Count() > 0)
        {
            Controller = controllers[0];
        }
    }
    else if ((updateSource & UpdateType.Update1) != 0)
    {
        string animationToUse = "";
        
        bool resetThrusters = true;
        
        if (Controller != null)
        {
            var currentVelocity = Controller.GetShipSpeed();
            
            if (currentVelocity > 0.5)
            {
               animationToUse = "fly";
            }
            else 
            {
                animationToUse = "idle";
            }
            
            if (Controller.IsUnderControl == true)
            {
                var acceleration = currentVelocity - previousVelocity;
                previousVelocity = currentVelocity;
                
                if (Controller.MoveIndicator.Z < 0)
                {
                    if (acceleration < MAXIMUM_ACCELERATION && currentVelocity < MAXIMUM_VELOCITY)
                    {
                        resetThrusters = false;
                        
                        var totalMass = Controller.CalculateShipMass().PhysicalMass;
                        var accelerationToGive = MAXIMUM_ACCELERATION - acceleration;
                        var thrustToGive = accelerationToGive * totalMass;
                        
                        foreach (var thruster in Thrusters)
                        {
                            var dotProduct = Controller.WorldMatrix.Backward.Dot(thruster.WorldMatrix.Forward);
                            if (dotProduct > 0)
                            {
                                thruster.ThrustOverride = (float)thrustToGive;
                            }
                            else
                            {
                                thruster.ThrustOverride = 0;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            animationToUse = "sit";
        }
        
        var wheels = new List<IMyMotorSuspension>();
        GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(wheels);
        
        if (wheels.Count() != 8)
        {
            animationToUse = "sit";
        }
        
        if (AnimationTerminal != null && animationToUse != "")
        {
            AnimationTerminal.TryRun(animationToUse);
        }
    
        if (resetThrusters == true)
        {
            foreach (var thruster in Thrusters)
            {
                thruster.ThrustOverride = 0.0001f;
            }
        }
    }
}
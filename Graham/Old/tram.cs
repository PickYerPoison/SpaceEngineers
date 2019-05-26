int situation;  // -2 and 2 are stations. -1 and 1 mean in transit from station. 0 means stopped in the middle.
IMyShipConnector ConnectorA, ConnectorB;
List<IMyThrust> ThrustersA, ThrustersB;
IMySensorBlock Sensor;
 
public Program()
{
    // get the sensor
    var sensors = new List<IMySensorBlock>(2);
    GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors, (IMySensorBlock x) => (x.CubeGrid == this.CubeGrid));
    Sensor = sensors[0];
     
    // get the connectors
    var connectors = new List<IMyShipConnector>(2);
    GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(connectors, (IMyShipConnector x) => (x.CubeGrid == this.CubeGrid));
    ConnectorA = connectors[0];
    ConnectorB = connectors[1];
     
    // determine position
    if (ConnectorA.IsLocked())
        situation = -2;
    else if (ConnectorB.IsLocked())
        situation = 2;
    else
        situation = 0;
     
    // get the thrusters
    var thrusters = new List<IMyThrust>(12);
    GridTerminalSystem.GetBlocksOfType<thrusters>(connectors, (IMyThrust x) => (x.CubeGrid == this.CubeGrid));
    ThrustersA = new List<IMyThrust>(6);
    ThrustersB = new List<IMyThrust>(6);
    foreach (var thruster in thrusters)
    {
        if (thruster.Orientation.Forward == ConnectorA.Orientation.Forward)
            ThrustersA.Add(thruster);
        else
            ThrustersB.Add(thruster);
    }
}
 
public void Main(string argument)
{
    if (argument == "go")
    {
        // if at a station, begin transit to the other station
        // if in transit, reverse
        // if stopped somewhere in the middle, go to a station
        switch (situation)
        {
            case -2: situation = -1; break;
            case -1: situation = 1; break;
            case 0: situation = 1; break;
            case 1: situation = -1; break;
            case 2: situation = 1; break;
            default: situation = 0;
        }
    }
    else if (argument == "stop")
    {
        // stop the tram if it's moving
        if (Math.Abs(situation) == 1)
            situation = 0;
    }
     
    // follow situational logic
}
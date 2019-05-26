// shared ini parser instance
MyIni _ini = new MyIni();

Dictionary<IMyAirVent, string> vents;
List<Door> doors;
Dictionary<string, Room> rooms;
Dictionary<string, Airlock> airlocks;
Dictionary<string, Airlock> purgeAirlocks;
List<Airlock> hangars;
IMyGasTank oxygenMeter;

IMyCubeGrid shipGrid;

const int screenWidth = 89;
const int screenHeight = 89;
string outputString;
int refreshProgress;
const int linesPerCycle = 1;

int screenXAxis;
int screenYAxis;
Vector2I minCorner;
Vector2I maxCorner;
int mapAdjustDirection;

//string[,] voronoiMap;
Vector2D outsidePoint;

Dictionary<Door, InteriorDistancePair> nearestInteriorDistances;

IMyTextPanel mapDisplay;
Dictionary<string, char> labelColors;

IMyTextPanel debugDisplay;

DateTime resumeTime;

const double LAG_DELAY = 1;
const double RUN_DELAY = 0.1;

public Program() {
	
	// The constructor, called only once every session and
	// always before any other method is called. Use it to
	// initialize your script. 
	//     
	// The constructor is optional and can be removed if not
	// needed.
	
	Runtime.UpdateFrequency = UpdateFrequency.Update10;
	initialize();
}

private void initialize() {
	vents = new Dictionary<IMyAirVent, string>();
	
	doors = new List<Door>();
	rooms = new Dictionary<String, Room>();
	airlocks = new Dictionary<String, Airlock>();
	purgeAirlocks = new Dictionary<String, Airlock>();
	hangars = new List<Airlock>();
	
	shipGrid = Me.CubeGrid;
	
	List<IMyGasTank> allTanks = new List<IMyGasTank>();
	GridTerminalSystem.GetBlocksOfType<IMyGasTank>(allTanks);
	foreach(IMyGasTank tank in allTanks) {
		if(tank.CubeGrid != shipGrid) {
			continue;
		}
		if(tank.CustomData.ToLower().Contains("meter")) {
			oxygenMeter = tank;
			break;
		}
	}
	if(oxygenMeter == null) {
		foreach(IMyGasTank tank in allTanks) {
			if(!tank.CustomName.ToLower().Contains("hydrogen")) { //Seriously, Space Engineers?
				oxygenMeter = tank;
				Echo("Oxygen meter is " + oxygenMeter.CustomName);
				break;
			}
		}
	}
	
	List<IMyAirVent> allVents = new List<IMyAirVent>();
	GridTerminalSystem.GetBlocksOfType<IMyAirVent>(allVents);
	foreach(IMyAirVent vent in allVents) {
		if(vent.CubeGrid != shipGrid) {
			continue;
		}
        
        // parse ini data
        bool iniParsed = false;
        iniParsed = _ini.TryParse(vent.CustomData);
        
        if (iniParsed == true &&
            _ini.ContainsKey("airlock", "name") == true &&
            _ini.ContainsKey("airlock", "type") == true)
        {
            string name = _ini.Get("airlock", "name").ToString().ToLower();
            string type = _ini.Get("airlock", "type").ToString().ToLower();
			vents[vent] = name;
			Echo(name);
			if(type.Contains("outside")) {
				if(rooms.ContainsKey("outside")) {
					rooms["outside"].addVent(vent);
				} else {
					Outside outside = new Outside(vent);
					rooms["outside"] = outside;
				}
			} else if(type.Contains("airlock") || type.Contains("hangar")) {
				if(rooms.ContainsKey(name)) {
					rooms[name].addVent(vent);
				} else {
					bool hangar = type.Contains("hangar") && !type.Contains("airlock");
					Airlock airlock = new Airlock(name, vent, hangar, oxygenMeter, this);
					rooms[name] = airlock;
					airlocks[name] = airlock;
					if(hangar) {
						hangars.Add(airlock);
					}
				}
			} else if(type.Contains("purge")) {
				if(rooms.ContainsKey(name)) {
					rooms[name].addVent(vent);
				} else {
					Airlock airlock = new Airlock(name, vent, false, oxygenMeter, this);
					rooms[name] = airlock;
					purgeAirlocks[name] = airlock;
				}
			} else {
				if(rooms.ContainsKey(name)) {
					rooms[name].addVent(vent);
				} else {
					Room room = new Room(name, vent, this);
					rooms[name] = room;
				}
			}
		}
	}
	if(!rooms.ContainsKey("outside")) {
		Outside outside = new Outside(null);
		rooms["outside"] = outside;
	}
	Echo("Rooms initialized.");
	List<IMyDoor> allDoors = new List<IMyDoor>();
	GridTerminalSystem.GetBlocksOfType<IMyDoor>(allDoors);
	foreach(IMyDoor door in allDoors) {
		if(door.CubeGrid != shipGrid) {
			continue;
		}
        
        // parse ini data
        bool iniParsed = false;
        iniParsed = _ini.TryParse(door.CustomData);
        
        if (iniParsed == true &&
            _ini.ContainsKey("airlock", "side1") == true &&
            _ini.ContainsKey("airlock", "side2") == true)
        {
            string side1 = _ini.Get("airlock", "side1").ToString().Trim().ToLower();
            string side2 = _ini.Get("airlock", "side2").ToString().Trim().ToLower();
            if (rooms.ContainsKey(side1) == false ||
                rooms.ContainsKey(side2) == false)
            {
				Echo("Invalid room name on door " + door.CustomName);
			}
            else
            {
                Room room1 = rooms[side1];
                Room room2 = rooms[side2];
                Door doorObject = new Door(door, room1, room2, this);
                doors.Add(doorObject);
                room1.addDoor(doorObject);
                room2.addDoor(doorObject);
            }
		}
	}
	Echo("Doors initialized.");
	
	List<IMyTextPanel> allPanels = new List<IMyTextPanel>();
	GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(allPanels);
	foreach(IMyTextPanel panel in allPanels) {
		if(panel.CubeGrid != shipGrid) {
			continue;
		}
		if(panel.CustomData.Contains("airlock map")) {
			mapDisplay = panel;
		} else if(panel.CustomData.Contains("airlock debug")) {
			debugDisplay = panel;
			debugDisplay.WriteText("");
		}
	}
	
	outputString = "";
	refreshProgress = 0;
	outsidePoint = new Vector2D(1000000, 1000000);
	
	Random random = new Random();
	labelColors = new Dictionary<string, char>();
	foreach(string roomName in rooms.Keys) {
		labelColors[roomName] = rgb((byte)random.Next(8), (byte)random.Next(8), (byte)random.Next(8));
		Echo("Picked color for " + roomName);
	}
	
	screenXAxis = 0;
	screenYAxis = 2;
	mapAdjustDirection = 1;
	
	resetMapZoom();
	
	resumeTime = DateTime.Now;
}

public void Save() {
	
	// Called when the program needs to save its state. Use
	// this method to save your state to the Storage field
	// or some other means. 
	// 
	// This method is optional and can be removed if not
	// needed.
	
}



public void Main(string argument) {
	
	// The main entry point of the script, invoked every time
	// one of the programmable block's Run actions are invoked.
	// 
	// The method itself is required, but the argument above
	// can be removed if not needed.
	
	
	argument = argument.ToLower();
	if(argument == "" || argument == "tick") {
		if(DateTime.Now <= resumeTime) {
			return;
		}
		if(oxygenMeter != null && oxygenMeter.FilledRatio == 1.0) {
			int depressurizing = 0;
			int depressurized = 0;
			foreach(Airlock airlock in airlocks.Values) {
				if(airlock.state == Airlock.State.depressurizing) {
					depressurizing++;
				} else if(airlock.state == Airlock.State.depressurized) {
					depressurized++;
				}
			}
			if(depressurizing > 0) {
				debugPrint("Tanks full! Venting air.");
				if(rooms["outside"].oxygen() > 0) {
					//debugPrint("Opening to planet's atmosphere.");
					foreach(Airlock airlock in airlocks.Values) {
						if(airlock.state == Airlock.State.depressurizing) {
							debugPrint("Overriding " + airlock.name + " as there is no point reclaiming air.");
							airlock.overrideOxygen();
						}
					}
				} else if(purgeAirlocks.Count > 0) {
					//debugPrint("Activating purge airlock.");
					foreach(Airlock purge in purgeAirlocks.Values) {
						if(!purge.purging) {
							debugPrint("Purging " + purge.name + " to make room in tanks.");
							purge.purge();
						}
					}
				} else if(airlocks.Count - hangars.Count > depressurizing) {
					//debugPrint("Utilizing extra airlocks.");
					if(depressurized > 0) {
						foreach(Airlock airlock in airlocks.Values) {
							if(airlock.hangar) {
								continue;
							}
							if(airlock.state == Airlock.State.depressurized) {
								debugPrint("Pressurizing " + airlock.name + " to make room in tanks.");
								airlock.pressurize();
							}
						}
					} else {
						foreach(Airlock airlock in airlocks.Values) {
							if(airlock.hangar) {
								continue;
							}
							if(airlock.state == Airlock.State.pressurized) {
								debugPrint("Purging " + airlock.name + " to make room in tanks.");
								airlock.purge();
							}
						}
					}
				} else {
					//debugPrint("Overriding door.");
					foreach(Airlock airlock in airlocks.Values) {
						if(airlock.state == Airlock.State.depressurizing) {
							airlock.overrideOxygen();
						}
					}
				}
			}
		}
		
		foreach(Door door in doors) {
			door.tick();
		}
		foreach(Airlock airlock in airlocks.Values) {
			airlock.tick();
		}
		foreach(Airlock purge in purgeAirlocks.Values) {
			purge.tick();
		}
		
		if(mapDisplay != null) {
			drawMap();
		}
		
		resumeTime = DateTime.Now.AddSeconds(RUN_DELAY);
	} else if(argument == "reset") {
		initialize();
	} else if(argument.StartsWith("pressurize") || argument.StartsWith("depressurize") || argument.StartsWith("override")) {
		debugReset();
		string[] arguments = argument.Split(new char[] {' '}, 2);
		if(arguments.Length == 2) {
			string airlockName = arguments[1];
			if(!airlocks.ContainsKey(airlockName)) {
				Echo("Airlock not found.");
				return;
			}
			
			Airlock airlock = airlocks[airlockName];
			string action = arguments[0];
			if(action == "pressurize") {
				airlock.pressurize();
			} else if(action == "depressurize") {
				airlock.depressurize();
			} else if(action == "override") {
				airlock.depressurize();
				airlock.overrideOxygen();
			}
		} else {
			Echo("Invalid argument.");
		}
	} else if(argument.StartsWith("map")) {
		string[] arguments = argument.Split(new char[] {' '});
		if(arguments.Length == 2) {
			if(arguments[1] == "zoom") {
				minCorner += Vector2I.One * mapAdjustDirection;
				if(minCorner.X > maxCorner.X || minCorner.Y > maxCorner.Y) {
					minCorner = maxCorner;
				}
			} else if(arguments[1] == "x") {
				minCorner += Vector2I.UnitX * mapAdjustDirection;
				maxCorner += Vector2I.UnitX * mapAdjustDirection;
			} else if(arguments[1] == "y") {
				minCorner += Vector2I.UnitY * mapAdjustDirection;
				maxCorner += Vector2I.UnitY * mapAdjustDirection;
			} else if(arguments[1] == "invert") {
				mapAdjustDirection *= -1;
			}
		} else if(arguments.Length == 4 && arguments[1] == "axis") {
			if(arguments[2] == "x") screenXAxis = 0;
			else if(arguments[2] == "y") screenXAxis = 1;
			else if(arguments[2] == "z") screenXAxis = 2;
			if(arguments[3] == "x") screenYAxis = 0;
			else if(arguments[3] == "y") screenYAxis = 1;
			else if(arguments[3] == "z") screenYAxis = 2;
			resetMapZoom();
		}
	} else {
		Echo("Invalid argument.");
	}
}

public void debugReset() {
	if(debugDisplay == null) {
		return;
	} else {
		debugDisplay.WriteText("");
	}
}

public void debugPrint(string text) {
	if(debugDisplay == null) {
		return;
	} else {
		debugDisplay.WriteText(text + "\n", true);
	}
}

public class Door {
	public IMyDoor door {get;}
	public Room room1 {get; private set;}
	public Room room2 {get; private set;}
	Program debug;
	
	bool waitingToOpen;
	bool overrideOxygen;
	DateTime resumeTime;
	
	public Door(IMyDoor door, Room room1, Room room2, Program debug = null) {
		this.door = door;
		this.room1 = room1;
		this.room2 = room2;
		this.debug = debug;
		
		waitingToOpen = false;
		overrideOxygen = false;
		resumeTime = DateTime.Now;
	}
	
	public void tick() {
		if(waitingToOpen) {
			debug.Echo("Door attempting to open!");
			room1.tick();
			room2.tick();
		}
		if(!waitingToOpen) {
			setLock();
		}
		if(waitingToOpen && DateTime.Now < resumeTime) {
			debug.Echo("Waiting to open until " + resumeTime.ToString());
		}
		if(waitingToOpen && DateTime.Now >= resumeTime) {
			if(overrideOxygen || isSafe()) {
				if(isSafe()) {
					debug.debugPrint(door.CustomName + " has opened safely (" + room1.name + " at " + room1.oxygen() + ", " + room2.name + " at " + room2.oxygen() + ").");
				} else {
					debug.debugPrint(door.CustomName + " has opened unsafely.");
				}
				open();
				debug.Echo("Opening door!");
			}
			waitingToOpen = false;
			overrideOxygen = false;
		}
	}
	
	public bool isSafe() {
		return room1.oxygen() == room2.oxygen() || room1 is Outside && room1.oxygen() >= room2.oxygen() || room2 is Outside && room1.oxygen() <= room2.oxygen();
	}
	
	public void setLock() {
		if(isSafe()) {
			door.ApplyAction("OnOff_On");
		} else if(door.OpenRatio == 0) {
			door.ApplyAction("OnOff_Off");
		}
	}
	
	public bool isOpen() {
		return door.Status == DoorStatus.Open;
	}
	
	public void open() {
		debug.debugPrint(door.CustomName + " has opened.");
		door.ApplyAction("Open_On");
	}
	
	public void openOverride() {
		if(!waitingToOpen) {
			debug.debugPrint(door.CustomName + " is preparing to open due to override.");
		}
		overrideOxygen = true;
		openDelay();
	}
	
	public void openSafe() {
		if(isSafe()) {
			if(!waitingToOpen) {
				debug.debugPrint(door.CustomName + " is preparing to open safely.");
			}
			openDelay();
		}
	}
	
	public void openDelay() {
		if(waitingToOpen) {
			return;
		}
		door.ApplyAction("OnOff_On");
		resumeTime = DateTime.Now.AddSeconds(LAG_DELAY);
		waitingToOpen = true;
	}
	
	public void close() {
		door.ApplyAction("Open_Off");
	}
	
	public Room otherRoom(Room thisRoom) {
		if(thisRoom == room1) {
			return room2;
		} else if(thisRoom == room2) {
			return room1;
		} else {
			return null;
		}
	}
}

public class Room {
	public string name {get; private set;}
	protected List<IMyAirVent> roomVents;
	protected List<Door> doors;
	Program debug;
	
	public Room(string name, IMyAirVent vent, Program debug = null) {
		this.name = name;
		roomVents = new List<IMyAirVent>();
		roomVents.Add(vent);
		doors = new List<Door>();
		
		this.debug = debug;
	}
	
	public virtual void tick() {
		if(oxygen() < 1) {
			debug.debugPrint("Oxygen failure in " + name);
		}
	}
	
	public virtual float oxygen() {
		if(roomVents[0].CanPressurize) {
			return 1;
		} else {
			return roomVents[0].GetOxygenLevel();
		}
	}
	
	public void addDoor(Door door) {
		doors.Add(door);
	}
	
	public void addVent(IMyAirVent vent) {
		roomVents.Add(vent);
	}
}

public class Airlock : Room {
	public enum State{depressurized, pressurized, depressurizing, pressurizing}
	public State state {get; private set;}
	public bool hangar {get; private set;}
	IMyGasTank oxygenMeter;
	Program debug;
	
	public bool purging;
	
	public Airlock(string name, IMyAirVent vent, bool hangar = false, IMyGasTank oxygenMeter = null, Program debug = null) : base (name, vent) {
		if(roomVents[0].GetOxygenLevel() == 1) {
			state = State.pressurized;
		} else {
			state = State.depressurized;
		}
		this.hangar = hangar;
		this.oxygenMeter = oxygenMeter;
		this.debug = debug;
		
		purging = false;
	}
	
	public override void tick() {
		debug.Echo(name + " status is: " + state.ToString());
		if((state == State.pressurizing && oxygen() == 1) || (state == State.depressurizing && oxygen() == 0) || (state == State.depressurizing && oxygen() <= debug.rooms["outside"].oxygen())) {
			complete();
		}
		
		if(purging && state == State.depressurizing) {
			overrideOxygen();
		}
	}
	
	public void pressurize() {
		debug.debugPrint(name + " is pressurizing.");
		foreach(Door door in doors) {
			door.close();
		}
		foreach(IMyAirVent vent in roomVents) {
			vent.ApplyAction("Depressurize_Off");
		}
		state = State.pressurizing;
	}
	
	public void depressurize() {
		debug.debugPrint(name + " is depressurizing.");
		foreach(Door door in doors) {
			door.close();
		}
		foreach(IMyAirVent vent in roomVents) {
			vent.ApplyAction("Depressurize_On");
		}
		state = State.depressurizing;
	}
	
	public void purge() {
		foreach(Door door in doors) {
			debug.Echo("PURGE MODE: CLOSING DOOR");
			door.close();
		}
		if(oxygen() < 1) {
			debug.Echo("PURGE MODE: REPRESSURIZING");
			purging = true;
			debug.debugPrint(name + " is pressurizing as part of purging.");
			pressurize();
		} else {
			debug.Echo("PURGE MODE: PURGING");
			purging = true;
			debug.debugPrint(name + " is depressurizing as part of purging.");
			depressurize();
		}
	}
	
	public void complete() {
		if(purging) {
			if(oxygen() == 1) {
				debug.debugPrint(name + " has completed purge pressurization and is now depressurizing.");
				depressurize();
			} else {
				debug.debugPrint(name + " has completed purge depressurization and is now idle.");
				purging = false;
				state = State.depressurized;
				foreach(Door door in doors) {
					door.close();
				}
			}
		} else {
			foreach(Door door in doors) {
				door.openSafe();
			}
			if(oxygen() == 1) {
				debug.debugPrint(name + " has completed pressurization.");
				state = State.pressurized;
			} else {
				debug.debugPrint(name + " has completed depressurization.");
				state = State.depressurized;
			}
		}
	}
	
	public void overrideOxygen() {
		debug.Echo("OVERRIDE ENGAGED");
		foreach(Door door in doors) {
			if(door.isOpen()) {
				return;
			}
		}
		foreach(Door door in doors) {
			Room otherRoom = door.otherRoom(this);
			if(state == State.pressurizing && otherRoom.oxygen() == 1) {
				door.openOverride();
				debug.Echo("FORCING DOOR");
			} else if(state == State.depressurizing && otherRoom.oxygen() < 1) {
				door.openOverride();
				debug.Echo("FORCING DOOR");
			}
		}
	}
	
	public override float oxygen() {
		if(state == State.pressurized || roomVents[0].GetOxygenLevel() == 1) {
			return 1;
		} else if(state == State.depressurized || roomVents[0].GetOxygenLevel() == 0) {
			return 0;
		} else {
			return roomVents[0].GetOxygenLevel();
		}
	}
}

public class Outside : Room {
	public Outside(IMyAirVent vent) : base ("outside", vent) {
	}
	
	public override float oxygen() {
		if(roomVents[0] == null) {
			return 0;
		} else {
			return roomVents[0].GetOxygenLevel();
		}
	}
	
	public override void tick() {
	}
}

//MAP CODE BELOW

public void calculateNearestInteriorDistances() {
	nearestInteriorDistances = new Dictionary<Door, InteriorDistancePair>();
	foreach(Door door in doors) {
		Vector2I doorPosition = flattenToScreenAxis(door.door.Position);
		Vector2D doorPoint = new Vector2D(doorPosition.X, doorPosition.Y);
		double nearestADistance = Double.PositiveInfinity;
		double nearestBDistance = Double.PositiveInfinity;
		foreach(IMyAirVent vent in vents.Keys) {
			Vector2I ventPosition = flattenToScreenAxis(vent.Position);
		Vector2D ventPoint = new Vector2D(ventPosition.X, ventPosition.Y);;
			if(vents[vent] == door.room1.name) {
				double distance = Vector2D.Distance(doorPoint, ventPoint);
				nearestADistance = Math.Min(distance, nearestADistance);
			} else if(vents[vent] == door.room2.name) {
				double distance = Vector2D.Distance(doorPoint, ventPoint);
				nearestBDistance = Math.Min(distance, nearestBDistance);
			}
		}
		if("outside" == door.room1.name) {
			nearestADistance = Vector2D.Distance(doorPoint, outsidePoint);
		} else if("outside" == door.room2.name) {
			nearestBDistance = Vector2D.Distance(doorPoint, outsidePoint);
		}
		nearestInteriorDistances[door] = new InteriorDistancePair(nearestADistance, nearestBDistance);
	}
}

public string getLabel(Vector2D point) {
	IMyAirVent nearestVent = null;
	double nearestVentDistance = Double.PositiveInfinity;
	Door nearestDoor = null;
	double nearestDoorDistance = Double.PositiveInfinity;
	Dictionary<string, double> roomDistances = new Dictionary<string, double>();
	
	foreach(IMyAirVent vent in vents.Keys) {
		Vector2I ventPosition = flattenToScreenAxis(vent.Position);
		Vector2D ventPoint = new Vector2D(ventPosition.X, ventPosition.Y);
		double distance = Vector2D.Distance(point, ventPoint);
		if(distance < nearestVentDistance) {
			nearestVent = vent;
			nearestVentDistance = distance;
		}
		string roomName = vents[vent];
		if(!roomDistances.ContainsKey(roomName) || distance < roomDistances[roomName]) {
			roomDistances[roomName] = distance;
		}
	}
	
	if(!roomDistances.ContainsKey("outside")) {
		double distance = Vector2D.Distance(point, outsidePoint);
		roomDistances["outside"] = distance;
	}
	
	foreach(Door door in doors) {
		Vector2I doorPosition = flattenToScreenAxis(door.door.Position);
		Vector2D doorPoint = new Vector2D(doorPosition.X, doorPosition.Y);
		
		double distance = Vector2D.Distance(point, doorPoint);
		if(distance < nearestDoorDistance) {
			nearestDoor = door;
			nearestDoorDistance = distance;
		}
	}
	
	if(nearestVentDistance < nearestDoorDistance) {
		return vents[nearestVent];
	} else {
		double adjustedDistanceA = roomDistances[nearestDoor.room1.name] / nearestInteriorDistances[nearestDoor].distanceA;
		double adjustedDistanceB = roomDistances[nearestDoor.room2.name] / nearestInteriorDistances[nearestDoor].distanceB;
		if(adjustedDistanceA < adjustedDistanceB) {
			return nearestDoor.room1.name;
		} else {
			return nearestDoor.room2.name;
		}
	}
}

public void resetMapZoom() {
	minCorner = flattenToScreenAxis(shipGrid.Min);
	maxCorner = flattenToScreenAxis(shipGrid.Max);
	int xLength = maxCorner.X - minCorner.X;
	int yLength = maxCorner.Y - minCorner.Y;
	if(xLength > yLength) {
		minCorner -= Vector2I.UnitY * (xLength - yLength) / 2;
		maxCorner += Vector2I.UnitY * (xLength - yLength) / 2;
	} else if(yLength > xLength) {
		minCorner -= Vector2I.UnitX * (yLength - xLength) / 2;
		maxCorner += Vector2I.UnitX * (yLength - xLength) / 2;
	}
}

public Vector2I flattenToScreenAxis(Vector3I point) {
	int[] axes = {point.X, point.Y, point.Z};
	return new Vector2I(axes[screenXAxis], axes[screenYAxis]);
}

public Vector2D screenToGridSpace(int x, int y) {
	int xLength = maxCorner.X - minCorner.X;
	int yLength = maxCorner.Y - minCorner.Y;
	
	double gridX = ((double)x / screenWidth) * xLength + minCorner.X;
	double gridY = ((double)y / screenHeight) * yLength + minCorner.Y;
	return new Vector2D(gridX, gridY);
}

public Vector2I gridToScreenSpace(Vector2I gridSpace) {
	int xLength = maxCorner.X - minCorner.X;
	int yLength = maxCorner.Y - minCorner.Y;
	
	int screenX = (int)((((double)gridSpace.X - minCorner.X) / xLength) * screenWidth);
	int screenY = (int)((((double)gridSpace.Y - minCorner.Y) / yLength) * screenHeight);
	return new Vector2I(screenX, screenY);
}

public void drawMap() {	
	calculateNearestInteriorDistances();
	Echo("Calculated nearest interior distances.");
	
	foreach(IMyAirVent vent in vents.Keys) {
		Vector2I ventPosition = flattenToScreenAxis(vent.Position);
		Vector2D ventPoint = new Vector2D(ventPosition.X, ventPosition.Y);
		Echo(vents[vent] + ": " + ventPoint.ToString());
	}
	
	Echo(refreshProgress.ToString());
	int endLine = Math.Min(screenHeight, refreshProgress + linesPerCycle);
	for(int row = refreshProgress; row < endLine; row++) {
		for(int col = 0; col < screenWidth; col++) {
			Vector2I screenPixel = new Vector2I(col, row);
			Vector2D screenSpace = new Vector2D(screenPixel.X, screenPixel.Y);
			Vector2D gridSpace = screenToGridSpace(col, row);
			
			bool skip = false;
			
			foreach(IMyAirVent vent in vents.Keys) {
				Vector2I ventPixel = gridToScreenSpace(flattenToScreenAxis(vent.Position));
				Vector2D ventPoint = new Vector2D(ventPixel.X, ventPixel.Y);
				double distance = Vector2D.Distance(screenSpace, ventPoint);
				if(distance <= 1) {
					Echo(vents[vent]);
					outputString += rgb((byte)0, (byte)0, (byte)0);
					skip = true;
					break;
				}
			}
			
			if(!skip) {
				Echo(getLabel(gridSpace));
				outputString += labelColors[getLabel(gridSpace)];
			}
		}
		if(row < screenHeight - 1) {
			outputString += '\n';
		}
		refreshProgress = row + 1;
	}
	
	if(refreshProgress == screenHeight) {
		mapDisplay.WriteText(outputString);
		outputString = "";
		refreshProgress = 0;
		//nextRefresh = DateTime.Now.AddSeconds(1);
	}
}

public char rgb(byte r, byte g, byte b) { 
	return (char)(0xe100 + (r << 6) + (g << 3) + b);
}

public struct InteriorDistancePair {
	public double distanceA;
	public double distanceB;
	
	public InteriorDistancePair(double distanceA, double distanceB) {
		this.distanceA = distanceA;
		this.distanceB = distanceB;
	}
}
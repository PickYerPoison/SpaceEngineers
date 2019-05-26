Dictionary<string, List<ContainerRequest>> containerRequests;
Dictionary<IMyTerminalBlock, Dictionary<string, List<ContainerRequest>>> requestsPerContainer;

List<IMyTerminalBlock> containerBlocks;
int containerIndex;

static List<string> knownCategories;

public Program() {

    // The constructor, called only once every session and
    // always before any other method is called. Use it to
    // initialize your script. 
    //     
    // The constructor is optional and can be removed if not
    // needed.
	
	Runtime.UpdateFrequency = UpdateFrequency.Update1;
	
	knownCategories = new List<string>(new string[] {"Ore", "Ingot", "Component"});
	populateContainers();
}

public void populateContainers() {
	containerRequests = new Dictionary<string, List<ContainerRequest>>();
	requestsPerContainer = new Dictionary<IMyTerminalBlock, Dictionary<string, List<ContainerRequest>>>();
	
	List<IMyTerminalBlock> terminalBlocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(terminalBlocks);
	containerBlocks = new List<IMyTerminalBlock>();
	foreach(IMyTerminalBlock terminal in terminalBlocks) {
		if(terminal.InventoryCount > 0 && !(terminal is IMyReactor || terminal is IMyGasGenerator)) {
			containerBlocks.Add(terminal);
		}
	}
	foreach(IMyTerminalBlock container in containerBlocks) {
		if(container.CustomData.Contains("debug")) {
			Echo(container.CustomName);
			for(int i = 0; i < container.InventoryCount; i++) {
				IMyInventory inventory = (container as IMyEntity).GetInventory(i);
                List<MyInventoryItem> itemsInInventory = new List<MyInventoryItem>();
                inventory.GetItems(itemsInInventory);
				foreach(MyInventoryItem item in itemsInInventory) {
					Echo(item.Type.TypeId + " - " + item.Type.SubtypeId + ": " + item.Amount.ToString()); 
				}
			}
		}
		
		if(!container.CustomData.ToLower().Contains("inventory")) {
			continue;
		}
		Echo("Rule found: " + container.CustomData);
		string[] requestLines = container.CustomData.Split(new char[] {'\n'});
		foreach(string line in requestLines) {
			Echo("Line: " + line);
			if(line.ToLower().Contains("inventory")) {
				continue;
			}
			string[] requestParts = line.ToLower().Split(new char[] {' '});
			string type = requestParts[0].ToLower();
			Echo("Type: " + type);
			int amount = 0;
			bool lowPriority = false;
			for(int i = 1; i < requestParts.Length; i++) {
				if(int.TryParse(requestParts[i], out amount)) {
					Echo("Amount: " + requestParts[i]);
				}
				if(requestParts[i].ToLower() == "low") {
					Echo("Priority: low");
					lowPriority = true;
				}
			}
			ContainerRequest request = new ContainerRequest(container, type, amount, lowPriority);
			if(!containerRequests.ContainsKey(type)) {
				containerRequests[type] = new List<ContainerRequest>();
			}
			containerRequests[type].Add(request);
			if(!requestsPerContainer.ContainsKey(container)) {
				requestsPerContainer[container] = new Dictionary<string, List<ContainerRequest>>();
			}
			if(!requestsPerContainer[container].ContainsKey(type)) {
				requestsPerContainer[container][type] = new List<ContainerRequest>();
			}
			requestsPerContainer[container][type].Add(request);
		}
	}
	
	foreach(List<ContainerRequest> requestList in containerRequests.Values) {
		requestList.Sort(compareRequestPriority);
	}
	
	containerIndex = 0;
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

	
	if(argument == "debug") {
		populateContainers();
		foreach(IMyTerminalBlock container in containerBlocks) {
			processInventory(container);
		}
		return;
	} else {
	
		IMyTerminalBlock container = containerBlocks[containerIndex];
		processInventory(container);
		containerIndex = (containerIndex + 1);
		if(containerIndex >= containerBlocks.Count) {
			populateContainers();
		}
	}
}

public void processInventory(IMyTerminalBlock container) {
	Echo("Processing " + container.CustomName + " (" + container.InventoryCount + " inventories)");
	for(int i = 0; i < container.InventoryCount; i++) {
		if(i == 0) {
			if(container is IMyAssembler || (container is IMyRefinery && container.CustomData == "")) {
				continue;
			}
		}
		Echo("Inventory number " + i.ToString());
		IMyInventory inventory = (container as IMyEntity).GetInventory(i);
        List<MyInventoryItem> itemsInInventory = new List<MyInventoryItem>();
        inventory.GetItems(itemsInInventory);
        Echo("Items in inventory: " + itemsInInventory.Count);
		for(int itemIndex = 0; itemIndex < itemsInInventory.Count; itemIndex++) {
			MyInventoryItem item = itemsInInventory[itemIndex];
			string itemName = item.Type.SubtypeId.ToLower();
			string itemCategory = getCategory(item).ToLower();
			List<ContainerRequest> conflictingRequests = new List<ContainerRequest>();
			if(requestsPerContainer.ContainsKey(container) && requestsPerContainer[container].ContainsKey(itemName)) {
				 conflictingRequests.AddRange(requestsPerContainer[container][itemName]);
			}
			if(requestsPerContainer.ContainsKey(container) && requestsPerContainer[container].ContainsKey(itemCategory)) {
				 conflictingRequests.AddRange(requestsPerContainer[container][itemCategory]);
			}
			if(requestsPerContainer.ContainsKey(container) && requestsPerContainer[container].ContainsKey("all")) {
				 conflictingRequests.AddRange(requestsPerContainer[container]["all"]);
			}
			MyDefinitionId definitionID = item.Type;
			List<ContainerRequest> matchingRequests = new List<ContainerRequest>();
			if(containerRequests.ContainsKey(itemName)) {
				matchingRequests.AddRange(containerRequests[itemName]);
			}
			if(containerRequests.ContainsKey(itemCategory)) {
				matchingRequests.AddRange(containerRequests[itemCategory]);
			}
			if(containerRequests.ContainsKey("all")) {
				matchingRequests.AddRange(containerRequests["all"]);
			}
			foreach(ContainerRequest request in matchingRequests) {
				if(container == request.container) {
					continue;
				}
				Echo("Request found to transfer " + request.amount.ToString() + " " + request.itemName);
				bool conflictFound = false;
				foreach(ContainerRequest conflict in conflictingRequests) {
					Echo("Conflict level is " + compareRequestPriority(request, conflict).ToString());
					if(compareRequestPriority(request, conflict) >= 0) {
						Echo("Ignoring request due to conflict.");
						conflictFound = true;
						break;
					}
				}
				if(conflictFound) {
					continue;
				}
				IMyInventory requestInventory = (request.container as IMyEntity).GetInventory(0);
				//if(!inventory.IsConnectedTo(requestInventory)) {
				//	Echo("Inventories not connected.");
					//continue;
				//} else {
				//	Echo("Inventories connected.");
				//}
				VRage.MyFixedPoint amount = (VRage.MyFixedPoint)request.amount;
				if(amount != 0) {
					VRage.MyFixedPoint existingAmount = requestInventory.GetItemAmount(definitionID);
					amount -= existingAmount;
					if(amount <= 0) {
						Echo("Request already satisfied.");
						continue;
					}
					Echo(amount.ToString() + " needed, " + item.Amount.ToString() + " available.");
					amount = VRage.MyFixedPoint.Min(amount, item.Amount);
					Echo("Attempting to transfer " + amount.ToString() + " " + request.itemName + " (" +  itemIndex.ToString() + ")");
					bool result = inventory.TransferItemTo(requestInventory, itemIndex, stackIfPossible : true, amount : amount);
					if(result) {
						Echo("Success!");
					} else {
						Echo("Failure.");
					}
				} else {
					Echo("Attempting to transfer all " + request.itemName + " (" +  itemIndex.ToString() + ")");
					bool result = inventory.TransferItemTo(requestInventory, itemIndex, stackIfPossible : true);
					if(result) {
						Echo("Success!");
					} else {
						Echo("Failure.");
					}
				}
			}
		}
	}
}

public struct ContainerRequest {
	public IMyTerminalBlock container;
	public string itemName;
	public int amount;
	public bool lowPriority;
	//public bool capped;
	
	public ContainerRequest(IMyTerminalBlock container, string itemName, int amount = 0, bool lowPriority = false) {
		this.container = container;
		this.itemName = itemName;
		this.amount = amount;
		this.lowPriority = lowPriority;
	}
}

public static int compareRequestPriority(ContainerRequest request1, ContainerRequest request2) {
	int priority1 = 0;
	int priority2 = 0;
	if(request1.amount != 0) {
		priority1 += 4;
	}
	if(request2.amount != 0) {
		priority2 += 4;
	}
	if(!knownCategories.Contains(request1.itemName)) {
		priority1 += 2;
	}
	if(!knownCategories.Contains(request2.itemName)) {
		priority2 += 2;
	}
	if(request1.itemName != "all") {
		priority1 += 1;
	}
	if(request2.itemName != "all") {
		priority2 += 1;
	}
	
	if(request1.lowPriority) {
		priority1 -= 100;
	}
	if(request2.lowPriority) {
		priority2 -= 100;
	}
	
	//if(priority1 != priority2) {
		return priority2 - priority1;
	//} else {
	//	return request1.amount - request2.amount;
	//}
}

public string getCategory(MyInventoryItem item) {
	return item.Type.TypeId.Split(new char[] {'_'})[1];
}

List<string> validNames = new List<string> {
	"Stone",
	"Iron",
	"Nickel",
	"Cobalt",
	"Magnesium",
	"Silicon",
	"Silver",
	"Gold",
	"Platinum",
	"Uranium",
	"Scrap",
	"Ice",
	
	"Construction",
	"MetalGrid",
	"InteriorPlate",
	"SteelPlate",
	"Girder",
	"SmallTube",
	"LargeTube",
	"Motor",
	"Display",
	"BulletproofGlass",
	"Superconductor",
	"Computer",
	"Reactor",
	"Thrust",
	"GravityGenerator",
	"Medical",
	"RadioCommunication",
	"Detector",
	"Explosives",
	"SolarCell",
	"PowerCell",
	
	"AutomaticRifleItem",
	"PreciseAutomaticRifleItem",
	"RapidFireAutomaticRifleItem",
	"UltimateAutomaticRifleItem",
	"WelderItem",
	"Welder2Item",
	"Welder3Item",
	"Welder4Item",
	"AngleGrinderItem",
	"AngleGrinder2Item",
	"AngleGrinder3Item",
	"AngleGrinder4Item",
	"HandDrillItem",
	"HandDrill2Item",
	"HandDrill3Item",
	"HandDrill4Item",
	"OxygenBottle",
	"HydrogenBottle",
	
	"NATO_5p56x45mm",
	"NATO_25x184mm",
	"Missile200mm"
};

Dictionary<string, string> nameAliases = new Dictionary<string, string> {
	{"EliteAutomaticRifleItem", "UltimateAutomaticRifleItem"},
	{"AutomaticRifle2Item", "PreciseAutomaticRifleItem"},
	{"AutomaticRifle3Item", "PreciseAutomaticRifleItem"},
	{"AutomaticRifle4Item", "PreciseAutomaticRifleItem"},
	{"EnhancedAngleGrinderItem", "AngleGrinder2Item"},
	{"ProficientAngleGrinderItem", "AngleGrinder3Item"},
	{"EliteAngleGrinderItem", "AngleGrinder4Item"},
	{"EnhancedDrillItem", "HandDrill2Item"},
	{"ProficientDrillItem", "HandDrill3Item"},
	{"EliteDrillItem", "HandDrill4Item"},
	{"Magazine", "NATO_5p56x45mm"},
	{"AmmoContainer", "NATO_25x184mm"}
};

public string nearestName(string itemName) {
	List<string> targets = new List<string>(validNames);
	targets.AddRange(nameAliases.Keys);
	
	string bestTarget = "";
	double bestScore = 50;
	
	foreach(string target in targets) {
		double score = stringDistance(itemName, target);
		if(score < bestScore) {
			bestTarget = target;
			bestScore = score;
		}
	}
	
	if(bestTarget != "") {
		return bestTarget;
	} else {
		return itemName;
	}
}

public double stringDistance(string given, string target) {
	//Optimal string alignment distance estimate of Damerau–Levenshtein distance
	double[,] costArray = new double[given.Length, target.Length];
	for(int i = 0; i < given.Length; i++) {
		costArray[i, 0] = i;
	}
	for(int j = 0; j < target.Length; j++) {
		costArray[0, j] = j;
	}
	
	for(int i = 1; i < given.Length; i++) {
		for(int j = 1; j < target.Length; j++) {
			double substitutionCost = 1;
			if(given[i] == target[j]) {
				substitutionCost = 0;
			}
			costArray[i, j] = Math.Min(costArray[i - 1, j] + 1, //Deletion cost of 1
							  Math.Min(costArray[i, j - 1] + 1, //Insertion cost of 1
									   costArray[i - 1, j - 1] + substitutionCost)); //Substitution cost of 1 when applicable, or 0 when there's a match
			if(i > 1 && j > 1 && given[i] == target[j - 1] && given[i - 1] == target[j]) {
				costArray[i, j] = Math.Min(costArray[i, j], costArray[i - 2, j - 2] + substitutionCost); //Transposition cost of 1 when applicable, or 0 when there's a match
			}
		}
	}
	return costArray[given.Length - 1, target.Length - 1];
}
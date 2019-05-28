/*
 * R e a d m e
 * -----------
 * 
 * In this file you can include any instructions or other comments you want to have injected onto the 
 * top of your final script. You can safely delete this file if you do not want any such comments.
 */

// This file contains your actual script.
//
// You can either keep all your code here, or you can create separate
// code files to make your program easier to navigate while coding.
//
// In order to add a new utility class, right-click on your project,
// select 'New' then 'Add Item...'. Now find the 'Space Engineers'
// category under 'Visual C# Items' on the left hand side, and select
// 'Utility Class' in the main area. Name it in the box below, and
// press OK. This utility class will be merged in with your code when
// deploying your final script.
//
// You can also simply create a new utility class manually, you don't
// have to use the template if you don't want to. Just do so the first
// time to see what a utility class looks like.
//
// Go to:
// https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
//
// to learn more about ingame scripts.


Dictionary<string, List<ContainerRequest>> containerRequests;
Dictionary<IMyTerminalBlock, Dictionary<string, List<ContainerRequest>>> requestsPerContainer;

List<IMyTerminalBlock> containerBlocks;
int containerIndex;

static List<string> knownCategories;

IEnumerator<bool> stateMachine;

public Program()
{

	// The constructor, called only once every session and
	// always before any other method is called. Use it to
	// initialize your script.
	//
	// The constructor is optional and can be removed if not
	// needed.

	Runtime.UpdateFrequency = UpdateFrequency.Update1;

	/*Echo("Testing...");
			Echo(nearestName("Bulletproof Glass"));
			Echo(nearestName("Canvas"));
			Echo(nearestName("Computer"));
			Echo(nearestName("Construction Component"));
			Echo(nearestName("Detector Component"));
			Echo(nearestName("Display"));
			Echo(nearestName("Explosives"));
			Echo(nearestName("Girder"));
			Echo(nearestName("Gravity Component"));
			Echo(nearestName("Interior Plate"));
			Echo(nearestName("Large Steel Tube"));
			Echo(nearestName("Medical Component"));
			Echo(nearestName("Metal Grid"));
			Echo(nearestName("200mm Missile Container"));
			Echo(nearestName("Motor"));
			Echo(nearestName("Nato Ammo Container"));
			Echo(nearestName("Power Cell"));
			Echo(nearestName("Radio Communication Component"));
			Echo(nearestName("Shield Component"));
			Echo(nearestName("Small Steel Tube"));
			Echo(nearestName("Solar Cell"));
			Echo(nearestName("Steel Plate"));
			Echo(nearestName("Superconductor"));
			Echo(nearestName("Thruster Component"));*/

	knownCategories = new List<string>(new string[] { "ore", "ingot", "component" });

	stateMachine = Coroutine();

	// Signal the programmable block to run again in the next tick. Be careful on how much you
	// do within a single tick, you can easily bog down your game. The more ticks you do your
	// operation over, the better.
	//
	// What is actually happening here is that we are _adding_ the Once flag to the frequencies.
	// By doing this we can have multiple frequencies going at any time.
	Runtime.UpdateFrequency |= UpdateFrequency.Once;
}

public IEnumerator<bool> populateContainers()
{
	Echo("Start Populate");
	containerRequests = new Dictionary<string, List<ContainerRequest>>();
	requestsPerContainer = new Dictionary<IMyTerminalBlock, Dictionary<string, List<ContainerRequest>>>();

	List<IMyTerminalBlock> terminalBlocks = new List<IMyTerminalBlock>();
	GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(terminalBlocks);
	containerBlocks = new List<IMyTerminalBlock>();
	Echo("Data structures created");
	foreach(IMyTerminalBlock terminal in terminalBlocks)
	{
		if(terminal.InventoryCount > 0 && !(terminal is IMyReactor || terminal is IMyGasGenerator))
		{
			containerBlocks.Add(terminal);
		}
	}
	foreach(IMyTerminalBlock container in containerBlocks)
	{
		if(container.CustomData.Contains("debug"))
		{
			Echo(container.CustomName);
			for(int i = 0; i < container.InventoryCount; i++)
			{
				IMyInventory inventory = (container as IMyEntity).GetInventory(i);
				List<MyInventoryItem> itemsInInventory = new List<MyInventoryItem>();
				inventory.GetItems(itemsInInventory);
				foreach(MyInventoryItem item in itemsInInventory)
				{
					Echo(item.Type.TypeId + " - " + item.Type.SubtypeId + ": " + item.Amount.ToString());
				}
			}
		}

		if(!container.CustomData.ToLower().Contains("inventory"))
		{
			continue;
		}
		Echo("Rule found: " + container.CustomData);
		string[] requestLines = container.CustomData.Split(new char[] { '\n' });
		foreach(string line in requestLines)
		{
			Echo("Line: " + line);
			if(line.ToLower().Contains("inventory"))
			{
				continue;
			}
			string[] requestParts = line.ToLower().Split(new char[] { ' ' });
			string type = requestParts[0].ToLower();
			if(!knownCategories.Contains(type))
			{
				type = nearestName(requestParts[0].ToLower());
			}
			Echo("Type: " + type);
			int amount = 0;
			bool lowPriority = false;
			for(int i = 1; i < requestParts.Length; i++)
			{
				if(int.TryParse(requestParts[i], out amount))
				{
					Echo("Amount: " + requestParts[i]);
				}
				if(requestParts[i].ToLower() == "low")
				{
					Echo("Priority: low");
					lowPriority = true;
				}
			}
			ContainerRequest request = new ContainerRequest(container, type, amount, lowPriority);
			if(!containerRequests.ContainsKey(type))
			{
				containerRequests[type] = new List<ContainerRequest>();
			}
			containerRequests[type].Add(request);
			if(!requestsPerContainer.ContainsKey(container))
			{
				requestsPerContainer[container] = new Dictionary<string, List<ContainerRequest>>();
			}
			if(!requestsPerContainer[container].ContainsKey(type))
			{
				requestsPerContainer[container][type] = new List<ContainerRequest>();
			}
			requestsPerContainer[container][type].Add(request);
			yield return true;
		}
	}

	foreach(List<ContainerRequest> requestList in containerRequests.Values)
	{
		requestList.Sort(compareRequestPriority);
	}

	containerIndex = 0;
}

public void Save()
{

	// Called when the program needs to save its state. Use
	// this method to save your state to the Storage field
	// or some other means.
	//
	// This method is optional and can be removed if not
	// needed.

}



public void Main(string argument, UpdateType updateType)
{

	// The main entry point of the script, invoked every time
	// one of the programmable block's Run actions are invoked.
	//
	// The method itself is required, but the argument above
	// can be removed if not needed.



	// Usually I verify that the argument is empty or a predefined value before running the state
	// machine. This way we can use arguments to control the script without disturbing the
	// state machine and its timing. For the purpose of this example however, I will omit this.

	// We only want to run the state machine(s) when the update type includes the
	// "Once" flag, to avoid running it more often than it should. It shouldn't run
	// on any other trigger. This way we can combine state machine running with
	// other kinds of execution, like tool bar commands, sensors or what have you.
	if(argument == "debug")
	{
		populateContainers();
		foreach(IMyTerminalBlock container in containerBlocks)
		{
			processInventory(container);
		}
		return;
	}

	if((updateType & UpdateType.Once) == UpdateType.Once)
	{
		RunStateMachine();
	}
}

public void RunStateMachine()
{
	// If there is an active state machine, run its next instruction set.
	if(stateMachine != null)
	{
		// The MoveNext method is the most important part of this system. When you call
		// MoveNext, your method is invoked until it hits a `yield return` statement.
		// Once that happens, your method is halted and flow control returns _here_.
		// At this point, MoveNext will return `true` since there's more code in your
		// method to execute. Once your method reaches its end and there are no more
		// yields, MoveNext will return false to signal that the method has completed.
		// The actual return value of your yields are unimportant to the actual state
		// machine.

		// If there are no more instructions, we stop and release the state machine.
		if(!stateMachine.MoveNext())
		{
			stateMachine.Dispose();

			// In our case we just want to run this once, so we set the state machine
			// variable to null. But if we wanted to continously run the same method, we
			// could as well do
			// _stateMachine = RunStuffOverTime();
			// instead.
			stateMachine = null;
		}
		else
		{
			// The state machine still has more work to do, so signal another run again,
			// just like at the beginning.
			Runtime.UpdateFrequency |= UpdateFrequency.Once;
		}
	}
}

public IEnumerator<bool> Coroutine()
{
	while(true)
	{
		/*Echo("Testing...");
				Echo(nearestName("Bulletproof Glass"));
				yield return true;
				Echo(nearestName("Canvas"));
				yield return true;
				Echo(nearestName("Computer"));
				yield return true;
				Echo(nearestName("Construction Component"));
				yield return true;
				Echo(nearestName("Detector Component"));
				yield return true;
				Echo(nearestName("Display"));
				yield return true;
				Echo(nearestName("Explosives"));
				yield return true;
				Echo(nearestName("Girder"));
				yield return true;
				Echo(nearestName("Gravity Component"));
				yield return true;
				Echo(nearestName("Interior Plate"));
				yield return true;
				Echo(nearestName("Large Steel Tube"));
				yield return true;
				Echo(nearestName("Medical Component"));
				yield return true;
				Echo(nearestName("Metal Grid"));
				yield return true;
				Echo(nearestName("200mm Missile Container"));
				yield return true;
				Echo(nearestName("Motor"));
				yield return true;
				Echo(nearestName("Nato Ammo Container"));
				yield return true;
				Echo(nearestName("Power Cell"));
				yield return true;
				Echo(nearestName("Radio Communication Component"));
				yield return true;
				Echo(nearestName("Shield Component"));
				yield return true;
				Echo(nearestName("Small Steel Tube"));
				yield return true;
				Echo(nearestName("Solar Cell"));
				yield return true;
				Echo(nearestName("Steel Plate"));
				yield return true;
				Echo(nearestName("Superconductor"));
				yield return true;
				Echo(nearestName("Thruster Component"));
				yield return true;
				yield break;*/

		if(containerBlocks == null || containerIndex >= containerBlocks.Count)
		{
			IEnumerator<bool> subcoroutine = populateContainers();
			while(subcoroutine.MoveNext())
			{
				yield return true;
			}
		}

		IMyTerminalBlock container = containerBlocks[containerIndex];
		processInventory(container);
		containerIndex = (containerIndex + 1);
		yield return true;
	}
}

public void processInventory(IMyTerminalBlock container)
{
	Echo("Processing " + container.CustomName + " (" + container.InventoryCount + " inventories)");
	for(int i = 0; i < container.InventoryCount; i++)
	{
		if(i == 0)
		{
			if(container is IMyAssembler || (container is IMyRefinery && container.CustomData == ""))
			{
				continue;
			}
		}
		Echo("Inventory number " + i.ToString());
		IMyInventory inventory = (container as IMyEntity).GetInventory(i);
		List<MyInventoryItem> itemsInInventory = new List<MyInventoryItem>();
		inventory.GetItems(itemsInInventory);
		Echo("Items in inventory: " + itemsInInventory.Count);
		for(int itemIndex = 0; itemIndex < itemsInInventory.Count; itemIndex++)
		{
			MyInventoryItem item = itemsInInventory[itemIndex];
			string itemName = item.Type.SubtypeId.ToLower();
			string itemCategory = getCategory(item).ToLower();
			List<ContainerRequest> conflictingRequests = new List<ContainerRequest>();
			if(requestsPerContainer.ContainsKey(container) && requestsPerContainer[container].ContainsKey(itemName))
			{
				conflictingRequests.AddRange(requestsPerContainer[container][itemName]);
			}
			if(requestsPerContainer.ContainsKey(container) && requestsPerContainer[container].ContainsKey(itemCategory))
			{
				conflictingRequests.AddRange(requestsPerContainer[container][itemCategory]);
			}
			if(requestsPerContainer.ContainsKey(container) && requestsPerContainer[container].ContainsKey("all"))
			{
				conflictingRequests.AddRange(requestsPerContainer[container]["all"]);
			}
			MyDefinitionId definitionID = item.Type;
			List<ContainerRequest> matchingRequests = new List<ContainerRequest>();
			if(containerRequests.ContainsKey(itemName))
			{
				matchingRequests.AddRange(containerRequests[itemName]);
			}
			if(containerRequests.ContainsKey(itemCategory))
			{
				matchingRequests.AddRange(containerRequests[itemCategory]);
			}
			if(containerRequests.ContainsKey("all"))
			{
				matchingRequests.AddRange(containerRequests["all"]);
			}
			foreach(ContainerRequest request in matchingRequests)
			{
				if(container == request.container)
				{
					continue;
				}
				Echo("Request found to transfer " + request.amount.ToString() + " " + request.itemName);
				bool conflictFound = false;
				foreach(ContainerRequest conflict in conflictingRequests)
				{
					Echo("Conflict level is " + compareRequestPriority(request, conflict).ToString());
					if(compareRequestPriority(request, conflict) >= 0)
					{
						Echo("Ignoring request due to conflict.");
						conflictFound = true;
						break;
					}
				}
				if(conflictFound)
				{
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
				if(amount != 0)
				{
					VRage.MyFixedPoint existingAmount = requestInventory.GetItemAmount(definitionID);
					amount -= existingAmount;
					if(amount <= 0)
					{
						Echo("Request already satisfied.");
						continue;
					}
					Echo(amount.ToString() + " needed, " + item.Amount.ToString() + " available.");
					amount = VRage.MyFixedPoint.Min(amount, item.Amount);
					Echo("Attempting to transfer " + amount.ToString() + " " + request.itemName + " (" + itemIndex.ToString() + ")");
					bool result = inventory.TransferItemTo(requestInventory, itemIndex, stackIfPossible: true, amount: amount);
					if(result)
					{
						Echo("Success!");
					}
					else
					{
						Echo("Failure.");
					}
				}
				else
				{
					Echo("Attempting to transfer all " + request.itemName + " (" + itemIndex.ToString() + ")");
					bool result = inventory.TransferItemTo(requestInventory, itemIndex, stackIfPossible: true);
					if(result)
					{
						Echo("Success!");
					}
					else
					{
						Echo("Failure.");
					}
				}
			}
		}
	}
}

public struct ContainerRequest
{
	public IMyTerminalBlock container;
	public string itemName;
	public int amount;
	public bool lowPriority;
	//public bool capped;

	public ContainerRequest(IMyTerminalBlock container, string itemName, int amount = 0, bool lowPriority = false)
	{
		this.container = container;
		this.itemName = itemName;
		this.amount = amount;
		this.lowPriority = lowPriority;
	}
}

public static int compareRequestPriority(ContainerRequest request1, ContainerRequest request2)
{
	int priority1 = 0;
	int priority2 = 0;
	if(request1.amount != 0)
	{
		priority1 += 4;
	}
	if(request2.amount != 0)
	{
		priority2 += 4;
	}
	if(!knownCategories.Contains(request1.itemName))
	{
		priority1 += 2;
	}
	if(!knownCategories.Contains(request2.itemName))
	{
		priority2 += 2;
	}
	if(request1.itemName != "all")
	{
		priority1 += 1;
	}
	if(request2.itemName != "all")
	{
		priority2 += 1;
	}

	if(request1.lowPriority)
	{
		priority1 -= 100;
	}
	if(request2.lowPriority)
	{
		priority2 -= 100;
	}

	//if(priority1 != priority2) {
	return priority2 - priority1;
	//} else {
	//	return request1.amount - request2.amount;
	//}
}

public string getCategory(MyInventoryItem item)
{
	return item.Type.TypeId.Split(new char[] { '_' })[1];
}

List<string> validNames = new List<string> {
	"stone",
	"iron",
	"nickel",
	"cobalt",
	"magnesium",
	"silicon",
	"silver",
	"gold",
	"platinum",
	"uranium",
	"scrap",
	"ice",

	"construction",
	"metalgrid",
	"interiorplate",
	"steelplate",
	"girder",
	"smalltube",
	"largetube",
	"motor",
	"display",
	"bulletproofglass",
	"superconductor",
	"computer",
	"reactor",
	"thrust",
	"gravitygenerator",
	"medical",
	"radiocommunication",
	"detector",
	"explosives",
	"solarcell",
	"powercell",

	"automaticrifleitem",
	"preciseautomaticrifleitem",
	"rapidfireautomaticrifleitem",
	"ultimateautomaticrifleitem",
	"welderitem",
	"welder2item",
	"welder3item",
	"welder4item",
	"anglegrinderitem",
	"anglegrinder2item",
	"anglegrinder3item",
	"anglegrinder4item",
	"handdrillitem",
	"handdrill2item",
	"handdrill3item",
	"handdrill4item",
	"oxygenbottle",
	"hydrogenbottle",

	"nato_5p56x45mm",
	"nato_25x184mm",
	"missile200mm"
};

Dictionary<string, string> nameAliases = new Dictionary<string, string> {
	{"eliteautomaticrifleitem", "ultimateautomaticrifleitem"},
	{"automaticrifle2item", "preciseautomaticrifleitem"},
	{"automaticrifle3item", "rapidfireautomaticrifleitem"},
	{"automaticrifle4item", "ultimateautomaticrifleitem"},
	{"enhancedanglegrinderitem", "anglegrinder2item"},
	{"proficientanglegrinderitem", "anglegrinder3item"},
	{"eliteanglegrinderitem", "anglegrinder4item"},
	{"enhanceddrillitem", "handdrill2item"},
	{"proficientdrillitem", "handdrill3item"},
	{"elitedrillitem", "handdrill4item"},
	{"magazine", "nato_5p56x45mm"},
	{"ammocontainer", "nato_25x184mm"},
	{"200mmmissilecontainer", "missile200mm"},
};

public string nearestName(string itemName)
{
	itemName = itemName.ToLower();
	if(validNames.Contains(itemName))
	{
		return itemName;
	}
	if(nameAliases.ContainsKey(itemName))
	{
		return nameAliases[itemName];
	}

	List<string> targets = new List<string>(validNames);
	targets.AddRange(nameAliases.Keys);

	string bestTarget = "";
	double bestScore = 20;

	foreach(string target in targets)
	{
		double score = stringDistance(itemName, target);
		if(score < bestScore)
		{
			bestTarget = target;
			bestScore = score;
		}
	}

	if(bestTarget != "")
	{
		if(validNames.Contains(bestTarget))
		{
			return bestTarget;
		}
		else if(nameAliases.ContainsKey(bestTarget))
		{
			return nameAliases[bestTarget];
		}
		else
		{
			throw new Exception("The machines are lying to you.");
		}
	}
	else
	{
		return itemName;
	}
}

public static double stringSimilarity(string given, string target)
{
	//Longest common subsequence
	double[,] costArray = new double[given.Length, target.Length];
	for(int i = 0; i < given.Length; i++)
	{
		costArray[i, 0] = 0;
	}
	for(int j = 0; j < target.Length; j++)
	{
		costArray[0, j] = 0;
	}

	for(int i = 1; i < given.Length; i++)
	{
		for(int j = 1; j < target.Length; j++)
		{
			if(given[i] == target[j])
			{
				costArray[i, j] = costArray[i - 1, j - 1] + 1;
			}
			else
			{
				costArray[i, j] = Math.Max(costArray[i, j - 1], costArray[i - 1, j]);
			}
		}
	}
	return costArray[given.Length - 1, target.Length - 1];
}

public static double stringDistance(string given, string target)
{
	//Optimal string alignment distance estimate of Damerau–Levenshtein distance
	double[,] costArray = new double[given.Length, target.Length];
	for(int i = 0; i < given.Length; i++)
	{
		costArray[i, 0] = i;
	}
	for(int j = 0; j < target.Length; j++)
	{
		costArray[0, j] = j;
	}

	for(int i = 1; i < given.Length; i++)
	{
		for(int j = 1; j < target.Length; j++)
		{
			double substitutionCost = 1;
			if(given[i] == target[j])
			{
				substitutionCost = 0;
			}
			costArray[i, j] = Math.Min(costArray[i - 1, j] + 0.25, //Deletion cost of 1
							  Math.Min(costArray[i, j - 1] + 1, //Insertion cost of 1
									   costArray[i - 1, j - 1] + substitutionCost)); //Substitution cost of 1 when applicable, or 0 when there's a match
			if(i > 1 && j > 1 && given[i] == target[j - 1] && given[i - 1] == target[j])
			{
				costArray[i, j] = Math.Min(costArray[i, j], costArray[i - 2, j - 2] + substitutionCost); //Transposition cost of 1 when applicable, or 0 when there's a match
			}
		}
	}
	return costArray[given.Length - 1, target.Length - 1];
}
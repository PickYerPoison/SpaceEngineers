const string NO_PROFILE_SELECTED = "No profile selected.";

string CurrentProfileName = NO_PROFILE_SELECTED;
Dictionary<string, Profile> Profiles = new Dictionary<string, Profile>();
IMyProjector MyProjector = null;

public Program()
{
	
	// The constructor, called only once every session and
	// always before any other method is called. Use it to
	// initialize your script. 
	//     
	// The constructor is optional and can be removed if not
	// needed.
    
    // Begin update frequency
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
    
    // Reload all the saved profiles
    LoadProfiles(Storage);
    
    // Find the nearest projector and latch onto it
    double closestDistance = -1;
    var projectors = new List<IMyProjector>();
    GridTerminalSystem.GetBlocksOfType<IMyProjector>(projectors);
    foreach (var projector in projectors)
    {
        var distance = Vector3D.Distance(Me.GetPosition(), projector.GetPosition());
        if (distance < closestDistance || closestDistance == -1)
        {
            closestDistance = distance;
            MyProjector = projector;
        }
    }
}

public void Save() 
{
	
	// Called when the program needs to save its state. Use
	// this method to save your state to the Storage field
	// or some other means. 
	// 
	// This method is optional and can be removed if not
	// needed.
	
    // Save all profiles
    Storage = SaveProfiles();
}

public void Main(string originalArgument)
{
	
	// The main entry point of the script, invoked every time
	// one of the programmable block's Run actions are invoked.
	// 
	// The method itself is required, but the argument above
	// can be removed if not needed.
	
    string argument = originalArgument.ToLower();
    
    if (argument == "")
    {
        Me.GetSurface(0).WriteText(GetReadout());
    }
    else if (argument.StartsWith("help"))
    {
        if (argument == "help")
        {
            Echo("Commands: load, save, delete, reload, rename, status");
            Echo("");
            Echo("Type \"help <command>\" for more help.");
        }
        else
        {
            var needsHelpWith = argument.Substring(5);
            switch (needsHelpWith)
            {
            case "load":
                Echo("load");
                Echo("Loads a profile, or loads a set of profiles from custom data.");
                Echo("");
                Echo("Usage:");
                Echo("load - Displays a list of saved profiles that can be loaded.");
                Echo("load <profile> - Loads a profile.");
                Echo("load from custom data - Loads a set of profiles from the custom data.");
                break;
            case "save":
                Echo("save");
                Echo("Saves a profile, or saves current profile set to custom data.");
                Echo("");
                Echo("Usage:");
                Echo("save - Saves any changes to the current profile.");
                Echo("save <profile> - Saves to a specific profile (and selects it).");
                Echo("save to custom data - Saves the profile set to the custom data.");
                break;
            case "delete":
                Echo("delete");
                Echo("Deletes a profile.");
                Echo("");
                Echo("Usage:");
                Echo("delete <profile> - Deletes a profile.");
                break;
            case "reload":
                Echo("reload");
                Echo("Reloads the current profile, erasing any modifications.");
                Echo("");
                Echo("Usage:");
                Echo("reload - Reloads the current profile.");
                break;
            case "rename":
                Echo("rename");
                Echo("Renames the current profile.");
                Echo("");
                Echo("Usage:");
                Echo("rename <new name> - Renames the current profile.");
                break;
            case "status":
                Echo("status");
                Echo("Shows the current profile status.");
                Echo("");
                Echo("Usage:");
                Echo("status - Dumps profile/projector status.");
                break;
            }
        }
    }
    else if (argument.StartsWith("load"))
    {
        if (argument == "load")
        {
            if (Profiles.Count() == 0)
            {
                Echo("No profiles have been saved.");
            }
            else
            {
                Echo("Saved profiles:");
                foreach (var profile in Profiles.Values)
                {
                    Echo(profile.Name);
                }
            }
        }
        else if (argument == "load from custom data")
        {
            if (Me.CustomData == "")
            {
                Echo("Custom data was empty.");
            }
            else
            {
                if (LoadProfiles(Me.CustomData) == true)
                {
                    Echo("Custom data successfully loaded!");
                    Me.CustomData = "";
                }
                else
                {
                    Echo("Custom data contained invalid entries.");
                }
            }
        }
        else
        {
            if (ProjectorIsWorking() == false)
            {
                Echo("Cannot load profiles without a working projector.");
            }
            else
            {
                var profileToLoad = argument.Substring(5);
                
                if (Profiles.ContainsKey(profileToLoad) == true)
                {
                    LoadProfile(profileToLoad);
                    Echo("Successfully loaded profile \"" + Profiles[profileToLoad].Name + "\".");
                }
                else
                {
                    Echo("Profile \"" + originalArgument.Substring(5) + "\" not found.");
                }
            }
        }
    }
    else if (argument.StartsWith("save"))
    {
        if (argument == "save")
        {
            if (ProjectorIsWorking() == false)
            {
                Echo("Cannot save profiles without a working projector.");
            }
            else
            {
                if (NoProfileSelected() == true)
                {
                    Echo("No profile selected.");
                }
                else
                {
                    SaveProfile(CurrentProfileName);
                    Echo("Profile \"" + CurrentProfileName + "\" updated.");
                }
            }
        }
        else if (argument == "save to custom data")
        {
            Me.CustomData = SaveProfiles();
            Echo("Profiles saved to custom data.");
        }
        else
        {
            var profileToSave = originalArgument.Substring(5);
            
            if (profileToSave.ToLower() == "from custom data")
            {
                Echo("You cannot save a profile with that name.");
            }
            else
            {
                SaveProfile(profileToSave);
                LoadProfile(profileToSave);
                Echo("Saved under profile \"" + profileToSave + "\".");
            }
        }
    }
    else if (argument.StartsWith("delete"))
    {
        var profileToDelete = originalArgument.Substring(7);
        
        if (Profiles.ContainsKey(profileToDelete.ToLower()))
        {
            Profiles.Remove(profileToDelete.ToLower());
            
            if (CurrentProfileName.ToLower() == profileToDelete.ToLower())
            {
                CurrentProfileName = NO_PROFILE_SELECTED;
            }
            
            Echo("Deleted profile \"" + profileToDelete + "\".");
        }
        else
        {
            Echo("Profile \"" + profileToDelete + "\" not found.");
        }
    }
    else if (argument == "reload")
    {
        if (NoProfileSelected() == true)
        {
            Echo("No profile selected.");
        }
        else
        {
            LoadProfile(CurrentProfileName);
            Echo("Profile \"" + CurrentProfileName + "\" reloaded.");
        }
    }
    else if (argument.StartsWith("rename"))
    {
        if (NoProfileSelected() == true)
        {
            Echo("No profile selected.");
        }
        else
        {
            var newProfileName = originalArgument.Substring(7);
            if (Profiles.ContainsKey(newProfileName.ToLower()))
            {
                Echo("Profile already exists.");
            }
            else
            {
                var oldProfile = CurrentProfileName;
                SaveProfile(newProfileName);
                LoadProfile(newProfileName);
                Profiles.Remove(oldProfile.ToLower());
                
                Echo("Renamed \"" + oldProfile + "\" to \"" + newProfileName + "\".");
            }
        }
    }
    else if (argument == "status")
    {
        Echo(GetReadout());
    }
    else
    {
        Echo("Command not recognized. Type \"help\" for valid commands.");
    }
}

// Returns whether the projector is working.
public bool ProjectorIsWorking()
{
    return (MyProjector != null && MyProjector.IsWorking);
}

// Saves the current projector settings to a profile.
public void SaveProfile(string s)
{
    if (Profiles.ContainsKey(s.ToLower()))
    {
        Profiles[s.ToLower()] = new Profile(s, MyProjector.ProjectionOffset, MyProjector.ProjectionRotation);
    }
    else
    {
        Profiles.Add(s.ToLower(), new Profile(s, MyProjector.ProjectionOffset, MyProjector.ProjectionRotation));
    }
}

// Returns whether no profile is selected
public bool NoProfileSelected()
{
    return (CurrentProfileName == NO_PROFILE_SELECTED);
}

// Returns the current profile. Best to check if a profile is selected first.
public Profile GetCurrentProfile()
{
    Profile defaultValue = new Profile("", Vector3I.Zero, Vector3I.Zero);
    if (Profiles.ContainsKey(CurrentProfileName.ToLower()))
    {
        defaultValue = Profiles[CurrentProfileName.ToLower()];
    }
    return defaultValue;
}

// Returns a string readout to display on the programmable block's text surface.
public string GetReadout()
{
    string readout = "No projector found!";
    
    if (MyProjector != null)
    {
        if (MyProjector.IsWorking == false)
        {
            readout = "Projector is not working.";
        }
        else if (MyProjector.IsProjecting == false)
        {
            readout = "No blueprint selected.";
        }
        else
        {
            if (NoProfileSelected() == true)
            {
                readout = NO_PROFILE_SELECTED;
            }
            else
            {
                readout = "Profile: " + CurrentProfileName;
                
                if (IsCurrentProfileModified() == true)
                {
                    readout += " (modified)";
                }
            }
            
            readout += "\n\n";
            
            readout += "Blocks remaining: " + MyProjector.RemainingBlocks.ToString() + "\n";
            readout += "Functional: " + (MyProjector.RemainingBlocks - MyProjector.RemainingArmorBlocks).ToString() + "\n";
            readout += "Armor: " + MyProjector.RemainingArmorBlocks.ToString();
        }
    }
    
    return readout;
}

// Returns whether or not the current profile matches the values in the projector.
public bool IsCurrentProfileModified()
{
    bool modified = false;
    if (NoProfileSelected() == false)
    {
        var currentProfile = GetCurrentProfile();
        modified = (currentProfile.ProjectionOffset != MyProjector.ProjectionOffset ||
                    currentProfile.ProjectionRotation != MyProjector.ProjectionRotation);
    }
    return modified;
}

// Loads profiles from a string. Returns false (and does not load) if invalid entries are found.
public bool LoadProfiles(string s)
{
    bool valid = true;
    
    var newProfiles = new List<Profile>();
    
    var profileStrings = s.Split(';');
    
    var newProfile = new Profile("UNINITIALIZED", Vector3I.Zero, Vector3I.Zero); 
    
    foreach (var profileString in profileStrings)
    {
        if (newProfile.ParseFromString(profileString) == false)
        {
            valid = false;
            break;
        }
        else
        {
            newProfiles.Add(newProfile);
        }
    }
    
    if (valid == true)
    {
        Profiles.Clear();
        foreach (var profile in newProfiles)
        {
            Profiles.Add(profile.Name.ToLower(), profile);
        }
    }
    
    return valid;
    
}

// Saves all profiles to a string.
public string SaveProfiles()
{
    int counter = 1;
    string saveString = "";
    foreach (var profile in Profiles.Values)
    {
        saveString += profile.ToString();
        if (counter < Profiles.Count())
        {
            saveString += ';';
        }
        counter++;
    }
    return saveString;
}

// Loads a profile.
public bool LoadProfile(string s)
{
    bool successful = false;
    
    if (Profiles.ContainsKey(s.ToLower()) == true)
    {
        successful = true;
        CurrentProfileName = Profiles[s.ToLower()].Name;
        MyProjector.ProjectionOffset = GetCurrentProfile().ProjectionOffset;
        MyProjector.ProjectionRotation = GetCurrentProfile().ProjectionRotation;
        MyProjector.UpdateOffsetAndRotation();
    }
    
    return successful;
}

// This is a profile, which has a name and the values for the projector.
public struct Profile
{
    public string Name;
    public Vector3I ProjectionOffset;
    public Vector3I ProjectionRotation;
    
    public Profile(string n, IMyProjector projector)
    {
        Name = n;
        ProjectionOffset = projector.ProjectionOffset;
        ProjectionRotation = projector.ProjectionRotation;
    }
    
    public Profile(string n, Vector3I po, Vector3I pr)
    {
        Name = n;
        ProjectionOffset = po;
        ProjectionRotation = pr;
    }
    
    public Profile(string s)
    {
        Name = s;
        ProjectionOffset = Vector3I.Zero;
        ProjectionRotation = Vector3I.Zero;
        
        ParseFromString(s);
    }
    
    public bool ParseFromString(string s)
    {
        bool valid = true;
        
        var vals = s.Split(',');
        
        if (vals.Length != 7)
        {
            valid = false;
        }
        else
        {
            Name = vals[0];
            
            for (int i = 1; i < 7; i++)
            {
                switch (i)
                {
                case 1:
                    valid = Int32.TryParse(vals[i], out ProjectionOffset.X);
                    break;
                case 2:
                    valid = Int32.TryParse(vals[i], out ProjectionOffset.Y);
                    break;
                case 3:
                    valid = Int32.TryParse(vals[i], out ProjectionOffset.Z);
                    break;
                case 4:
                    valid = Int32.TryParse(vals[i], out ProjectionRotation.X);
                    break;
                case 5:
                    valid = Int32.TryParse(vals[i], out ProjectionRotation.Y);
                    break;
                case 6:
                    valid = Int32.TryParse(vals[i], out ProjectionRotation.Z);
                    break;
                }
                
                if (valid == false)
                {
                    break;
                }
            }
        }
        
        return valid;
    }
    
    public override string ToString()
    {
        string saveString = Name + ',';
        saveString += ProjectionOffset.X.ToString() + ',';
        saveString += ProjectionOffset.Y.ToString() + ',';
        saveString += ProjectionOffset.Z.ToString() + ',';
        saveString += ProjectionRotation.X.ToString() + ',';
        saveString += ProjectionRotation.Y.ToString() + ',';
        saveString += ProjectionRotation.Z.ToString();
        return saveString;
    }
}
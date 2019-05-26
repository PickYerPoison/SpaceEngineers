const double MIN_DIST_TO_WELDER = 5.5;
 
Session currentSession;
public static List<string> TextLog;
 
public static void Log(object text)
{
    TextLog.Add(text.ToString());
    if (TextLog.Count() > 20)
        TextLog.RemoveAt(0);
}
// this is for shuffling target lists
private static Random rng = new Random();  
   
public static void Shuffle<T>(ref List<T> list)
{  
    int n = list.Count;  
    while (n > 1) {  
        n--;  
        int k = rng.Next(n + 1);  
        T value = list[k];  
        list[k] = list[n];  
        list[n] = value;  
    }  
}

// Holds information about effects (for use by effects blocks)
public struct Environment
{
    public string Name;
    public Color PrimaryColor, SecondaryColor, TertiaryColor;
    public Environment(string name, Color primary, Color secondary, Color tertiary)
    {
        Name = name;
        PrimaryColor = primary;
        SecondaryColor = secondary;
        TertiaryColor = tertiary;
    }
}

// Holds information about presets
public struct Preset
{
    public string Name;
    public int NumberOfRounds;
    public double CivilianChance;
    public int MinGroupSize;
    public int MaxGroupSize;
    public bool TimedRounds;
    public float TimeModifier;
    public Preset(string name, int numRounds = 5, double civilianChance = 0, int minGroupSize = 1, int maxGroupSize = 1, bool timedRounds = false, float timeModifier = 1f)
    {
        Name = name;
        NumberOfRounds = numRounds;
        CivilianChance = civilianChance;
        MinGroupSize = minGroupSize;
        MaxGroupSize = maxGroupSize;
        TimedRounds = timedRounds;
        TimeModifier = timeModifier;
    }
}

// Represents a display that shows session information.
// The first line of the custom data should be "firing range display" and subsequent lines determine the content of each line on the display.
// The syntax for lines is:
// DETAIL Prefix @ Suffix
// DETAIL is the data to be displayed on that line. Prefix and suffix are text to be displayed. Every @ is replaced with the data.
// Valid data: None, CurrentRound, TotalRounds, CivilianPercent, MinGroupSize, MaxGroupSize, Environment, Preset
public class Display
{
    IMyTextPanel m_panel;       // the text panel this display controls
    Session m_session;          // the session this text panel is tied to
     
    public Display() : this(null, null) { }
    public Display(Session session) : this(null, session) { }
    public Display(IMyTextPanel panel) : this(panel, null) { }
    public Display(IMyTextPanel panel, Session session)
    {
        m_panel = panel;
        m_session = session;
    }
     
    public void SetPanel(IMyTextPanel panel)
    {
        m_panel = panel;
    }
     
    public void SetSession(Session session)
    {
        m_session = session;
    }
     
    public void Update()
    {
        if (m_panel != null && m_session != null)
        {
            // create output string
            string toDisplay = "";
             
            // get the custom data from the text panel
            string[] entries = m_panel.CustomData.Split(new char[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
             
            // go through each entry but the first (it's just "firing range display")
            for (int i = 1; i < entries.Length; i++)
            {
                // put into a variable for nicer looking code
                var entry = entries[i];
                 
                // trim leading and trailing whitespace
                entry = entry.Trim();
                 
                // find where the first word ends
                var firstSpaceIndex = entry.IndexOf(' ');
                 
                // if just one word, it's just a command
                if (firstSpaceIndex == -1)
                {
                    toDisplay += FillData(entry) + "\n";
                }
                // otherwise separate out the command and the text
                else
                {
                    toDisplay += FillData(entry.Substring(0, firstSpaceIndex), entry.Substring(firstSpaceIndex)) + "\n";
                }
            }
             
            // set the panel text
            m_panel.WritePublicText(toDisplay);
        }
    }
     
    string FillData(string dataType)
    {
        return FillData(dataType, "@");
    }
     
    string FillData(string dataType, string replaceIn)
    {
        if (m_session == null)
            return "ERROR: SESSION NOT FOUND";
        dataType = dataType.Trim().ToLower();
        string data;
        switch (dataType)
        {
            case "currentround":
                var round = m_session.CurrentRound;
                data = round == 0 ? "Not started" : round.ToString();
                break;
            case "totalrounds": 
                var totalRounds = m_session.TotalRounds;
                data = totalRounds == 0 ? "Endless" : totalRounds.ToString();
                break;
            case "civilianchance":
                data = (Math.Round(m_session.CivilianChance * 100)).ToString();
                break;
            case "mingroupsize": data = m_session.MinGroupSize.ToString(); break;
            case "maxgroupsize": data = m_session.MaxGroupSize.ToString(); break;
            case "timedrounds": data = m_session.TimedRounds ? "Yes" : "No"; break;
            case "environment": data = m_session.Environment.Name; break;
            case "preset":
                if (m_session.Preset.Equals(default(Preset)))
                    data = "Custom";
                else
                    data = m_session.Preset.Name;
                break;
            case "timemodifier": data = (m_session.TimeModifier * 100 - 100).ToString(); break;
            default: data = ""; break;
        }
        return replaceIn.Replace("@", data);
    }
}
   
// Represents a target on the firing range.
// On the base, each target needs a rotor (to pop up with) and a welder (which will fix up the target after it's shot).
// On the extension, each target needs a terminal block (the target to shoot), and may optionally contain lights.
// The rotor and the welder need "firing range target" in their custom data. The bullseye terminal block needs "firing range bullseye" in its custom data.
// In order to properly judge what orientation is "up" the bullseye needs to be closer to the rotor than the welder when active, and closer to the welder than the rotor when inactive.
public class Target
{
    IMyMotorStator m_rotor;
    IMyTerminalBlock m_bullseye;
    IMyShipWelder m_welder;
    List<IMyLightingBlock> m_lights;
     
    float m_damageThreshold; // damage value at which the target flips down
    bool m_civilian;    // deduct points for shooting
    bool m_up;        // whether the target is currently up
    bool m_upDesired;   // whether the target should be up
     
    Color hostileColor = new Color(255, 0, 0, 255);
    Color civilianColor = new Color(0, 0, 255, 255);
     
    public Target(IMyMotorStator rotor, IMyTerminalBlock bullseye)
    {
        m_rotor = rotor;
        m_bullseye = bullseye;
        m_welder = null;
        m_lights = new List<IMyLightingBlock>();
         
        m_damageThreshold = 1;
        m_civilian = false;
        m_up = false;
        m_upDesired = false;
    }
     
    public void SetWelder(IMyShipWelder welder)
    {
        m_welder = welder;
        
        if (m_welder != null)
        {
            var distToWelder = Vector3D.Distance(m_bullseye.GetPosition(), m_welder.GetPosition());
            m_up = distToWelder > MIN_DIST_TO_WELDER;
            if (m_up)
                Log("I'm up! Distance to welder: " + distToWelder.ToString());
            else
                Log("I'm down! Distance to welder: " + distToWelder.ToString());
        }
    }
     
    public void SetDamageThreshold(float damage)
    {
        m_damageThreshold = damage;
    }
     
    public void AddLight(IMyLightingBlock light)
    {
        m_lights.Add(light);
    }
     
    public void AddLights(List<IMyLightingBlock> lights)
    {
        m_lights.AddRange(lights);
    }
     
    // sets whether the target is a civilian
    public void SetCivilian(bool civilian)
    {
        foreach (var light in m_lights)
        {
            if (light != null)
            {
                if (civilian)
                    light.SetValue("Color", civilianColor);
                else
                    light.SetValue("Color", hostileColor);
            }
        }
        m_civilian = civilian;
    }
     
    // returns whether the target is a civilian
    public bool IsCivilian()
    {
        return m_civilian;
    }
     
    public void SetActive(bool up)
    {
        m_upDesired = up;
    }
     
    public float GetDamage()
    {
        if (m_bullseye != null)
        {
            return m_bullseye.CubeGrid.GetCubeBlock(m_bullseye.Position).CurrentDamage;
        }
        else
        {
            return m_damageThreshold;
        }
    }
     
    public bool IsActive()
    {
        return m_upDesired;
    }
     
    // asks the target to check if it should be up right now
    public void UpdateStatus()
    {
        if (m_up != m_upDesired)
        {
            Flip();
            m_up = m_upDesired;
            foreach (var light in m_lights)
            {
                if (light != null)
                    light.GetActionWithName(m_up ? "OnOff_On" : "OnOff_Off").Apply(light);
            }
        }
        if (GetDamage() >= m_damageThreshold)
        {
            SetActive(false);
            if (m_welder != null)
                m_welder.GetActionWithName("OnOff_On").Apply(m_welder);
        }
        else if (!m_up && m_welder != null)
        {
            if (GetDamage() == 0)
                m_welder.GetActionWithName("OnOff_Off").Apply(m_welder);
            else
                m_welder.GetActionWithName("OnOff_On").Apply(m_welder);
        }
    }
     
    // flips the target if it's not already moving
    void Flip()
    {
        if (m_rotor != null)
            m_rotor.GetActionWithName("Reverse").Apply(m_rotor);
    }
}
   
// Represents a firing range session. Handles raising and lowering of targets.
// Needs a timer block with "firing range timer" in the custom data for accurate timed actions. The timer block does not need to have any associated actions.
public class Session
{
    // storage variables
    List<Preset> m_presets;     // presets that can be used
    List<Environment> m_environments;   // environments effects can have
    List<IMyTerminalBlock> m_effects;   // effect blocks
    IMyTimerBlock m_timer;      // timer block used by the session
    List<Target> m_targets;     // list of selectable targets
    List<Target> m_nextTargets; // list of targets to activate in the next round
    List<Display> m_displays;   // list of displays
    int m_round;                // the current round
    bool m_started;             // whether the session has started
    bool m_betweenRounds;       // whether it's between rounds
    bool m_allCivilians;        // tracks if all the targets in a round are civilians
    
    // settings for firing range
    int m_presetIndex;          // which preset is selected (becomes Custom if options are changed)
    int m_environmentIndex;     // index of the environment in use
    int m_numRounds;            // number of rounds in this session (how many times targets will pop up)
    int m_scorePerTarget;       // how many points hitting a hostile gives
    double m_civilianChance;    // chance that a target will be a civilian (0 = disable civilians, 0.5 = even mix, 1 = disable hostiles)
    int m_civilianPenalty;      // how many points are lost when a civilian is shot
    int m_minGroupSize;         // minimum targets to appear per round
    int m_maxGroupSize;         // maximum targets to appear per round
    bool m_timedRounds;         // whether rounds have a time limit
    float m_timePerHTarget;     // how much each active hostile target contributes to the round's timer
    float m_timePerCTarget;     // how much each active civilian target contributes to the round's timer
    float m_timeModifier;       // applied to the total round time before the round starts
    
    public int CurrentRound { get { return m_round; } }
    public int TotalRounds { get { return m_numRounds; } }
    public double CivilianChance { get { return m_civilianChance; } }
    public int MinGroupSize
    {
        get
        {
            if (m_minGroupSize > m_targets.Count())
                return m_targets.Count();
            else
                return m_minGroupSize;
        }
    }
    public int MaxGroupSize
    {
        get
        {
            if (m_maxGroupSize > m_targets.Count())
                return m_targets.Count();
            else
                return m_maxGroupSize;
        }
    }
    public bool TimedRounds { get { return m_timedRounds; } }
    public Environment Environment { get { return m_environments[m_environmentIndex]; } }
    public Preset Preset
    {
        get
        {
            if (m_presetIndex == -1)
                return default(Preset);
            else
                return m_presets[m_presetIndex];
        }
    }
    public float TimeModifier { get { return m_timeModifier; } }
    
    // statistics
    int m_score;                // score for this session
    int m_hostileHits;          // how many hostile targets have been hit
    int m_hostileTargets;       // how many hostile targets popped up
    int m_civilianHits;         // how many civilian targets have been hit
    int m_civilianTargets;      // how many civilian targets popped up
    
    // sets up a session with no values, or with specified values
    public Session(IMyTimerBlock timer_block = null, int number_of_rounds = 1, int score_per_target = 1, double civilian_chance = 0, int civilian_penalty = 1,
                    int minimum_group_size = 1, int maximum_group_size = 1, bool timed_rounds = false, float time_per_hostile_target = 3f, float time_per_civilian_target = 1f)
    {
        m_environments = new List<Environment>();
        m_presets = new List<Preset>();
        m_effects = new List<IMyTerminalBlock>();
        m_timer = timer_block;
        m_targets = new List<Target>();
        m_nextTargets = new List<Target>();
        m_displays = new List<Display>();
        m_round = 0;
        m_started = false;
        m_betweenRounds = true;
        
        m_presetIndex = 0;
        m_environmentIndex = 0;
        m_numRounds = number_of_rounds;
        m_scorePerTarget = score_per_target;
        m_civilianChance = civilian_chance;
        m_civilianPenalty = civilian_penalty;
        m_minGroupSize = minimum_group_size;
        m_maxGroupSize = maximum_group_size;
        m_timedRounds = timed_rounds;
        m_timePerHTarget = time_per_hostile_target;
        m_timePerCTarget = time_per_civilian_target;
        m_timeModifier = 1.00f;
         
        ResetStats();
         
        // scrub inputs for errors
        if (m_numRounds < 0)
            m_numRounds = 0;
        if (m_civilianChance < 0)
            m_civilianChance = 0;
        if (m_civilianChance > 1)
            m_civilianChance = 1;
        if (m_minGroupSize < 1)
            m_minGroupSize = 1;
        if (m_maxGroupSize < m_minGroupSize)
            m_maxGroupSize = m_minGroupSize;
        if (m_timePerHTarget < 0f)
            m_timePerHTarget = 0f;
        if (m_timePerCTarget < 0f)
            m_timePerCTarget = 0f;
    }
    
    // starts the session
    public void StartSession()
    {
        if (!m_started)
        {
            // verify valid state before starting session
            string validString = SessionIsValid();
            if (validString != null)
            {
                Log("Unable to start session: " + validString);
                return;
            }
             
            // start over at the first round
            m_round = 0;
              
            // deactivate all targets
            DeactivateAll();
              
            // start after a delay
            StartTimer(3f);
              
            // start between rounds
            m_started = true;
            m_betweenRounds = true;
            
            // apply the environment settings
            ApplyEnvironment();
            
            // start effects
            foreach (var effect in m_effects)
            {
                effect.GetActionWithName("OnOff_On").Apply(effect);
            }
        }
    }
    
    // called every tick to update the session
    public void Update()
    {
        // update all targets
        UpdateTargets();
        
        // if the session has started, advance it
        if (m_started)
        {
            // find if the timer is ticking
            var ticking = m_timer.IsCountingDown;
             
            // see if it's between rounds
            if (m_betweenRounds)
            {
                // if the timer is no longer ticking, it's time to go to the next round
                if (!ticking)
                {
                    Log("Initiating next round");
                    NextRound();
                }
            }
            else
            {
                // go to the next round if no non-civilian targets are up, or if the timer isn't ticking AND timed rounds are enabled
                if (!ActiveTargets() || (!ticking && (m_timedRounds || m_allCivilians)))
                {
                    Log("Active targets? " + ActiveTargets().ToString());
                    NextRound();
                }
            }
        }
    }
    
    // moves to the next round (or in-between phase)
    void NextRound()
    {
        if (m_started)
        {
            if (!m_betweenRounds)
            {
                // if a round just finished, check if any more rounds should be had
                if (m_numRounds > 0 && m_round >= m_numRounds)
                    EndSession();
                else
                {
                    // tally up shot targets
                    foreach (var target in m_nextTargets)
                    {
                        // see if it was hit
                        if (!target.IsActive())
                        {
                            if (target.IsCivilian())
                            {
                                m_civilianHits++;
                                m_score += m_civilianPenalty;
                            }
                            else
                            {
                                m_hostileHits++;
                                m_score += m_scorePerTarget;
                            }
                        }
                        
                        // deactivate target
                        target.SetActive(false);
                    }
     
                    // start the timer
                    StartTimer(2f);
                    
                    // between rounds now
                    m_betweenRounds = true;
                    
                    Log("Ending round " + m_round.ToString());
                }
            }
            else
            {   
                // if a round is starting, get targets for it
                GetNextTargets();
     
                // this only matters if timed rounds are selected
                float roundTime = 0f;
                 
                // check if all civilians
                bool anyHostiles = false;
                 
                // activate this round's targets
                foreach (var target in m_nextTargets)
                {
                    // activate target
                    target.SetActive(true);
                    Log("Activating a target.");
                    // add to total targets
                    if (target.IsCivilian())
                        m_civilianTargets++;
                    else
                    {
                        m_hostileTargets++;
                        anyHostiles = true;
                    }
                     
                    // calculate this round's time
                    if (target.IsCivilian())
                        roundTime += m_timePerCTarget;
                    else
                        roundTime += m_timePerHTarget;
                }
                m_allCivilians = !anyHostiles;
                 
                // if timed rounds are selected (or if all targets are civilians), start the timer
                if (m_timedRounds || m_allCivilians)
                {
                    Log("Starting timer.");
                    StartTimer(roundTime * (m_allCivilians ? 1.00f : m_timeModifier));
                }
                Log("Active targets? " + ActiveTargets());
                // not between rounds anymore
                m_betweenRounds = false;
                 
                // increment number of rounds
                m_round++;
                 
                Log("Starting round " + m_round.ToString());
            }
            
            UpdateDisplays();
        }
    }
    
    // ends the currently running session
    public void EndSession()
    {
        m_nextTargets = new List<Target>();
        m_started = false;
        m_round = m_numRounds;
        m_betweenRounds = true;
        DeactivateAll();
        Log("All targets deactivated.");
        StopTimer();
        Log("Timer stopped.");
        m_round = 0;
        UpdateDisplays();
        Log("Displays updated.");
        
        // stop effects
        foreach (var effect in m_effects)
        {
            effect.GetActionWithName("OnOff_Off").Apply(effect);
        }
    }
    
    // applies the settings for the currently selected environment
    public void ApplyEnvironment()
    {
        //  update effects
        foreach (var effect in m_effects)
        {
            // discard if it's not asking for a color to be set
            var customData = effect.CustomData.ToLower();
            if (customData.Contains("color"))
            {
                // gotta figure out if this has the "Color" property for modification
                var resultList = new List<ITerminalProperty>();
                effect.GetProperties(resultList, (ITerminalProperty x) => x.Id == "Color");
                
                // if it has it, set it!
                if (resultList.Count() > 0)
                {
                    if (customData.Contains("primary"))
                        effect.SetValue("Color", m_environments[m_environmentIndex].PrimaryColor);
                    else if (customData.Contains("secondary"))
                        effect.SetValue("Color", m_environments[m_environmentIndex].SecondaryColor);
                    else if (customData.Contains("tertiary"))
                        effect.SetValue("Color", m_environments[m_environmentIndex].TertiaryColor);
                }
            }
        }
    }
    
    // applies the currently selected preset
    public void ApplyPreset()
    {
        // -1 is reserved for custom, so don't change anything if that's the preset selected
        if (m_presetIndex >= 0)
        {
            var preset = m_presets[m_presetIndex];
            m_numRounds = preset.NumberOfRounds;
            m_civilianChance = preset.CivilianChance;
            m_minGroupSize = preset.MinGroupSize;
            m_maxGroupSize = preset.MaxGroupSize;
            m_timedRounds = preset.TimedRounds;
            m_timeModifier = preset.TimeModifier;
        }
    }
    
    // add an environment
    public void AddEnvironment(Environment environment)
    {
        if (!m_environments.Contains(environment))
            m_environments.Add(environment);
    }
    
    // add a preset
    public void AddPreset(Preset preset)
    {
        if (!m_presets.Contains(preset))
            m_presets.Add(preset);
    }
    
    // add an effect
    public void AddEffect(IMyTerminalBlock effect)
    {
        if (!m_effects.Contains(effect))
            m_effects.Add(effect);
    }
    
    // add a display
    public void SetDisplays(List<Display> displays)
    {
        m_displays = new List<Display>(displays);
        UpdateDisplays();
    }
    
    // increase or decrease a specific property by a preset amount
    public void AdjustProperty(string property, bool increase)
    {
        // can't change mid-game!
        if (!m_started)
        {
            // now a custom preset
            if (property.ToLower() != "environment" && property.ToLower() != "preset")
                m_presetIndex = -1;
            
            switch (property.ToLower())
            {
                case "totalrounds":
                    if (increase)
                        m_numRounds++;
                    else
                        m_numRounds--;
                    m_numRounds = Math.Max(0, m_numRounds);
                    break;
                case "civilianchance":
                    m_civilianChance += increase ? 0.1 : -0.1;
                    m_civilianChance = Math.Max(0, Math.Min(1, m_civilianChance));
                    break;
                case "mingroupsize":
                    m_minGroupSize += increase ? 1 : -1;
                    m_minGroupSize = Math.Max(1, Math.Min(m_maxGroupSize, m_minGroupSize));
                    break;
                case "maxgroupsize":
                    m_maxGroupSize += increase ? 1 : -1;
                    m_maxGroupSize = Math.Max(m_minGroupSize, Math.Min(m_targets.Count(), m_maxGroupSize));
                    break;
                case "timedrounds":
                    m_timedRounds = !m_timedRounds;
                    break;
                case "timemodifier":
                    m_timeModifier += increase ? 0.1f : -0.1f;
                    if (m_timeModifier < 0.25f)
                        m_timeModifier = 0.25f;
                    if (m_timeModifier > 2.00f)
                        m_timeModifier = 2.00f;
                    break;
                case "environment":
                    m_environmentIndex += increase ? 1 : -1;
                    if (m_environmentIndex == m_environments.Count())
                        m_environmentIndex = 0;
                    if (m_environmentIndex == -1)
                        m_environmentIndex = m_environments.Count() - 1;
                    break;
                case "preset":
                    m_presetIndex += increase ? 1 : -1;
                    if (m_presetIndex >= m_presets.Count())
                        m_presetIndex = 0;
                    if (m_presetIndex < 0)
                        m_presetIndex = m_presets.Count() - 1;
                    ApplyPreset();
                    break;
            }
             
            // update all displays
            UpdateDisplays();
        }
    }
    
    void ResetStats()
    {
        m_score = 0;
        m_hostileHits = 0;
        m_hostileTargets = 0;
        m_civilianHits = 0;
        m_civilianTargets = 0;
    }
    
    public void SetTimer(IMyTimerBlock timer)
    {
        m_timer = timer;
    }
    
    // checks if the session is valid. returns null if valid.
    string SessionIsValid()
    {
        if (m_timer == null)
            return "No attached timer.";
        if (m_targets.Count() == 0)
            return "No targets found.";
        return null;
    }
    
    public void SetTargets(List<Target> targets)
    {
        m_targets = new List<Target>(targets);
    }
    
    public void GetNextTargets()
    {
        // shuffle list of targets
        Shuffle<Target>(ref m_targets);
        
        // get the number of targets for the next round
        int numTargets = rng.Next(Math.Min(m_minGroupSize, m_targets.Count()), Math.Min(m_maxGroupSize, m_targets.Count()));
        
        // create next group of targets
        m_nextTargets.Clear();
        for (int i = 0; i < Math.Min(numTargets, m_targets.Count()); i++)
        {
            m_nextTargets.Add(m_targets[i]);
        }
        
        // figure out civilians
        foreach (var target in m_nextTargets)
        {
            if (m_civilianChance > 0)
            {
                if (rng.NextDouble() < m_civilianChance)
                    target.SetCivilian(true);
                else
                    target.SetCivilian(false);
            }
            else
                target.SetCivilian(false);
        }
    }
    
    // returns if any targets are active (optionally includes civilian targets)
    bool ActiveTargets(bool includeCivilians = false)
    {
        foreach (var target in m_targets)
        {
            if (target.IsActive() && (!target.IsCivilian() || includeCivilians))
            {
                return true;
            }
        }
        return false;
    }
    
    // deactivates all targets
    void DeactivateAll()
    {
        foreach (var target in m_targets)
        {
            target.SetActive(false);
        }
        UpdateTargets();
    }
    
    // updates each target
    void UpdateTargets()
    {
        foreach (var target in m_targets)
        {
            target.UpdateStatus();
        }
    }
    
    // updates each display
    void UpdateDisplays()
    {
        foreach (var display in m_displays)
        {
            display.Update();
        }
    }
    
    // sets the timer delay
    void SetTimerDelay(float delay)
    {
        m_timer.SetValue("TriggerDelay", delay);
    }
    
    // stops the timer
    void StopTimer()
    {
        if (m_timer != null)
            m_timer.GetActionWithName("Stop").Apply(m_timer);
    }
    
    // starts the timer without setting the delay
    void StartTimer()
    {
        if (m_timer != null)
            m_timer.GetActionWithName("Start").Apply(m_timer);
    }
    
    // starts the timer with a delay (0 = trigger now)
    void StartTimer(float delay)
    {
        if (m_timer != null)
        {
            StopTimer();
            if (delay == 0f)
            {
                m_timer.GetActionWithName("TriggerNow").Apply(m_timer);
            }
            else
            {
                SetTimerDelay(delay);
                StartTimer();
            }
        }
    }
}

// executed once on first run
public Program()
{
    // initialize text log
    TextLog = new List<string>();
    
    // initialize session
    currentSession = new Session(number_of_rounds: 5);
    
    // add effects environments
    currentSession.AddEnvironment(
        new Environment("Industrial",
            new Color(255, 128, 0, 255),
            new Color(255, 128, 0, 255),
            new Color(255, 255, 255, 255)
        ));
    currentSession.AddEnvironment(
        new Environment("Psychadelic",
            new Color(255, 0, 255, 255),
            new Color(0, 255, 0, 255),
            new Color(255, 255, 255, 255)
        ));
    currentSession.AddEnvironment(
        new Environment("Tunnel of Love",
            new Color(255, 128, 128, 255),
            new Color(255, 64, 64, 255),
            new Color(255, 96, 96, 255)
        ));
    
    // add presets
    currentSession.AddPreset(
        new Preset("Shooting Gallery",
            civilianChance: 0,
            minGroupSize: 1,
            maxGroupSize: 1,
            timedRounds: false
        ));
    currentSession.AddPreset(
        new Preset("Trigger Discipline",
            civilianChance: 0.8,
            minGroupSize: 999,
            maxGroupSize: 999,
            timedRounds: true,
            timeModifier: 1f
        ));
    currentSession.AddPreset(
        new Preset("Rapid Aiming",
            civilianChance: 0,
            minGroupSize: 1,
            maxGroupSize: 1,
            timedRounds: true,
            timeModifier: 0.5f
        ));
    currentSession.AddPreset(
        new Preset("Zerg Rush",
            civilianChance: 0,
            minGroupSize: 999,
            maxGroupSize: 999,
            timedRounds: true,
            timeModifier: 0.5f
        ));
    
    // retrieve components
    RetrieveTargets();
    RetrieveDisplays();
    RetrieveEffects();
}

public void Main(string argument)
{
    currentSession.Update();
    foreach (var line in TextLog)
    {
        Echo(line);
    }
    
    if (argument != "")
    {
        string[] entries = argument.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
        switch (entries[0].Trim().ToLower())
        {
            case "start":
                RetrieveTargets();
                var timers = new List<IMyTimerBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyTimerBlock>(timers, (IMyTimerBlock x) => x.CustomData.Contains("firing range timer"));
                Log("Found " + timers.Count() + " timers for firing range.");
                currentSession.SetTimer(timers[0]);
                currentSession.StartSession();
                break;
            case "reset":
                RetrieveTargets();
                RetrieveDisplays();
                currentSession.EndSession();
                currentSession = new Session(number_of_rounds: currentSession.TotalRounds, minimum_group_size: currentSession.MinGroupSize, maximum_group_size: currentSession.MaxGroupSize, civilian_chance: currentSession.CivilianChance);
                RetrieveTargets();
                RetrieveDisplays();
                break;
            case "increase":
            case "decrease":
                if (entries.Length == 2)
                {
                    currentSession.AdjustProperty(entries[1], entries[0] == "increase");
                }
                break;
        }
    }
}

// retrieve displays and update the session
public void RetrieveDisplays()
{
    // find panels
    var displays = new List<Display>();
    var panels = new List<IMyTextPanel>();
    GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(panels, (IMyTextPanel x) => x.CustomData.Contains("firing range display"));
    foreach (var panel in panels)
    {
        displays.Add(new Display(panel, currentSession));
    }
    currentSession.SetDisplays(displays);
}

// retrieve targets and update the session 
public void RetrieveTargets()
{
    // remove list of old targets
    var targets = new List<Target>();
    
    // find bullseyes
    var bullseyes = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(bullseyes, ((IMyTerminalBlock x) => x.CustomData.Contains("firing range bullseye")));
    
    // find rotors
    var rotors = new List<IMyMotorStator>();
    GridTerminalSystem.GetBlocksOfType<IMyMotorStator>(rotors, (IMyMotorStator x) => x.CustomData.Contains("firing range target"));
    
    // find welders
    var welders = new List<IMyShipWelder>();
    GridTerminalSystem.GetBlocksOfType<IMyShipWelder>(welders, (IMyShipWelder x) => x.CustomData.Contains("firing range target"));
    
    if (rotors.Count() != bullseyes.Count())
    {
        Log("Rotor/bullseye numbers do not match - aborting.");
        return;
    }
    if (rotors.Count() != welders.Count())
    {
        Log("Rotor/welder numbers do not match - aborting.");
        return;
    }
     
    // match them up, if possible
    foreach (var rotor in rotors)
    {
        // get closest bullseye to match with
        var dists = new List<double>(bullseyes.Count());
        foreach (var bullseye in bullseyes)
        {
            dists.Add(Vector3D.Distance(bullseye.GetPosition(), rotor.GetPosition()));
        }
        var min = dists.Min<double>();
        var index = dists.IndexOf(min);
        var target = new Target(rotor, bullseyes[index]);
        
        // find lights
        var lights = new List<IMyLightingBlock>();
        GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights, (IMyLightingBlock x) => x.CubeGrid == bullseyes[index].CubeGrid);
        // set up range of lights
        foreach (var light in lights)
        {
            var dist = Math.Max(Vector3D.Distance(bullseyes[index].GetPosition(), light.GetPosition()), 3);
            light.SetValueFloat("Radius", (float)dist);
        }
        target.AddLights(lights);
        
        // find welders
        dists = new List<double>(welders.Count());
        foreach (var welder in welders)
        {
            dists.Add(Vector3D.Distance(welder.GetPosition(), rotor.GetPosition()));
        }
        min = dists.Min<double>();
        index = dists.IndexOf(min);
        target.SetWelder(welders[index]);
        
        // add target
        targets.Add(target);
    }
     
    Log("Targets: " + targets.Count().ToString());
    currentSession.SetTargets(targets);
}

// retrieve effects and update the session
public void RetrieveEffects()
{
    var effects = new List<IMyTerminalBlock>();
    GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(effects, (IMyTerminalBlock x) => x.CustomData.Contains("firing range effect"));
    foreach (var effect in effects)
    {
        currentSession.AddEffect(effect);
    }   
}
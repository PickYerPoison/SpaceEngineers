public void Main(string argument){
    List<IMyGravityGenerator> gens = new List<IMyGravityGenerator>();     
    GridTerminalSystem.GetBlocksOfType<IMyGravityGenerator>(gens);     
         
    if (gens.Count == 0)     
        return;     
         
    List<IMyShipController> controllers = new List<IMyShipController>();     
    GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);     
         
    if (controllers.Count == 0)    
        return;    
    
    IMyShipController controller = controllers.First();   
    double ngrav = controller.GetNaturalGravity().Length();   
    double agrav = 9.81 - ngrav;   
    Echo("Natural gravity: " + ngrav.ToString());   
    Echo("Artificial gravity: " + agrav.ToString());   
    
    foreach (var gen in gens)    
    {    
        if (gen.CustomData.ToLower().Contains("normal"))    
        {  
            gen.SetValueFloat("Gravity", (float)(agrav)); 
        }    
        else if (gen.CustomData.ToLower().Contains("inverted"))    
        {     
            gen.SetValueFloat("Gravity", (float)(-agrav));    
        }    
    }    
}
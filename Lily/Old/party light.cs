const string filterTag = "Party Light";  
double hue; 
  
public Program()  
{  
    hue = 0;  
}  
  
public void Main(string argument) {  
    List<IMyTerminalBlock> lights = new List<IMyTerminalBlock>();  
    GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(lights, ((IMyTerminalBlock x) => x.CustomName.Contains(filterTag)));
    Color partyColor = HSVToRGB(hue, 1, 1);  
    for (int i = 0; i < lights.Count; i++)  
    {  
        lights[i].SetValue("Color", partyColor);  
    }  
    hue += 0.5;
    if (hue >= 360) { hue = 0; }
}  
  
Color HSVToRGB(double h, double S, double V) 
{
  double H = h; 
  while (H < 0) { H += 360; }; 
  while (H >= 360) { H -= 360; }; 
  double R, G, B; 
  if (V <= 0) 
    { R = G = B = 0; } 
  else if (S <= 0) 
  { 
    R = G = B = V; 
  } 
  else 
  { 
    double hf = H / 60.0; 
    int i = (int)Math.Floor(hf); 
    double f = hf - i; 
    double pv = V * (1 - S); 
    double qv = V * (1 - S * f); 
    double tv = V * (1 - S * (1 - f)); 
    switch (i) 
    { 
 
      // Red is the dominant color 
 
      case 0: 
        R = V; 
        G = tv; 
        B = pv; 
        break; 
 
      // Green is the dominant color 
 
      case 1: 
        R = qv; 
        G = V; 
        B = pv; 
        break; 
      case 2: 
        R = pv; 
        G = V; 
        B = tv; 
        break; 
 
      // Blue is the dominant color 
 
      case 3: 
        R = pv; 
        G = qv; 
        B = V; 
        break; 
      case 4: 
        R = tv; 
        G = pv; 
        B = V; 
        break; 
 
      // Red is the dominant color 
 
      case 5: 
        R = V; 
        G = pv; 
        B = qv; 
        break; 
 
      // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here. 
 
      case 6: 
        R = V; 
        G = tv; 
        B = pv; 
        break; 
      case -1: 
        R = V; 
        G = pv; 
        B = qv; 
        break; 
 
      // The color is not defined, we should throw an error. 
 
      default: 
        //LFATAL("i Value error in Pixel conversion, Value is %d", i); 
        R = G = B = V; // Just pretend its black/white 
        break; 
    } 
  }
    int r = Clamp((int)(R * 255.0)); 
    int g = Clamp((int)(G * 255.0)); 
    int b = Clamp((int)(B * 255.0)); 
   
    return new Color(255, r, g, b); 
} 
 
/// <summary> 
/// Clamp a value to 0-255 
/// </summary> 
int Clamp(int i) 
{ 
  if (i < 0) return 0; 
  if (i > 255) return 255; 
  return i; 
}
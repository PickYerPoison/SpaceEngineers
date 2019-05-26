// todo:
// make fish stop congregating in edges and corners
// implement plants

static Random random = new Random();

Fishtank StoredFishtank;

public Program()
{
	Runtime.UpdateFrequency = UpdateFrequency.Update10;
    
    StoredFishtank = new Fishtank();
}

public void Save()
{
    
}

public void Main(string argument, UpdateType updateType)
{
    if (argument == "")
    {
        StoredFishtank.Update();
        
        // retrieve LCDs
        var LCDs = new List<IMyTextPanel>(); 
        GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(LCDs, (IMyTextPanel x) => x.CustomData.Contains("fishtank"));

        foreach (var LCD in LCDs)
        {
            LCD.WritePublicText(StoredFishtank.ToString()); 
        }
    }
}

// This is an object in a fishtank. It has an X and a Y location.
abstract class FishtankObject
{
    // Updates the data of the fishtank object for the next tick.
    public abstract void Update();
    // Modifies a fishtank character set to display this object.
    public abstract void AddToFishtank(ref List<List<char>> fishtankContents);
    
    // The X position in the fishtank.
    protected double xPosition;
    // The Y position in the fishtank.
    protected double yPosition;
    // The fishtank that contains this object.
    protected Fishtank fishtank;
};

// This is a bubble. It floats upwards, gets smaller, and then disappears.
class Bubble : FishtankObject
{
    const int CHAR_CHANGE = 6;
    const int Y_CHANGE = 2;

    public Bubble(Fishtank container) : this(0, 0, container) { }
    public Bubble(double x, double y, Fishtank container)
    {
        xPosition = x;
        yPosition = y;
        fishtank = container;
        changeTimer = 0;
        displayChar = 'O';
    }
    
    public override void Update()
    {
        bool dead = false;
        changeTimer++;
        if (changeTimer == Y_CHANGE)
        {
            yPosition--;
            if (yPosition < 0)
            {
                dead = true;
            }
        }
        
        if (changeTimer == CHAR_CHANGE)
        {
            changeTimer = 0;
            if (displayChar == 'O')
            {
                displayChar = 'o';
            }
            else if (displayChar == 'o')
            {
                displayChar = 'x';
                changeTimer = Y_CHANGE + 1;
            }
            else
            {
                dead = true;
            }
        }
        
        if (dead == true)
        {
            fishtank.RemoveObject(this);
        }
    }
    
    public override void AddToFishtank(ref List<List<char>> fishtankContents)
    {
        int xRounded = (int)Math.Round(xPosition);
        int yRounded = (int)Math.Round(yPosition);
        if (xRounded >= 0 && xRounded < fishtank.Width &&
            yRounded >= 0 && yRounded < fishtank.Height)
        {
            fishtankContents[yRounded][xRounded] = displayChar;
        }
    }
    
    protected int changeTimer;
    protected char displayChar;
};

// This is a fish. It swims around and sometimes blows bubbles.
class Fish : FishtankObject
{
    public Fish(Fishtank container) : this(0, 0, "", "", container) { }
    public Fish(double x, double y, string bodyLeft, string bodyRight, Fishtank container)
    {
        xPosition = x;
        yPosition = y;
        fishtank = container;
        facingLeft = true;
        xDestination = (int)x;
        yDestination = (int)y;
        xSpeed = 0.2;
        ySpeed = 0.1;
        idleTimer = 0;
        bubbleTimer = 0;
        
        bodyCharsLeft = new List<char>();
        bodyCharsRight = new List<char>();
        
        foreach (var c in bodyLeft)
        {
            bodyCharsLeft.Add(c);
        }
        
        foreach (var c in bodyRight)
        {
            bodyCharsRight.Add(c);
        }
    }
    
    public override void Update()
    {
        // move if necessary
        if (xPosition != xDestination)
        {
            if (xPosition < xDestination)
            {
                facingLeft = false;
                xPosition += xSpeed;
                if (xPosition > xDestination)
                {
                    xPosition = xDestination;
                }
            }
            else
            {
                facingLeft = true;
                xPosition -= xSpeed;
                if (xPosition < xDestination)
                {
                    xPosition = xDestination;
                }
            }
        }

        if (yPosition != yDestination)
        {
            if (yPosition < yDestination)
            {
                yPosition += ySpeed;
                if (yPosition > yDestination)
                {
                    yPosition = yDestination;
                }
            }
            else
            {
                yPosition -= ySpeed;
                if (yPosition < yDestination)
                {
                    yPosition = yDestination;
                }
            }
        }
        
        if (xPosition == xDestination && yPosition == yDestination)
        {
            if (idleTimer == -1)
            {
                idleTimer = random.Next(0, 50);
            }
            else if (idleTimer > 0)
            {
                idleTimer--;
            }
            else // idleTimer == 0
            {
                xDestination = (int)random.Next(0, fishtank.Width);
                yDestination = (int)random.Next(0, fishtank.Height);
                
                idleTimer = -1;
            }
        }
        
        // timer for creating a bubble
        if (bubbleTimer == 0)
        {
            var newBubblePosition = xPosition;
            if (facingLeft == false)
            {
                newBubblePosition += bodyCharsLeft.Count();
            }
            fishtank.AddObject(new Bubble(newBubblePosition, yPosition - 1, fishtank));
            bubbleTimer = random.Next(60, 120);
        }
        else // bubbleTimer > 0
        {
            bubbleTimer--;
        }
        
        // bounce off the walls/ceiling if necessary
        if (xPosition < 0 && xDestination < 0)
        {
            xDestination = -xDestination;
            facingLeft = false;
        }
        else if (xPosition >= fishtank.Width && xDestination >= fishtank.Width)
        {
            xDestination = xDestination - fishtank.Width;
            facingLeft = true;
        }
        
        if (yPosition < 0 && yDestination < 0)
        {
            yDestination = -yDestination;
        }
        else if (yPosition >= fishtank.Height && yDestination >= fishtank.Height)
        {
            yDestination = yDestination - fishtank.Height;
        }
    }
    
    public override void AddToFishtank(ref List<List<char>> fishtankContents)
    {
        int xRounded = (int)Math.Round(xPosition);
        int yRounded = (int)Math.Round(yPosition);
        
        // add the fish to the tank
        for (int i = 0; i < bodyCharsLeft.Count(); i++)
        {
            char toPlace = ' ';
            if (facingLeft == true)
            {
                toPlace = bodyCharsLeft[i];
            }
            else
            {
                toPlace = bodyCharsRight[i];
            }
            
            if (yRounded >= 0 && yRounded < fishtank.Height &&
                xRounded + i >= 0 && xRounded + i < fishtank.Width)
            {
                fishtankContents[yRounded][xRounded + i] = toPlace;
            }
        }
    }
    
    protected List<char> bodyCharsLeft;
    protected List<char> bodyCharsRight;
    protected bool facingLeft;
    protected int xDestination;
    protected int yDestination;
    protected double xSpeed;
    protected double ySpeed;
    private int idleTimer;
    private int bubbleTimer;
};

// This is a plant. It grows from the bottom of the tank.
class Plant : FishtankObject
{
    public Plant() : this(0, 0) { }
    public Plant(double x, double y)
    {
        xPosition = x;
        yPosition = y;
    }
    
    public override void Update()
    {
        
    }
    
    public override void AddToFishtank(ref List<List<char>> fishtankContents)
    {
        
    }
};

// This is a fishtank. It has fish and plants in it.
class Fishtank
{
    public Fishtank()
    {
        int height = 21;
        int width = 75;
        display = new List<List<char>>(height);
        for (int i = 0; i < height; i++)
        {
            display.Add(new List<char>(width));
            for (int j = 0; j < width; j++)
            {
                display[i].Add(' ');
            }
        }
        contents = new List<FishtankObject>();
        
        toAdd = new List<FishtankObject>();
        toDelete = new List<FishtankObject>();
        
        contents.Add(new Fish(3, 2, "<o)))<", ">(((o>", this));
        contents.Add(new Fish(12, 2, "<o)))<", ">(((o>", this));
        contents.Add(new Fish(35, 4, "<o)))<", ">(((o>", this));
        contents.Add(new Fish(46, 5, "<o)))<", ">(((o>", this));
        contents.Add(new Fish(55, 5, "<o)))<", ">(((o>", this));
        contents.Add(new Fish(54, 7, "<o)))<", ">(((o>", this));
        contents.Add(new Fish(22, 10, "<><", "><>", this));
        contents.Add(new Fish(5, 12, "<><", "><>", this));
        contents.Add(new Fish(28, 12, "<><", "><>", this));
        contents.Add(new Fish(26, 15, "<><", "><>", this));
        contents.Add(new Fish(68, 16, "<><", "><>", this));
        contents.Add(new Fish(37, 18, "<><", "><>", this));
        contents.Add(new Fish(64, 12, "<><", "><>", this));
        contents.Add(new Fish(22, 8, "<><", "><>", this));
        contents.Add(new Fish(8, 6, "<><", "><>", this));
        contents.Add(new Fish(71, 14, "<><", "><>", this));
        contents.Add(new Fish(25, 8, "<=O<", ">O=>", this));
        contents.Add(new Fish(37, 14, "<=O<", ">O=>", this));
        contents.Add(new Fish(58, 16, "<=O<", ">O=>", this));
        
    }
    
    public void Update()
    {
        // clear out list
        for (int i = 0; i < display.Count(); i++)
        {
            for (int j = 0; j < display[i].Count(); j++)
            {
                display[i][j] = ' ';
            }
        }
        
        // update everything in the fishtank
        foreach (var item in contents)
        {
            item.Update();
        }
        
        foreach (var item in toAdd)
        {
            contents.Add(item);
        }
        toAdd.Clear();
        
        foreach (var item in toDelete)
        {
            contents.Remove(item);
        }
        toDelete.Clear();
        
        // display everything in the fishtank
        for (int i = contents.Count() - 1; i >= 0; i--)
        {
            contents[i].AddToFishtank(ref display);
        }
    }
    
    public override string ToString()
    {
        var outputString = new StringBuilder();
        
        foreach (var row in display)
        {
            foreach (var cell in row)
            {
                outputString.Append(cell); 
            }
            outputString.AppendLine();
        }
        
        return outputString.ToString();
    }
    
    public void AddObject(FishtankObject item)
    {
        toAdd.Add(item);
    }
    
    public void RemoveObject(FishtankObject item)
    {
        toDelete.Add(item);
    }
    
    public int Width
    {
        get
        {
            if (display.Count() > 0)
            {
                return display[0].Count();
            }
            else
            {
                return 0;
            }
        }
    }
    
    public int Height
    {
        get
        {
            return display.Count();
        }
    }
    
    protected List<FishtankObject> contents;
    protected List<List<char>> display;
    protected List<FishtankObject> toAdd;
    protected List<FishtankObject> toDelete;
};
public IMyRadioAntenna PrimaryAntenna;

public int MyID = 2;

public int HeartbeatTimer = 60;
 
// Format for an order:
//      Description: Description of the order.
//      Data: Data included in the order.
public enum MessageOrders
{
    INVALID_ORDER,          // Description: Invalid order. Used to indicate uninitialized orders.
                            // Data: None.
                             
    DTM_STATUS,             // Description: Information about current drone status.
                            // Data: int(StatusTypes) status
                             
    DTM_DISTRESS,           // Description: Distress call requesting operator intervention.
                            // Data: int(DistressTypes) status
                             
    DTM_ORE_INFO,           // Description: Information about ore that was mined.
                            // Data: int(OreTypes) ore_type, int X, int Y, int Z
                             
    END_OF_DTM,             // END OF DRONE-TO-MOTHERSHIP MESSAGES
                             
    MTD_MOVETO,             // Description: Order to move to a location.
                            // Data: int X, int Y, int Z
                             
    MTD_FACETO,             // Description: Order to face a location.
                            // Data: int X, int Y, int Z
                             
    MTD_MOVETO_FACETO,      // Description: Order to move to a location while facing a location.
                            // Data: int X_moveto, int Y_moveto, int Z_moveto, int X_faceto, int Y_faceto, int Z_faceto
                             
    END_OF_MTD              // END OF MOTHERSHIP-TO-DRONE MESSAGES
}
 
public class MessageData
{
    // For organizational purposes, it is recommended you keep the following three values.
    public bool Valid;              // Whether the data in this message is valid.
    public int Recipient;           // The intended recipient of this message.
    public int Sender;              // The sender of this message.
      
    // Add any additional information that a message may contain here.
    public MessageOrders Order;
    public List<int> Data;
     
    // Default constructor that should initialize all data to safe values.
    public MessageData()
    {
        Valid = false;
        Recipient = 0;
        Sender = 0;
        Order = 0;
        Data = new List<int>();
    }
}

public class Command
{
    public MessageOrders Order;
    public int Data;
    public int Sender;
    public Vector3 MoveTo;
    public Vector3 FaceTo;
    
    public Command()
    {
        Order = MessageOrders.INVALID_ORDER;
        Data = 0;
        Sender = 0;
        MoveTo = new Vector3(0, 0, 0);
        FaceTo = new Vector3(0, 0, 0);
    }
    
    // DTM_STATUS, DTM_DISTRESS
    public Command(MessageOrders order, int data, int sender)
    {
        Order = order;
        Data = data;
        Sender = sender;
        MoveTo = new Vector3(0, 0, 0);
        FaceTo = new Vector3(0, 0, 0);
    }
    
    // DTM_ORE_INFO
    public Command(MessageOrders order, int data, Vector3 vector1)
    {
        Order = order;
        Data = data;
        Sender = 0;
        MoveTo = vector1;
        FaceTo = new Vector3(0, 0, 0);
    }
    
    // MTD_MOVETO, MTD_FACETO
    public Command(MessageOrders order, Vector3 vector1)
    {
        Order = order;
        Data = 0;
        Sender = 0;
        if (order == MessageOrders.MTD_FACETO)
        {
            MoveTo = new Vector3(0, 0, 0);
            FaceTo = vector1;
        }
        else
        {
            MoveTo = vector1;
            FaceTo = new Vector3(0, 0, 0);
        }
    }
    
    // MTD_MOVETO_FACETO
    public Command(MessageOrders order, Vector3 vector1, Vector3 vector2)
    {
        Order = order;
        Data = 0;
        Sender = 0;
        MoveTo = vector1;
        FaceTo = vector2;
    }
}
 
public enum StatusTypes
{
    IDLE,
    MOVING,
    MINING,
    DOCKING,
    DOCKED,
    LOADING,
    UNLOADING
}
 
public enum DistressTypes
{
    NOMINAL,
    CARGO_FULL,
    LOW_POWER,
    DAMAGE
}
 
public enum OreTypes
{
    NONE,
    STONE,
    IRON,
    NICKEL,
    COBALT,
    MAGNESIUM,
    SILICON,
    SILVER,
    GOLD,
    PLATINUM,
    URANIUM,
    ICE
}
 
public Queue<Command> Commands;
 
public Program()
{
    // get the primary radio antenna
    PrimaryAntenna = null;
    var radioAntennas = new List<IMyRadioAntenna>();
    GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(radioAntennas, (IMyRadioAntenna x) => x.CubeGrid == Me.CubeGrid);
    if (radioAntennas.Count() > 0)
    {
        PrimaryAntenna = radioAntennas.First(); 
    }
    
    // set up the orders queue
    Commands = new Queue<Command>();
    
    // keep running
    Runtime.UpdateFrequency = UpdateFrequency.Update1; 
}
 
public void Save()
{
 
}
 
public void Main(string argument, UpdateType updateSource)
{
    if (updateSource == UpdateType.Terminal)
    {
        var messageData = new MessageData();
        messageData.Recipient = 1;
        messageData.Order = MessageOrders.DTM_STATUS;
        messageData.Data.Add((int)StatusTypes.IDLE);
        TransmitMessage(messageData);
        Echo("Sending status message!");
    }
    else if (updateSource == UpdateType.Antenna && argument != "")
    {
        var messageData = ReceiveMessage(argument);
        if (messageData.Valid)
        {
            switch (messageData.Order)
            {
                case MessageOrders.MTD_MOVETO:
                case MessageOrders.MTD_FACETO:
                    Commands.Enqueue(new Command(messageData.Order, 
                                     new Vector3(messageData.Data[0], messageData.Data[1], messageData.Data[2])));
                    break;
                case MessageOrders.MTD_MOVETO_FACETO:
                    Commands.Enqueue(new Command(messageData.Order, 
                                     new Vector3(messageData.Data[0], messageData.Data[1], messageData.Data[2]),
                                     new Vector3(messageData.Data[3], messageData.Data[4], messageData.Data[5])));
                    break;
                default:
                    break;
            }
        }
    }
    else if (updateSource == UpdateType.Update1)
    {
        if (Commands.Count() > 0)
        {
            bool completedCommand = false;
            switch (Commands.Peek().Order)
            {
                case MessageOrders.MTD_MOVETO:
                    // update status of drones
                    Echo("Move to (" + Commands.Peek().MoveTo.X + ", " + Commands.Peek().MoveTo.Z + ", " + Commands.Peek().MoveTo.Z + ").");
                    completedCommand = true;
                    break;
                case MessageOrders.MTD_FACETO:
                    // register distress call
                    Echo("Face to (" + Commands.Peek().FaceTo.X + ", " + Commands.Peek().FaceTo.Z + ", " + Commands.Peek().FaceTo.Z + ").");
                    completedCommand = true;
                    break;
                case MessageOrders.MTD_MOVETO_FACETO:
                    // update ore information
                    Echo("Move to (" + Commands.Peek().MoveTo.X + ", " + Commands.Peek().MoveTo.Z + ", " + Commands.Peek().MoveTo.Z + ") and face to ("
                                     + Commands.Peek().FaceTo.X + ", " + Commands.Peek().FaceTo.Z + ", " + Commands.Peek().FaceTo.Z + ").");
                    completedCommand = true;
                    break;
                default:
                    break;
            }
            
            if (completedCommand)
            {
                Echo("Completed command.");
                Commands.Dequeue();
            }
        }
    }
}

// Unpacks a string (typically from an antenna) into a MessageData.
public MessageData UnpackMessageData(string message)
{
    var data = new List<string>();
    var splitMessage = System.Text.RegularExpressions.Regex.Split(message, "~");
      
    foreach (var item in splitMessage)
    {
        data.Add(item);
    }
      
    var messageData = new MessageData();
     
    bool valid = true;
     
    // if a message doesn't contain a sender, recipient, and order, it's definitely not valid
    if (data.Count() < 3)
    {
        valid = false;
    }
    else
    {
        // retrieve sender, recipient, and order
        for (int i = 0; i < data.Count(); i++)
        {
            switch (i)
            {
                case 0:
                    valid = int.TryParse(data[i], out messageData.Recipient);
                    break;
                case 1: 
                    valid = int.TryParse(data[i], out messageData.Sender);
                    break;
                case 2:
                    valid = Enum.TryParse(data[i], out messageData.Order);
                    break;
                default:
                    // default case fills the rest of the data
                    int dataParsed;
                    valid = int.TryParse(data[i], out dataParsed);
                    if (valid)
                    {
                        messageData.Data.Add(dataParsed);
                    }
                    break;
            }
             
            if (!valid)
            {
                break;
            }
        }
    }
     
    messageData.Valid = valid;
      
    return messageData;
}
  
// Packs a MessageData into a string (for transmitting by antenna).
public string PackMessageData(MessageData messageData)
{
    var data = new List<string>();
     
    data.Add(messageData.Recipient.ToString());
    data.Add(messageData.Sender.ToString());
    data.Add(messageData.Order.ToString());
     
    foreach (var item in messageData.Data)
    {
        data.Add(item.ToString());
    }
     
    var message = "";
     
    if (data.Count() > 0)
    {
        for (int i = 0; i < data.Count() - 1; i++)
        {
            message += data[i] + "~";
        }
         
        message += data[data.Count() - 1];
    }
     
    return message;
}
 
public MessageData ReceiveMessage(string message)
{
    // unpack message
    var messageData = UnpackMessageData(message);
     
    // make sure message was sent to this ship
    if (messageData.Valid)
    {
        if (messageData.Recipient > 0 && messageData.Recipient != MyID)
        {
            messageData.Valid = false;
        }
    }
     
    // return the message
    return messageData;
}

public void TransmitMessage(MessageData messageData)
{
    if (PrimaryAntenna != null)
    {
        messageData.Sender = MyID;
        
        var message = PackMessageData(messageData);
        
        PrimaryAntenna.TransmitMessage(message);
    }
}
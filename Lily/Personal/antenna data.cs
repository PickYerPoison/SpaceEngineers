public struct MessageData
{
    // For organizational purposes, it is recommended you keep the following three values.
    public bool Valid = false;      // Whether the data in this message is valid.
    public int Sender = 0;          // The sender of this message.
    public int Recipient = 0;       // The intended recipient of this message.
    
    // Modify the rest of this struct as necessary to represent your own program's message.
    public int Order = 0;
    public int Data1 = 0;
    public int Data2 = 0;
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
    
    // Put your own code here to turn the List<string> into a MessageData.
    
    return messageData;
}
 
// Packs a MessageData into a string (for transmitting by antenna).
public string PackMessageData(MessageData messageData)
{
    var data = new List<string>();
    
    // Put your own code here to turn the MessageData into a List<string>.
    
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
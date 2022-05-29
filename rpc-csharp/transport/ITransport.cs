namespace rpc_csharp.transport;

public interface ITransport
{
    // Functions
    public abstract void SendMessage(byte[] data);

    public abstract void Close();
    
    // Events
    public event Action OnClose;

    public event Action<string> OnError;

    public event Action<byte[]> OnMessage;

    public event Action OnConnect; 
}
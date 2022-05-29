using rpc_csharp.transport;
using WebSocketSharp.Server;

public interface WebSocketEvents
{
    // Events
    public void OnCloseHandler();

    public void OnErrorHandler(string message);
    public void OnMessageHandler(byte[] data);

    public void OnConnectHandler(); 
}
public class WebSocketService : WebSocketBehavior
{
    private WebSocketEvents listener;
    public WebSocketService(WebSocketEvents listener)
    {
        this.listener = listener;
    }
    protected override void OnMessage(WebSocketSharp.MessageEventArgs e)
    {
        base.OnMessage(e);
        listener.OnMessageHandler(e.RawData);
    }

    protected override void OnError(WebSocketSharp.ErrorEventArgs e)
    {
        base.OnError(e);
        listener.OnErrorHandler(e.Message);
    }

    protected override void OnClose(WebSocketSharp.CloseEventArgs e)
    {
        base.OnClose(e);
        listener.OnCloseHandler();
    }

    protected override void OnOpen()
    {
        base.OnOpen();
        listener.OnConnectHandler();
    }

    public void SendMessage(byte[] data)
    {
        Send(data);
    }

    public void Close()
    {
        
    }
}
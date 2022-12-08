using System;
using rpc_csharp.transport;
using WebSocketSharp;
using WebSocketSharp.Server;

public class WebSocketServerTransport : WebSocketBehavior, ITransport
{
    protected override void OnMessage(MessageEventArgs e)
    {
        base.OnMessage(e);
        OnMessageEvent?.Invoke(e.RawData);
    }

    protected override void OnError(ErrorEventArgs e)
    {
        base.OnError(e);
        OnErrorEvent?.Invoke(e.Message);
    }

    protected override void OnClose(CloseEventArgs e)
    {
        base.OnClose(e);
        OnCloseEvent?.Invoke();
    }

    protected override void OnOpen()
    {
        base.OnOpen();
        OnConnectEvent?.Invoke();
    }

    public void SendMessage(byte[] data)
    {
        Send(data);
    }

    public void Close()
    {
        Sessions.CloseSession(ID);
    }

    public void Dispose()
    {
        OnCloseEvent = null;
        OnErrorEvent = null;
        OnMessageEvent = null;
        OnConnectEvent = null;
    }

    public event Action OnCloseEvent;
    public event Action<string> OnErrorEvent;
    public event Action<byte[]> OnMessageEvent;
    public event Action OnConnectEvent;
}
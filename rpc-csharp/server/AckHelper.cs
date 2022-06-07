using Google.Protobuf;
using rpc_csharp.protocol;
using rpc_csharp.transport;

namespace rpc_csharp.server;

public class AckHelper
{
    private Dictionary<string, (Action<StreamMessage>, Action<Exception>)> oneTimeCallbacks = new();
    private ITransport transport;
    public AckHelper(ITransport transport)
    {
        this.transport = transport;

        transport.OnCloseEvent += () =>
        {
            var err = new Exception("Transport closed while waiting the ACK");
            CloseAll(err);
        };

        transport.OnErrorEvent += (err) =>
        {
            CloseAll(new Exception(err));
        };
    }
    
    private void CloseAll(Exception err)
    {
        foreach (var (_, reject) in oneTimeCallbacks.Values)
        {
            reject(err);
        }
        oneTimeCallbacks.Clear();
    }

    
    public void ReceiveAck(StreamMessage data, uint messageNumber)
    {
        var key = $"{messageNumber},{data.SequenceId}";
        if(oneTimeCallbacks.TryGetValue(key, out var fut))
        {
            oneTimeCallbacks.Remove(key);
            fut.Item1(data);
        }
    }
    
    public Task<StreamMessage> SendWithAck(StreamMessage data)
    {
        var (_, messageNumber) = ProtocolHelpers.ParseMessageIdentifier(data.MessageIdentifier);
        var key = $"{messageNumber},{data.SequenceId}";

        // C#Promiches
        var ret = new TaskCompletionSource<StreamMessage>();
        var accept = new Action<StreamMessage>(message =>
        {
             ret.SetResult(message);
        });
        var reject = new Action<Exception>(error =>
        {
            Console.WriteLine(error.ToString());
            ret.SetCanceled();
        });
        oneTimeCallbacks.Add(key, (accept, reject));

        transport.SendMessage(data.ToByteArray());

        return ret.Task;
    }
}
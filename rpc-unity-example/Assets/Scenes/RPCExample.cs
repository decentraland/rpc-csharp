using System.Collections;
using System.Collections.Generic;
using rpc_csharp;
using UnityEngine;

public class RPCExample : MonoBehaviour
{
    public class TestContext {}
    // Start is called before the first frame update
    void Start()
    {
        var rpcServer = new RpcServer<TestContext>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

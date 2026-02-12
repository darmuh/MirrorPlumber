using Mirror;
using MirrorPlumber;

namespace MyMod;

public class ExampleNetBehaviour : NetworkBehaviour
{
    internal static Plumber GeneratedCommand = new();
    internal static Plumber<string, int> GeneratedRPC = new();
    internal int TestInt = 0;


    private void Awake()
    {
        GeneratedCommand.Create(typeof(ExampleNetBehaviour), nameof(CmdSendHello), PlumberBase.NetType.Command);
        GeneratedCommand.AddListener(TargetRpcMessage);

        GeneratedRPC.Create(typeof(ExampleNetBehaviour), nameof(TargetRpcMessage), PlumberBase.NetType.TargetRpc);
        GeneratedRPC.AddListener(HelloFromTheNetwork);
    }

    private void Start()
    {
        TestInt = UnityEngine.Random.Range(0, 1337);
        CmdSendHello();
    }

    //[Command(requiresAuthority = false)] //These attributes are not necessary since they're just cosmetic without Mirror's Weaver doing anything
    //Realistically, all of this could be in Start but you still need a valid method name to provide mirror
    public void CmdSendHello()
    {
        // This runs on the client calling it
        Plugin.Log.LogMessage($"{this} - CmdSendHello");

        // Client invokes GeneratatedCommand, which calls TargetRpcMessage
        GeneratedCommand?.Invoke(this);
    }

    public void TargetRpcMessage()
    {
        // This runs on the server
        Plugin.Log.LogMessage($"{this} - TargetRpcMessage");

        // Server invokes TargetRpc which targets the first connection client, running HelloFromTheNetwork 
        GeneratedRPC?.Invoke(this, "Hello World from the network!", TestInt, NetworkServer.connections[0]);
    }

    //This is the method that runs on the target client of the above TargetRpc
    public void HelloFromTheNetwork(string message, int value)
    {
        Plugin.Log.LogMessage($"{this} - {message} / {value}");
    }
}

using Mirror;

namespace MirrorPlumber;

public class ExampleNetBehaviour : NetworkBehaviour
{
    internal static Plumber GeneratedCommand = new();
    internal static Plumber<string, int> GeneratedRPC = new();
    internal int TestInt = 0;


    private void Awake()
    {
        GeneratedCommand.Create(typeof(ExampleNetBehaviour), nameof(CmdSendHello), PlumberBase.NetType.Command);
        GeneratedCommand.AddListener(RpcShowHelloMessage);

        GeneratedRPC.Create(typeof(ExampleNetBehaviour), nameof(RpcShowHelloMessage), PlumberBase.NetType.TargetRpc);
        GeneratedRPC.AddListener(HelloFromTheNetwork);

    }

    private void Start()
    {
        SendHelloWorld();
        TestInt = UnityEngine.Random.Range(0, 1337);
    }

    // Call this from any client to send message to all clients
    public void SendHelloWorld()
    {
        Plugin.Log.LogMessage("Hello world WITH plumbing");
        CmdSendHello();
    }

    [Command(requiresAuthority = false)]
    public void CmdSendHello()
    {
        // This runs on the server
        Plugin.Log.LogMessage($"{this} - CmdSendHello");

        // Server tells all clients to show the message
        GeneratedCommand?.Invoke(this);
    }

    [ClientRpc]
    public void RpcShowHelloMessage()
    {
        // This runs on ALL clients
        Plugin.Log.LogMessage($"{this} - RpcShowHelloMessage");

        GeneratedRPC?.Invoke(this, "Hello World from the network!", TestInt, NetworkServer.connections[0]);
    }

    public void HelloFromTheNetwork(string message, int value)
    {
        Plugin.Log.LogMessage($"{this} - {message} / {value}");
    }
}

# Mirror Plumber

**Utility BepInEx Mod that allows modders to insert and use new NetworkActions (Commands, ClientRpcs, and TargetRpcs)**  

- This was built for YAPYAP, however, it should work in any other unity game that utilizes the same style of Mirror Networking.  

## Mirror Network Prefabs 
 *Note: These steps assume you have some prior Unity modding knowledge.*  
 *If you do not need a background on Mirror Networking and just want to know how to use this utility, skip to ``Using MirrorPlumber for your NetworkBehaviour``*

### Create a GameObject that will serve as your Network Prefab containg your NetworkBehaviour  
 - This game object should at the minimum contain a ``Networkidentity`` component.  
	- Your class inheriting NetworkBehaviour should also be attached to this game object either during in Unity or added during runtime.  
	- You cannot add a ``Networkidentity`` component at runtime because the assetId property will not be set properly.  
 - Once you have created your Prefab with the ``NetworkIdentity`` component, build it into an asset bundle.  

### Loading your GameObject Assetbundle as a Network Prefab  
 - Load your asset bundle containing your game object at Plugin Awake. If you have not added your Network Behaviour classes already, add them as components now.  
	- You'll want to cache your GameObject as it will be used in a few other places.  
 - Either at Plugin Start or at another appropriate time (before hosting a game session), you'll want to then use ``NetworkClient.RegisterPrefab`` on your game object.  
	- You'll also need to cache the assetId by performing ``GetComponent<NetworkIdentity>().assetId`` after you've registered your prefab.  
    
### Spawning your Network Prefab  
 - Once your prefab has been properly registered, you can now spawn it once a game session has been started and the server is active.  
	- Ensure you are ONLY spawning the object from the server client.  
 - To spawn a NetworkPrefab you will Instantiate a copy of your cached GameObject and then run ``NetworkServer.Spawn`` on the copy with your cached assetId.  

### YAPYAP Simple NetworkPrefab Load/Spawn Example

```
        internal static ManualLogSource Log { get; private set; } = null!;
        internal static GameObject Networker = null!;
        internal static uint NetworkerID = default!;

        private void Awake()
        {
            Log = Logger;

            Log.LogInfo($"Plugin {Name} is loaded!");

            //Networker Bundle
            string networkAsset = Path.Combine(Path.GetDirectoryName(Info.Location), "networker");
            AssetBundle networker = AssetBundle.LoadFromFile(networkAsset);
            Networker = networker.LoadAsset<GameObject>("Networker");
            Networker.AddComponent<NetworkingTestWithPlumbing>();
            GameManager.OnPlayerSpawned += OnPawnSpawn;
        }

        private void Start()
        {
            Log.LogMessage("Registering network prefabs");
            NetworkClient.RegisterPrefab(Networker);
            NetworkerID = Networker.GetComponent<NetworkIdentity>().assetId;
            Log.LogDebug($"NetworkerID - {NetworkerID}");
        }

        private static void OnPawnSpawn(Pawn pawn)
        {
            Log.LogMessage($"Pawn spawned {pawn.PlayerName}");

            if (!pawn.isLocalPlayer)
                return;

            if (pawn.isServer)
            {
                Log.LogDebug("Spawning networker");
                var networker = Object.Instantiate(Networker);
                NetworkServer.Spawn(networker, NetworkerID);
            }
        }
```

**In the above example:**
 - The NetworkBehaviour ``NetworkingTestWithPlumbing`` is added to the Networker GameObject at Plugin Awake  
 - Networker is then registered as a Network Prefab in Plugin Start and the assetId is cached as NetworkerID
 - OnPlayerSpawn is listening to the GameManager.OnPlayerSpawned event. When a player is spawned, it checks if the player is the server and then also spawns the testing networker object.

### Where MirrorPlumber comes in
 - Without MirrorPlumber, this is where you would find that the various Attributes ``[ClientRpc]``, ``[Command]``, etc. are not actually utilizing Mirror to send the information across the network.  
 - You *could* perform your own plumbing and register each and every NetworkAction yourself following the logic for ``RemoteProcedureCalls.RegisterDelegate`` or ``RemoteProcedureCalls.RegisterRpc`` or `RemoteProcedureCalls.RegisterCommand`  
    - However this process is incredibly tedious and will certainly cause you some headaches.  
 - MirrorPlumber does all of this manual plumbing for you. You'll just need to perform a few steps in your NetworkBehaviour to start the process.  
 
 ## Using MirrorPlumber for your NetworkBehaviour
 - To start, it is recommended to create a static reference to each Plumber you create.
    - This is because you will be the one to invoke these network actions in your code, so you'll need a reference to it.
    - Example: 
 ```
    internal static Plumber GeneratedCommand = null!;
    internal static Plumber<string, int> GeneratedRPC = null!;
 ```

 - Creating the Plumbers is recommended to be done in your NetworkBehaviour's Awake method, however it can be done any time after the prefab holding your NetworkBehaviour has been spawned.  
    - You create the Plumber with a simple constructor that takes the following parameters:
        - ``System.Type netBehaviour`` You'll provide this by converting your NetworkBehaviour to a System.Type - ie: ``typeof(MyNetworkBehaviour)``
        - ``string methodName`` This is the name of the Method you are performing plumbing for - ie: ``nameof(MyNetworkedMethod)``
        - ``NetType netType`` This is an enum created by MirrorPlumber to help identify what kind of NetworkAction you are requesting plumbing for.
        - ``bool requiresAuthority = false`` (Optional) This bool determines if the NetworkAction requires ownership of the Network Prefab before being used. 
    - NOTE: You cannot invoke Network Actions without having created your Plumber.
    - Once you have created your Plumber, you should then add the method you wish to run when the NetworkAction is received via ``AddListener``. 
      - This can be multiple methods so long as each one has the number of parameters expected with the correct type.
- Example: 
 ``` 
    private void Awake()
    {
        GeneratedCommand = new(typeof(NetworkingTestWithPlumbing), nameof(CmdSendHello), PlumberBase.NetType.Command);
        GeneratedCommand.AddListener(RpcShowHelloMessage);

        GeneratedRPC = new(typeof(NetworkingTestWithPlumbing), nameof(RpcShowHelloMessage), PlumberBase.NetType.TargetRpc);
        GeneratedRPC.AddListener(HelloFromTheNetwork);
    }
 ```
 - Now that you have your Plumbers created, all you need to do now is Invoke them wherever you would typically call the method with the NetworkAction.
    - It's recommended to use a [null conditional operator](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/member-access-operators) before invoking your Plumber.
    - Below are some of the parameters you can expect in the Plumber's Invoke method:
        - ``TSource instance`` This is the instance of your NetworkBehaviour that is calling this method.
        - ``NetworkConnection target = null!`` (OPTIONAL) If your NetworkAction is a TargetRpc, you'll need to set this to the client you wish to send the NetworkAction to.
        - ``TParam param`` or ``TParam1 param1`` or ``TParam2 param2`` All of these potential parameters are values of the parameter types you are sending to other clients.
    - Examples: ``GeneratedCommand?.Invoke(this);`` ``GeneratedRPC?.Invoke(this, "Hello World from the network!", 1337, NetworkServer.connections[0]);``
        - Both of these examples are called from inside non-static methods inside my NetworkBehaviour ``NetworkingTestWithPlumbing``
    - For information on what types can be passed in TParams over to Mirror's NetworkWriter/NetworkReader, please see [this page](https://github.com/MirrorNetworking/MirrorDocs/blob/main/manual/guides/data-types.md) of Mirror's Documentation.
        - Some games may also have custom NetworkWriter/NetworkReaders that you can utilize, YAPYAP does not and you cannot add new ones via a Mod (they require Weaving from Mirror)
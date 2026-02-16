using System;
using Mirror;
using Mirror.RemoteCalls;

namespace MirrorPlumber;

/// <summary>
/// Delegate that is used to invoke NetworkEvent actions for MirrorPlumber subscribers with 0 parameters.
/// </summary>
public delegate void NetworkEvent();
/// <summary>
/// Delegate that is used to invoke NetworkEvent actions for MirrorPlumber subscribers with 1 parameter.
/// </summary>
public delegate void NetworkEvent<T>(T param);
/// <summary>
/// Delegate that is used to invoke NetworkEvent actions for MirrorPlumber subscribers with 2 parameters.
/// </summary>
public delegate void NetworkEvent<T1, T2>(T1 param1, T2 param2);

/// <summary>
/// This is the plumber base that all Plumber types use
/// </summary>
/// <remarks>
/// You should not ever be creating a PlumberBase on it's own. It will be created when creating your specificly typed Plumber instance.
/// </remarks>
public abstract class PlumberBase
{
    internal Type? type;
    internal string? _fullName { get; private set; }
    internal ushort _hash { get; private set; }
    internal bool _requiresAuth { get; private set; } = false;
    internal bool _includeOwner { get; private set; } = false;
    internal RemoteCallType _callType { get; private set; }
    internal NetType _netType { get; private set; }

    //ensure we only ever register this once!
    internal bool IsRegistered = false;

    /// <summary>This determines the type of Mirror Remote Action you are requesting plumbing for.</summary>
    /// <remarks>
    /// For more information on the different types, see https://github.com/MirrorNetworking/MirrorDocs/blob/main/manual/guides/communications/remote-actions.md
    /// </remarks>
    public enum NetType
    {
        /// <summary>
        /// Commands are sent from player objects on the client to player objects on the server. 
        /// For security, Commands can only be sent from YOUR player object by default, so you cannot control the objects of other players. 
        /// You can bypass the authority check using [Command(requiresAuthority = false)].
        /// </summary>
        Command,
        /// <summary>
        /// ClientRpc calls are sent from objects on the server to objects on clients. 
        /// They can be sent from any server object with a NetworkIdentity that has been spawned.
        /// Since the server has authority, then there no security issues with server objects being able to send these calls.
        /// </summary>
        ClientRpc,
        /// <summary>
        /// TargetRpc functions are called by user code on the server, and then invoked on the corresponding client object on the client of the specified NetworkConnection. 
        /// The arguments to the RPC call are serialized across the network, so that the client function is invoked with the same values as the function on the server. 
        /// </summary>
        TargetRpc
    }

    /// <summary>
    /// This method method will immediately Create and Register your Plumber with Mirror. It's recommended to do this at your NetworkBehaviour's Awake method.
    /// </summary>
    /// <param name="netBehaviour">The System.Type converison of your NetworkBehaviour. ie. typeof(MyNetworkBehaviour)</param>
    /// <param name="methodName">This is the name of the method you wish to perform plumbing for. ie. nameof(MyNetworkedMethod)</param>
    /// <param name="netType">This is the type of Mirror Network Event you wish to perform plumbing for.</param>
    /// <param name="requiresAuthority">If enabled, only the host client can invoke this network event</param>
    /// <remarks>
    /// This method can be run as much as you want, however only the first time will it actually perform any actions with Mirror.
    /// </remarks>
    public void Create(Type netBehaviour, string methodName, NetType netType, bool requiresAuthority = false)
    {
        if (IsRegistered)
            return;

        type = netBehaviour;
        _fullName = GetFullName(type, methodName);
        _hash = GetHash(_fullName);
        _requiresAuth = requiresAuthority;
        _netType = netType;

        Register();
    }

    //This method should be called on the constructor method for classes that inherit this one
    internal void Register()
    {
        Plugin.Log.LogDebug($"Registering {_fullName} with mirror");

        if (_netType == NetType.Command)
            _callType = RemoteCallType.Command;
        else
            _callType = RemoteCallType.ClientRpc;

        if (_netType == NetType.Command)
            RemoteProcedureCalls.RegisterDelegate(type, _fullName, _callType, StandardCommand, _requiresAuth);
        else
            RemoteProcedureCalls.RegisterDelegate(type, _fullName, _callType, StandardRPC, _requiresAuth);

        IsRegistered = true;
    }

    internal abstract void StandardCommand(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection);
    internal abstract void StandardRPC(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection);

    // Utility method for packing and sending the remote action based on what type of action it is 
    internal void SendToMirror<T>(T instance, NetworkWriterPooled writer, NetworkConnection target = null!) where T : NetworkBehaviour
    {
        if (instance == null)
        {
            Plugin.Log.LogError($"{(typeof(T))} instance is Null!");
            return;
        }

        if (_netType == NetType.Command)
            instance.SendCommandInternal(_fullName, _hash, writer, 0, _requiresAuth);
        else if (_netType == NetType.ClientRpc)
            instance.SendRPCInternal(_fullName, _hash, writer, 0, _includeOwner);
        else if (_netType == NetType.TargetRpc && target != null)
            instance.SendTargetRPCInternal(target, _fullName, _hash, writer, 0);
        else
        {
            Plugin.Log.LogWarning($"{_fullName}: TargetRpc Invoke missing target parameter!");
            return;
        }

        NetworkWriterPool.Return(writer);
    }

    // Used as part of registering the Network Event with mirror, this emulates Mirror's standard registration
    internal static string GetFullName(Type type, string methodName)
    {
        return $"{type?.GetMethod(methodName)?.ReturnType} {type?.Namespace}::{methodName}";
    }

    // Used as part of registering the Network Event with mirror, this emulates Mirror's standard registration
    internal static ushort GetHash(string fullName)
    {
        return (ushort)(fullName.GetStableHashCode() & 0xFFFF);
    }

    /// <summary>
    /// Set includeOwner property for sending Rpcs.
    /// </summary>
    /// <param name="includeOwner"></param>
    /// <remarks>Owner refers to the network object's owner, not the sender of the ClientRpc</remarks>
    public void SetRpcIncludeOwner(bool includeOwner)
    {
        _includeOwner = includeOwner;
    }
}

/// <summary>
/// This is your standard plumber for any Network Event that does not use any parameters
/// </summary>
/// <remarks>
/// You should define your Plumber for your NetworkBehaviour type at Awake. As it will be registered as soon as it is defined.
/// If you do not define the Plumber in Awake, make sure you define it before you try to run any NetworkAction methods.
/// </remarks>
public class Plumber : PlumberBase
{
    private event NetworkEvent NetworkedEvent = null!;

    /// <summary>
    /// Add an additional method as a listener to this Network Event.
    /// </summary>
    /// <param name="listener">The method you wish to run when your Network Event is called</param>
    /// <remarks>
    /// If a listener is added more than once it will run more than once.
    /// Listeners WILL persist after destruction if not cleared.
    /// Use <see cref="ClearListeners"/> to clear any existing listeners that have been destroyed before adding any new listeners.
    /// If defining your listener in your NetworkBehaviour awake, it is recommended to use <see cref="SetListener"/> for the first listener 
    /// <see cref="SetListener"/> will clear any existing listeners to avoid adding duplicates or listeners with destroyed components.
    /// </remarks>
    public void AddListener(NetworkEvent listener)
    {
        NetworkedEvent += listener;
    }

    /// <summary>
    /// Clear any existing listeners for this Network Event and set to a new one.
    /// </summary>
    /// <param name="listener">The method you wish to run when your Network Event is called. It must take two parameters with the types TParam1 and TParam2 (in that order)</param>
    /// <remarks>
    /// When setting listeners for your network event, this should be used to specify your event's first listener. 
    /// Any additional listeners should be set with <see cref="AddListener"/>
    /// </remarks>
    public void SetListener(NetworkEvent listener)
    {
        NetworkedEvent = null!;
        NetworkedEvent += listener;
    }

    /// <summary>
    /// Clear all listeners from this Network Event.
    /// </summary>
    public void ClearListeners()
    {
        NetworkedEvent = null!;
    }

    /// <summary>
    /// This Method handles the rest of Mirror's plumbing for you and invokes the NetworkedEvent your methods are listening to.
    /// </summary>
    /// <param name="instance">The instance of your NetworkBehaviour Type that is calling this method</param>
    /// <param name="target">If this NetworkAction is a TargetRpc, specify the target of the Rpc here</param>
    public void Invoke<TSource>(TSource instance, NetworkConnection target = null!) where TSource : NetworkBehaviour
    {
        NetworkWriterPooled writer = NetworkWriterPool.Get();
        SendToMirror(instance, writer, target);
    }

    internal override void StandardRPC(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
    {
        if (!NetworkClient.active)
        {
            Plugin.Log.LogError($"{_fullName} called on inactive client.");
        }
        else
        {
            NetworkedEvent?.Invoke();
        }
    }

    internal override void StandardCommand(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
    {
        if (!NetworkServer.active)
        {
            Plugin.Log.LogError($"Attempted to call {_fullName} when NetworkServer is not active.");
        }
        else
        {
            NetworkedEvent?.Invoke();
        }
    }
}

/// <summary>
/// This is a Plumber for any Network Event that takes one parameter
/// </summary>
/// <typeparam name="TParam">This is the type your network event takes as a parameter.</typeparam>
/// <remarks>
/// TParam can be any type that Mirror can successfully read/write with. For more information see https://github.com/MirrorNetworking/MirrorDocs/blob/main/manual/guides/data-types.md
/// You should define your Plumber for your NetworkBehaviour type at Awake. As it will be registered as soon as it is defined.
/// If you do not define the Plumber in Awake, make sure you define it before you try to run any NetworkAction methods.
/// </remarks>
public class Plumber<TParam> : PlumberBase
{
    private event NetworkEvent<TParam> NetworkedEvent = null!;

    /// <summary>
    /// Add an additional method as a listener to this Network Event.
    /// </summary>
    /// <param name="listener">The method you wish to run when your Network Event is called. It must take a parameter of the same type as TParam</param>
    /// <remarks>
    /// If a listener is added more than once it will run more than once.
    /// Listeners WILL persist after destruction if not cleared.
    /// Use <see cref="ClearListeners"/> to clear any existing listeners that have been destroyed before adding any new listeners.
    /// If defining your listener in your NetworkBehaviour awake, it is recommended to use <see cref="SetListener"/> for the first listener 
    /// <see cref="SetListener"/> will clear any existing listeners to avoid adding duplicates or listeners with destroyed components.
    /// </remarks>
    public void AddListener(NetworkEvent<TParam> listener)
    {
        NetworkedEvent += listener;
    }

    /// <summary>
    /// Clear any existing listeners for this Network Event and set to a new one.
    /// </summary>
    /// <param name="listener">The method you wish to run when your Network Event is called. It must take two parameters with the types TParam1 and TParam2 (in that order)</param>
    /// <remarks>
    /// When setting listeners for your network event, this should be used to specify your event's first listener. 
    /// Any additional listeners should be set with <see cref="AddListener"/>
    /// </remarks>
    public void SetListener(NetworkEvent<TParam> listener)
    {
        NetworkedEvent = null!;
        NetworkedEvent += listener;
    }

    /// <summary>
    /// Clear all listeners from this Network Event.
    /// </summary>
    public void ClearListeners()
    {
        NetworkedEvent = null!;
    }

    /// <summary>
    /// This Method handles the rest of Mirror's plumbing for you and invokes the NetworkedEvent your methods are listening to.
    /// </summary>
    /// <typeparam name="TSource">This is your NetworkBehaviour Type which this is most likely being called from</typeparam>
    /// <param name="instance">The instance of your NetworkBehaviour Type that is calling this method</param>
    /// <param name="param">The value of type TParam you are sending over the network</param>
    /// <param name="target">If this NetworkAction is a TargetRpc, specify the target of the Rpc here</param>
    /// <remarks>
    /// param can NOT be a null value!
    /// </remarks>
    public void Invoke<TSource>(TSource instance, TParam param, NetworkConnection target = null!) where TSource : NetworkBehaviour
    {
        NetworkWriterPooled writer = NetworkWriterPool.Get();

        if (param == null)
        {
            Plugin.Log.LogError("Given Parameter is null! Mirror cannot read/write NULL values!");
            return;
        }

        //give mirror writer the param
        writer.Write(param);

        SendToMirror(instance, writer, target);
    }

    internal override void StandardRPC(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
    {
        if (!NetworkClient.active)
        {
            Plugin.Log.LogError($"{_fullName} called on inactive client.");
        }
        else
        {
            NetworkedEvent?.Invoke(reader.Read<TParam>());
        }
    }

    internal override void StandardCommand(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
    {
        if (!NetworkServer.active)
        {
            Plugin.Log.LogError($"Attempted to call {_fullName} when NetworkServer is not active.");
        }
        else
        {
            NetworkedEvent?.Invoke(reader.Read<TParam>());
        }
    }
}

/// <summary>
/// This is a Plumber for any Network Event that takes two parameters
/// </summary>
/// <typeparam name="TParam1">This is the first type your network event takes as a parameter</typeparam>
/// <typeparam name="TParam2">This is the second type your network event takes as a parameter</typeparam>
/// <remarks>
/// TParam1 and TParam2 can be any type that Mirror can successfully read/write with. For more information see https://github.com/MirrorNetworking/MirrorDocs/blob/main/manual/guides/data-types.md
/// You should define your Plumber for your NetworkBehaviour type at Awake. As it will be registered as soon as it is defined.
/// If you do not define the Plumber in Awake, make sure you define it before you try to run any NetworkAction methods.
/// </remarks>
public class Plumber<TParam1, TParam2> : PlumberBase
{
    private event NetworkEvent<TParam1, TParam2> NetworkedEvent = null!;

    /// <summary>
    /// Add an additional method as a listener to this Network Event.
    /// </summary>
    /// <param name="listener">The method you wish to run when your Network Event is called. It must take two parameters with the types TParam1 and TParam2 (in that order)</param>
    /// <remarks>
    /// If a listener is added more than once it will run more than once.
    /// Listeners WILL persist after destruction if not cleared.
    /// Use <see cref="ClearListeners"/> to clear any existing listeners that have been destroyed before adding any new listeners.
    /// If defining your listener in your NetworkBehaviour awake, it is recommended to use <see cref="SetListener"/> for the first listener 
    /// <see cref="SetListener"/> will clear any existing listeners to avoid adding duplicates or listeners with destroyed components.
    /// </remarks>
    public void AddListener(NetworkEvent<TParam1, TParam2> listener)
    {
        NetworkedEvent += listener;
    }

    /// <summary>
    /// Clear any existing listeners for this Network Event and set to a new one.
    /// </summary>
    /// <param name="listener">The method you wish to run when your Network Event is called. It must take two parameters with the types TParam1 and TParam2 (in that order)</param>
    /// <remarks>
    /// When setting listeners for your network event, this should be used to specify your event's first listener. 
    /// Any additional listeners should be set with <see cref="AddListener"/>
    /// </remarks>
    public void SetListener(NetworkEvent<TParam1, TParam2> listener)
    {
        NetworkedEvent = null!;
        NetworkedEvent += listener;
    }

    /// <summary>
    /// Clear all listeners from this Network Event.
    /// </summary>
    public void ClearListeners()
    {
        NetworkedEvent = null!;
    }

    /// <summary>
    /// This Method handles the rest of Mirror's plumbing for you and invokes the NetworkedEvent your methods are listening to.
    /// </summary>
    /// <typeparam name="TSource">This is your NetworkBehaviour Type which this is most likely being called from</typeparam>
    /// <param name="instance">The instance of your NetworkBehaviour Type that is calling this method</param>
    /// <param name="param1">The value of type TParam1 you are sending over the network</param>
    /// <param name="param2">The value of type TParam2 you are sending over the network</param>
    /// <param name="target">If this NetworkAction is a TargetRpc, specify the target of the Rpc here</param>
    /// <remarks>
    /// param1 amd param2 can NOT be null!
    /// </remarks>
    public void Invoke<TSource>(TSource instance, TParam1 param1, TParam2 param2, NetworkConnection target = null!) where TSource : NetworkBehaviour
    {
        NetworkWriterPooled writer = NetworkWriterPool.Get();

        if (param1 == null)
        {
            Plugin.Log.LogError("Parameter 1 is null! Mirror cannot read/write NULL values!");
            return;
        }

        if (param2 == null)
        {
            Plugin.Log.LogError("Parameter 2 is null! Mirror cannot read/write NULL values!");
            return;
        }

        //give mirror writer the params
        writer.Write(param1);
        writer.Write(param2);

        SendToMirror(instance, writer, target);
    }

    internal override void StandardRPC(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
    {
        if (!NetworkClient.active)
        {
            Plugin.Log.LogError($"{_fullName} called on inactive client.");
        }
        else
        {
            NetworkedEvent?.Invoke(reader.Read<TParam1>(), reader.Read<TParam2>());
        }
    }

    internal override void StandardCommand(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection)
    {
        if (!NetworkServer.active)
        {
            Plugin.Log.LogError($"Attempted to call {_fullName} when NetworkServer is not active.");
        }
        else
        {
            NetworkedEvent?.Invoke(reader.Read<TParam1>(), reader.Read<TParam2>());
        }
    }
}

/// <summary>
/// This is a class that will hold a networked value and sync it over the network when you set it.
/// </summary>
/// <typeparam name="TSource"></typeparam>
/// <typeparam name="TValue"></typeparam>
/// <remarks>
/// While this does behave similarly to a SyncVar, this is not a native Mirror syncvar. 
/// It will hold a reference of your value and sync it over the network via a Command/ClientRpc combination.
/// </remarks>
public class PlumbVar<TSource, TValue>(TValue initialValue) where TSource : NetworkBehaviour
{
    /// <summary>
    /// This event is invoked whenever the value is changed over the network.
    /// </summary>
    /// <remarks>
    /// You can subscribe to this event with any method that takes the same parameter as the value you are tracking.
    /// For more information on subscribing to events, see https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/events/how-to-subscribe-to-and-unsubscribe-from-events
    /// </remarks>
    public event Action<TValue> OnValueChanged = null!;
    private TSource? SourceInstance = default;
    private static readonly Plumber<TValue> CommandUpdateValue = new();
    private static readonly Plumber<TValue> ClientRpcUpdateValue = new();
    private readonly TValue initVal = initialValue;
    private TValue _value = initialValue;

    /// <summary>
    /// This is the value holder for your PlumbVar.
    /// </summary>
    /// <remarks>
    /// When getting the value, you are returned your current reference value (no networking is done).
    /// When setting the value, your changes are synced over the network to all other clients.
    /// If you are receiving a networked change, your reference value will be updated and any listeners to the OnValueChanged event will be invoked.
    /// </remarks>
    public TValue Value
    {
        get
        {
            return _value;
        }
        set
        {
            _value = value;
            if (SourceInstance != null)
            {
                if (SourceInstance.isServer)
                    ClientRpcUpdateValue?.Invoke(SourceInstance, value);
                else
                    CommandUpdateValue?.Invoke(SourceInstance, value);
            }
        }
    }

    /// <summary>
    /// This method will Create your PlumbVar instance.
    /// </summary>
    /// <param name="NetBehaviourInstance">This is the instance of your NetworkBehaviour</param>
    /// <remarks>
    /// This method is recommended to be run in your NetworkBehaviour's Awake method.
    /// The command and clientrpc associated to syncing this value across the network is set here.
    /// </remarks>
    public void Create(TSource NetBehaviourInstance)
    {
        Plugin.Log.LogDebug($"Creating PlumbVar for {NetBehaviourInstance}!");
        _value = initVal;
        SourceInstance = NetBehaviourInstance;
        CommandUpdateValue.Create(typeof(TSource), $"{typeof(TSource).Name} {nameof(TValue)} PlumbVar Command", PlumberBase.NetType.Command);
        CommandUpdateValue.SetListener(UpdateValue);
        ClientRpcUpdateValue.Create(typeof(TSource), $"{typeof(TSource).Name} {nameof(TValue)} PlumbVar ClientRpc", PlumberBase.NetType.ClientRpc);
        ClientRpcUpdateValue.SetListener(UpdateValue);
    }

    private void UpdateValue(TValue value)
    {
        _value = value;
        OnValueChanged?.Invoke(value);
    }
}
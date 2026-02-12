using System;
using System.Linq;
using Mirror;
using Mirror.RemoteCalls;

namespace MirrorPlumber;

public delegate void NetworkEvent();
public delegate void NetworkEvent<T>(T param);
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
        Command,
        ClientRpc,
        TargetRpc
    }

    /// <summary>
    /// This method method will immediately Create & Register your Plumber with Mirror. It's recommended to do this at your NetworkBehaviour's Awake method.
    /// </summary>
    /// <param name="netBehaviour">The System.Type converison of your NetworkBehaviour. ie. typeof(MyNetworkBehaviour)</param>
    /// <param name="methodName">This is the name of the method you wish to perform plumbing for. ie. nameof(MyNetworkedMethod)</param>
    /// <param name="netType">This is the type of Mirror Network Action you wish to perform plumbing for.</param>
    /// <param name="requiresAuthority">If enabled, only the host client can invoke this network action</param>
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
        if(instance == null)
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

    // Used as part of registering the network action with mirror, this emulates Mirror's standard registration
    internal static string GetFullName(Type type, string methodName)
    {
        return $"{type?.GetMethod(methodName)?.ReturnType} {type?.Namespace}::{methodName}";
    }

    // Used as part of registering the network action with mirror, this emulates Mirror's standard registration
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
/// This is your standard plumber for any network action that does not use any parameters
/// </summary>
/// <remarks>
/// You should define your Plumber for your NetworkBehaviour type at Awake. As it will be registered as soon as it is defined.
/// If you do not define the Plumber in Awake, make sure you define it before you try to run any NetworkAction methods.
/// </remarks>
public class Plumber : PlumberBase
{
    private event NetworkEvent NetworkedEvent = null!;

    /// <summary>
    /// Add a method as a listener to this Network Action.
    /// </summary>
    /// <param name="listener">The method you wish to run when your Network Action is called</param>
    /// <remarks>
    /// There is handling here not to add the same listener more than once.
    /// </remarks>
    public void AddListener(NetworkEvent listener)
    {
        if (NetworkedEvent.GetInvocationList().Contains(listener))
            return;

        NetworkedEvent += listener;
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
/// This is a Plumber for any Network Action that takes one parameter
/// </summary>
/// <typeparam name="TParam">This is the type your network action takes as a parameter.</typeparam>
/// <remarks>
/// TParam can be any type that Mirror can successfully read/write with. For more information see https://github.com/MirrorNetworking/MirrorDocs/blob/main/manual/guides/data-types.md
/// You should define your Plumber for your NetworkBehaviour type at Awake. As it will be registered as soon as it is defined.
/// If you do not define the Plumber in Awake, make sure you define it before you try to run any NetworkAction methods.
/// </remarks>
public class Plumber<TParam> : PlumberBase
{
    private event NetworkEvent<TParam> NetworkedEvent = null!;

    /// <summary>
    /// Add a method as a listener to this Network Action.
    /// </summary>
    /// <param name="listener">The method you wish to run when your Network Action is called. It must take a parameter of the same type as TParam</param>
    public void AddListener(NetworkEvent<TParam> listener)
    {
        NetworkedEvent += listener;
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

        if(param == null)
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
/// This is a Plumber for any Network Action that takes two parameters
/// </summary>
/// <typeparam name="TParam1">This is the first type your network action takes as a parameter</typeparam>
/// <typeparam name="TParam2">This is the second type your network action takes as a parameter</typeparam>
/// <remarks>
/// TParam1 & TParam2 can be any type that Mirror can successfully read/write with. For more information see https://github.com/MirrorNetworking/MirrorDocs/blob/main/manual/guides/data-types.md
/// You should define your Plumber for your NetworkBehaviour type at Awake. As it will be registered as soon as it is defined.
/// If you do not define the Plumber in Awake, make sure you define it before you try to run any NetworkAction methods.
/// </remarks>
public class Plumber<TParam1, TParam2> : PlumberBase
{
    private event NetworkEvent<TParam1, TParam2> NetworkedEvent = null!;

    /// <summary>
    /// Add a method as a listener to this Network Action.
    /// </summary>
    /// <param name="listener">The method you wish to run when your Network Action is called. It must take two parameters with the types TParam1 and TParam2 (in that order)</param>
    public void AddListener(NetworkEvent<TParam1, TParam2> listener)
    {
        NetworkedEvent += listener;
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

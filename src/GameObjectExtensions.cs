using Mirror;
using UnityEngine;

namespace MirrorPlumber;

/// <summary>
/// MirrorPlumber GameObject Extensions class
/// </summary>
public static class GameObjectExtensions
{
    /// <summary>
    /// Register your GameObject prefab with Mirror and get it's NetworkIdentity assetId for spawning later
    /// </summary>
    /// <param name="prefab">Your game object you wish to register with Mirror</param>
    /// <param name="assetId">This is your game object's NetworkIdentity assetId that is used for spawning it later</param>
    /// <returns>If the prefab is null or does not contain a NetworkIdentity this will return false</returns>
    public static bool TryRegisterPrefab(this GameObject prefab, out uint assetId)
    {
        assetId = 0;

        if(prefab == null)
            return false;

        if (!prefab.GetComponent<NetworkIdentity>())
            return false;

        NetworkClient.RegisterPrefab(prefab);
        assetId = prefab.GetComponent<NetworkIdentity>().assetId;
        Plugin.Log.LogDebug($"{prefab.name} registered with Mirror");
        return true;
    }
}

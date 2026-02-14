using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace MirrorPlumber;

/// <summary>
/// MirrorPlumber GameObject Extensions class
/// </summary>
public static class GameObjectExtensions
{
    internal static List<GameObject> RegisteredPrefabs = [];

    /// <summary>
    /// Attempts to add your game object to an internal MirrorPlumber list of game objects that will be registered at NetworkClient.Initialize.
    /// assetId is no longer necessary but this overload is still included for backwards compatibility
    /// </summary>
    /// <param name="prefab">Your game object you wish to register with Mirror</param>
    /// <param name="assetId">Provides you the assetId in case you wish to reference it in your project.</param>
    /// <returns>If the prefab is null or does not contain a NetworkIdentity this will return false</returns>
    public static bool TryRegisterPrefab(this GameObject prefab, out uint assetId)
    {
        prefab.TryGetAssetId(out assetId);
        if (prefab == null)
            return false;

        if (!prefab.GetComponent<NetworkIdentity>())
            return false;

        RegisteredPrefabs.Add(prefab);
        
        Plugin.Log.LogDebug($"{prefab.name} will be registered with Mirror at NetworkClient.Initialize!");
        return true;
    }

    /// <summary>
    /// Attempts to add your game object to an internal MirrorPlumber list of game objects that will be registered at NetworkClient.Initialize.
    /// </summary>
    /// <param name="prefab">Your game object you wish to register with Mirror</param>
    /// <returns>If the prefab is null or does not contain a NetworkIdentity this will return false</returns>
    public static bool TryRegisterPrefab(this GameObject prefab)
    {
        if (prefab == null)
            return false;

        if (!prefab.GetComponent<NetworkIdentity>())
            return false;

        RegisteredPrefabs.Add(prefab);

        Plugin.Log.LogDebug($"{prefab.name} will be registered with Mirror at NetworkClient.Initialize!");
        return true;
    }

    /// <summary>
    /// This method will return your game object's assetId if it exists.
    /// </summary>
    /// <param name="prefab">Your game object</param>
    /// <param name="assetId">The assetId from the NetworkIdentity component of your game object</param>
    /// <returns>If game object is null or does not have a NetworkIdentity, returns false</returns>
    public static bool TryGetAssetId(this GameObject prefab, out uint assetId)
    {
        assetId = 0;

        if (prefab == null)
            return false;

        if (!prefab.GetComponent<NetworkIdentity>())
            return false;

        assetId = prefab.GetComponent<NetworkIdentity>().assetId;

        return true;
    }

    /// <summary>
    /// Remove your prefab from the internal MirrorPlumber list of game objects that will be registered at NetworkClient.Initialize
    /// </summary>
    /// <param name="prefab">The prefab you wish to remove from the  internal MirrorPlumber list</param>
    /// <returns>If the prefab is null, does not contain a NetworkIdentity, or is not already in the list it will return false.</returns>
    /// <remarks>
    /// This does not unregister prefabs with Mirror directly.
    /// </remarks>
    public static bool TryUntrackPrefab(this GameObject prefab)
    {
        if (prefab == null)
            return false;

        if (!prefab.GetComponent<NetworkIdentity>())
            return false;

        bool isRemoved = RegisteredPrefabs.Remove(prefab);
        Plugin.Log.LogDebug($"{prefab.name} has been removed from MirrorPlumber's RegisteredPrefabs list [{isRemoved}]");
        return isRemoved;
    }

    /// <summary>
    /// Attempts to spawn a given prefab on the server
    /// </summary>
    /// <param name="prefab">The game object to spawn on the server</param>
    /// <returns>Returns false if prefab is null or NetworkClient does not contain the prefab in it's prefabs list</returns>
    /// <remarks>
    /// Make sure to add any components or changes to your prefab before this method is run.
    /// </remarks>
    public static bool TrySpawnOnServer(this GameObject prefab)
    {
        if (!NetworkServer.active)
        {
            Plugin.Log.LogWarning($"Unable to spawn {prefab.name} on the server as we are not the server client!");
            return false;
        }

        if(!NetworkClient.prefabs.ContainsValue(prefab))
        {
            Plugin.Log.LogError($"Unable to spawn {prefab.name} on the server! It does not exist in the prefabs list!");
            return false;
        }

        GameObject prefabCopy = GameObject.Instantiate(prefab);
        NetworkServer.Spawn(prefabCopy);
        return true;
    }
}

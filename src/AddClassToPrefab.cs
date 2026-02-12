using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace MirrorPlumber;

/// <summary>
/// This class allows you to add your own custom NetworkBehaviour class to an existing Network Prefab
/// </summary>
public class AddClassToPrefab
{
    private static readonly List<KeyValuePair<AddClassToPrefab, uint>> PrefabAddons = []; //in this format so pairs dont need unique keys
    private static readonly List<AddClassToPrefab> PlayerPrefabAddons = [];
    private bool IsPlayerPrefab = false;
    private Type? NewBehaviour = null!;

    internal static bool Frozen = false;

    /// <summary>
    /// Add your custom NetworkBehaviour to the Player Prefab
    /// </summary>
    /// <typeparam name="T">This is your class that inherits NetworkBehaviour</typeparam>
    /// <remarks>
    /// The class will be added to the Player Prefab during the freeze event, which fires before NetworkManager Awake
    /// NOTE: This networkbehaviour will be added to EVERY player (not just the local player), keep this in mind when doing your networking.
    /// </remarks>
    public void AddToPlayerPrefab<T>() where T : NetworkBehaviour
    {
        IsPlayerPrefab = true;
        NewBehaviour = typeof(T);
        PlayerPrefabAddons.Add(this);
    }

    /// <summary>
    /// Add your custom NetworkBehaviour to an existing Network Prefab that is NOT the Player Prefab
    /// </summary>
    /// <typeparam name="T">This is your class that inherits NetworkBehaviour</typeparam>
    /// <param name="assetId">This is the existing prefabs' NetworkIdentity assetId property</param>
    /// <remarks>
    /// Use unity explorer to find the assetId of the existing prefab you wish to add a NetworkBehaviour to.
    /// The class will be added to the Network Prefab during the freeze event, which fires before NetworkManager Awake
    /// </remarks>
    public void AddToPrefab<T>(uint assetId) where T : NetworkBehaviour
    {
        NewBehaviour = typeof(T);
        PrefabAddons.Add(new(this, assetId));
    }

    internal static void FreezeEvent(NetworkManager networkManager)
    {
        if (Frozen)
            return;

        Plugin.Log.LogDebug(">>>AddClassToPrefab Freeze Event<<<");

        if (networkManager.playerPrefab.GetComponent<NetworkIdentity>() == null)
        {
            Plugin.Log.LogWarning("""
                    Player Prefab does not have a NetworkIdentity!!
                    Our added NetworkBehaviors will not work and have not been added
                    Freeze event canceled.
                    """);
            return;
        }

        int playerComponentsAdded = 0;
        int spawnPrefabsModified = 0;
        foreach(AddClassToPrefab component in PlayerPrefabAddons)
        {
            if (component.NewBehaviour == null || !component.IsPlayerPrefab)
                continue;

            Type type = component.NewBehaviour;

            if (networkManager.playerPrefab.GetComponent(type) != null)
                continue;

            networkManager.playerPrefab.AddComponent(type);
            playerComponentsAdded++;
        }

        if(PrefabAddons.Count != 0)
        {
            var SafeDict = GetNetIdentities(networkManager);

            foreach (KeyValuePair<AddClassToPrefab, uint> pair in PrefabAddons)
            {
                if (pair.Key.IsPlayerPrefab || pair.Key.NewBehaviour == null)
                    continue;

                if (!SafeDict.TryGetValue(pair.Value, out NetworkIdentity match))
                    continue;

                Type type = pair.Key.NewBehaviour;

                if (match.GetComponent(type) != null)
                    continue;

                match.gameObject.AddComponent(type);
                Plugin.Log.LogInfo($"Added {type.Name} component to {pair.Value} Network Prefab!");
                spawnPrefabsModified++;
            }
        }

        Frozen = true;
        Plugin.Log.LogDebug($"Added [ {playerComponentsAdded} ] NetworkBehaviours to the PlayerPrefab.");
        Plugin.Log.LogDebug($"Added [ {spawnPrefabsModified} ] NetworkBehaviours to Network Objects in the SpawnPrefabs list.");
    }

    private static Dictionary<uint, NetworkIdentity> GetNetIdentities(NetworkManager networkManager)
    {
        Dictionary<uint, NetworkIdentity> safe = [];
        foreach (GameObject gameObject in networkManager.spawnPrefabs)
        {
            var netIdentity = gameObject.GetComponent<NetworkIdentity>();
            if (netIdentity != null)
            {
                safe.Add(netIdentity.assetId, netIdentity);
            }
        }

        return safe;
    }
}

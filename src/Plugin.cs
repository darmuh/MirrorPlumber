using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace MirrorPlumber;

[BepInAutoPlugin]
internal partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;
        Harmony harmony = new("com.github.darmuh.MirrorPlumber");
        harmony.PatchAll();
        Log.LogInfo($"Plugin {Name} is loaded and ready to go!");
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Awake))]
    public class NetworkManagerAwake
    {
        public static void Prefix(NetworkManager __instance)
        {
            Log.LogDebug("NetworkManager Awake Prefix");
            BehaviourAdder.FreezeEvent(__instance);
        }
    }

    [HarmonyPatch(typeof(NetworkClient), nameof(NetworkClient.Initialize))]
    public class Initialize
    {
        public static void Prefix()
        {
            Log.LogDebug("NetworkClient Initialize Prefix");
            foreach (GameObject gameObject in GameObjectExtensions.RegisteredPrefabs)
            {
                if (!NetworkClient.prefabs.ContainsValue(gameObject))
                {
                    Log.LogMessage("registering prefab");
                    NetworkClient.RegisterPrefab(gameObject);
                }
            }
        }
    }
}

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Mirror;

namespace MirrorPlumber;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;
        Harmony harmony = new("com.github.darmuh.MirrorPlumber");
        harmony.PatchAll();
        Log.LogInfo($"Plugin {Name} is loaded!");
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Awake))]
    public class NetworkManagerAwake
    {
        public static void Prefix(NetworkManager __instance)
        {
            Log.LogDebug("NetworkManager Prefix");
            BehaviourAdder.FreezeEvent(__instance);
        }
    }
}

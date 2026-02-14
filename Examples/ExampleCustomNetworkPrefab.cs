using System.IO;
using BepInEx;
using BepInEx.Logging;
using MirrorPlumber;
using UnityEngine;

namespace MyMod;

[BepInPlugin("MyModGUID", "MYMODNAME", "0.0.1")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static GameObject Networker = null!;
    internal static uint NetworkerID = default!;

    private void Awake()
    {
        Log = new("MYMOD");

        Log.LogInfo($"Plugin {Info.Metadata.Name} is loaded!");  
    }

    private void Start()
    {
        //Networker Bundle
        string networkAsset = Path.Combine(Path.GetDirectoryName(Info.Location), "networker");
        AssetBundle networker = AssetBundle.LoadFromFile(networkAsset);
        Networker = networker.LoadAsset<GameObject>("Networker");
        Networker.AddComponent<ExampleNetBehaviour>();
        Log.LogMessage("Registering network prefabs");
        Log.LogMessage($"Networker will be registered with Mirror - {Networker.TryRegisterPrefab(out NetworkerID)}";
    }
}
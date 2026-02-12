# Mirror Plumber

**Utility BepInEx Mod that allows modders to utilize various Mirror Networking functions at runtime**  

This was built for YAPYAP, however, it should work in any other unity game that utilizes the same style of Mirror Networking.  

 ### Features:  
 - Plumber class which facilitates all the internal plumbing Mirror requires to get a NetworkBehaviour working at runtime.  
	- Mirror does not recognize it's Attribute tags at runtime so you'll need to utilize this Plumber (or perhaps a patcher in the future) in order to get things working.  
	- Currently supports Commands, ClientRpcs, and TargetRpcs.  
	- SyncVar support is a planned feature, pending I can get a decent implementation working.  

 - BehaviourAdder class handles adding custom classes that inherit NetworkBehaviour to existing Prefabs.  
	- Currently supports adding custom NetworkBehaviour classes to the Player Prefab and any prefab in NetworkManager's spawnPrefabs list.  
	- The classes are hooked into the prefabs during a NetworkManager Awake prefix patch.   
	- You will need to supply the assetId for any non-player prefab you wish to add a custom NetworkBehaviour class to.   
	- NOTE: Prefabs cannot contain more than 64 NetworkBehaviours  
	- NOTE 2: You will still need to have MirrorPlumber perform the plumbing of your NetworkBehaviour class for it to properly network.
    - NOTE 3: When adding a NetworkBehaviour to the Player Prefab, keep in mind your NetworkBehaviour will be on *every* instance of the Player Prefab. Not just the local instance. 

 - GameObjectExtensions class which holds useful game object extension methods that pertain to Mirror and MirrorPlumber.  
	- ``TryRegisterPrefab`` Register your GameObject prefab with Mirror and get it's NetworkIdentity assetId for spawning later  
		- If the prefab is null or does not contain a NetworkIdentity this will return false  
		- When true, provides you the NetworkIdentity assetId. You should cache this value so you can use it to spawn the prefab over the network later.  

 ### Examples:  
 - Network Behaviour using MirrorPlumber - [ExampleNetBehaviour.cs](https://github.com/darmuh/MirrorPlumber/blob/master/Examples/ExampleNetBehaviour.cs)  
 - Loading a Network Prefab with MirrorPlumber - [ExampleCustomNetworkPrefab.cs](https://github.com/darmuh/MirrorPlumber/blob/master/Examples/ExampleCustomNetworkPrefab.cs)  
 - Example Mod using MirrorPlumber - [SkeletonCostume](https://thunderstore.io/c/yapyap/p/darmuh/SkeletonCostume/)

 ### Documentation:  
  - Documentation is WIP, for now you can view the [old readme](https://github.com/darmuh/MirrorPlumber/blob/master/Wiki/Old-README.md) which had a lot of good but poorly organized information.  
  - It is HIGHLY recommended to reference [Mirror's own documentation](https://mirror-networking.gitbook.io/docs)
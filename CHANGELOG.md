# MirrorPlumber Changelog

## 0.3.0
 - Updated project to generate xml documentation file.
 - Updated AutoPlugin version in project.
 - Added PlumbVar which acts as a psuedo syncvar (using an internal command and clientrpc)
	- Defined with the class name, value type, and initial value
	- Get and set the value via the ``Value`` property. Setting the value will send your changes over the network.
 - Found and provided fix for issue of NetworkEvent delegate listeners persisting past class destruction.
	- In order to clear these delegates with destroyed (null) references, you can now use the ``ClearListeners`` method in your NetworkBehaviour's ``OnDestroy`` method.
	- You can also swap to ``SetListener`` in place of ``AddListener`` that will automatically clear existing listeners before adding your new listener.
	- To add additional listeners for one networkevent you can still utilize AddListener, just ensure it is *after* ``ClearListeners`` or ``SetListener``
 - Found and provided fix for issue where NetworkPrefabs loaded from asset bundles will get cleared from the NetworkClient when they are no longer hosting.
	- This is due to Mirror natively running ``NetworkClient.ClearSpawners`` during ``NetworkClient.Shutdown`` (in yapyap this method is called when closing a hosted lobby)
	- The extension method ``TryRegisterPrefab`` has been updated to handle registration for you. 
		- MirrorPlumber will keep a list of gameobjects that have been queued for registration and ensure they've been registered with NetworkClient at ``NetworkClient.Initialize``
		- An overload exists that does not provide the assetId as it's no longer necessary to keep track of.
 - New game object extension method can be used to remove a prefab from MirrorPlumber's prefab game object list via ``TryUntrackPrefab``
	- This will not remove the prefab from an active server, but will ensure it is not in the prefab list for the next time NetworkClient is initialized.
 - New game object extension method for getting a prefab's assetId
 - New game object extension method for spawning a prefab on the server.
 - Examples updated with latest changes
 - Readme updates for latest version.

## 0.2.1
 - removed sample classes from compiler that I accidentally included in last build

## 0.2.0
 - Moved Plumber registration from constructor to ``Create`` method (breaking change from 0.1.1)
	- This was to solve an issue where Commands/Rpcs were trying to be re-added to Mirror after a lobby reload.
	- Now rather than creating a new Plumber every awake, it should be defined only once and then ``Create`` can be ran multiple times without any issues.
 - Added Examples folder to github.
	- This should help the visual learners who learn strictly thru code.
 - Added ``BehaviourAdder`` class for adding network behaviours to existing prefabs at runtime.
	- Handles both the PlayerPrefab and any spawnPrefab in the list that you can match to an assetId
 - Added ``GameObjectExtensions`` class for useful extension methods relating to Mirror/MirrorPlumber
	- ``TryRegisterPrefab`` will attempt to take your GameObject prefab and register it with MirrorClient
		- Returns true/false and provides you the NetworkIdentity assetId when successful.
 - Some msbuild project changes to make building the release package a bit easier

## 0.1.1
 - Fixed some typos in the readme
 - Removed some old debugging logs for more accurate ones in Plumber.cs

## 0.1.0
 - Initial release.

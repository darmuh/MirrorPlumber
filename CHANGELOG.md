# MirrorPlumber Changelog

## 0.2.0
 - Moved Plumber registration from constructor to ``Create`` method (breaking change from 0.1.1)
	- This was to solve an issue where Commands/Rpcs were trying to be re-added to Mirror after a lobby reload.
	- Now rather than creating a new Plumber every awake, it should be defined only once and then ``Create`` can be ran multiple times without any issues.
 - Added Examples folder to github.
	- This should help the visual learners who learn strictly thru code.
 - Added ``AddClassToPrefab`` class for adding network behaviours to existing prefabs at runtime.
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

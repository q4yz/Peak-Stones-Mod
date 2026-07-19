using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections;
using System.IO; // Required for Path combining
using Photon.Pun; // Required for Network Pool


namespace Stones;

[BepInAutoPlugin]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource logger { get; private set; } = null!;
    
    public static ConfigEntry<int> MaxStones { get; private set; } = null!;
    public static ConfigEntry<float> SpawnRadius { get; private set; } = null!;
    
    public static ConfigEntry<bool> EnableVolcanoEvent { get; private set; } = null!;
    public static ConfigEntry<float> VolcanoEventInterval { get; private set; } = null!;
    public static ConfigEntry<float> StoneRainDropRate { get; private set; } = null!;
    public static ConfigEntry<float> StoneDamageThreshold { get; private set; } = null!;

    private bool hasLoadedWorld = false;
    private bool isRaining = false;
    private void Awake()
    {
        logger = Logger;
        logger.LogInfo($"Plugin Stones is loaded!");
        
        AddConfigs();
        LoadAssetBundle();

        Harmony.CreateAndPatchAll(typeof(Plugin));

        
        PrintAllItems();
    }
    
    
    private void LoadAssetBundle()
    {
        string bundlePath = Path.Combine(Paths.PluginPath, "cubestone");
        
        if (!File.Exists(bundlePath))
        {
            logger.LogError($"Could not find AssetBundle at: {bundlePath}");
            return;
        }

        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        GameObject rawPrefab = bundle.LoadAsset<GameObject>("Cube");
        
        if (rawPrefab != null)
        {
            GameObject networkedPrefab = UnityEngine.Object.Instantiate(rawPrefab);
            networkedPrefab.name = "Cube"; 

// Prevent it from being destroyed when loading new maps
            UnityEngine.Object.DontDestroyOnLoad(networkedPrefab);

// THE FIX: Move the master prefab far underground instead of deactivating it!
            networkedPrefab.transform.position = new Vector3(0f, -9999f, 0f);

// 2. Attach the REAL PhotonView natively from the game's assembly
            PhotonView pv = networkedPrefab.AddComponent<PhotonView>();

            // 3. Register our modified clone to the network pool
            DefaultPool pool = PhotonNetwork.PrefabPool as DefaultPool;
            if (pool != null && !pool.ResourceCache.ContainsKey("Cube"))
            {
                pool.ResourceCache.Add("Cube", networkedPrefab);
                logger.LogInfo("Successfully registered custom 'Cube' prefab to the network!");
            }
        }
        else
        {
            logger.LogError("Failed to extract 'Cube' from the AssetBundle!");
        }
    }

    

    private void AddConfigs()
    {
        MaxStones = Config.Bind("1. Spawning", "MaxStones", 50, "The maximum number of items allowed in the world.");
        SpawnRadius = Config.Bind("1. Spawning", "SpawnRadius", 150f, "How far out from the center of the map items can spawn.");
        
        EnableVolcanoEvent = Config.Bind("2. Events", "EnableVolcanoEvent", true, "Set to true to allow random volcanic eruptions.");
        VolcanoEventInterval = Config.Bind("2. Events", "VolcanoEventInterval", 300.0f, "How often (in seconds) the volcano erupts. Default is 300.");
        StoneRainDropRate = Config.Bind("2. Events", "StoneRainDropRate", 0.5f, "How fast the stones fall during the rain.");
        
        StoneDamageThreshold = Config.Bind("3. Combat", "StoneDamageThreshold", 10.0f, "How fast a stone must be traveling to deal damage to a player.");
    }
    
   

    private void Update()
    {
       
        // Listen for the F2 key
        if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F2))
        {
            logger.LogInfo("F2 pressed! Attempting to spawn Coconut and Cube...");

            if (Player.localPlayer != null && Player.localPlayer.character != null)
            {
                Transform playerTransform = Player.localPlayer.character.transform;
        
                // Base height 5 units in the air
                Vector3 basePos = playerTransform.position + new Vector3(0f, 5f, 0f);
        
                // Offset the Coconut to the left and the Cube to the right
                Vector3 coconutPos = basePos + (playerTransform.right * -1.5f);
                Vector3 cubePos = basePos + (playerTransform.right * 1.5f);
                
                UnityEngine.Vector3 spawnPos = playerTransform.position + (playerTransform.forward * 2f) + (UnityEngine.Vector3.up * 1f);

                logger.LogInfo($"Spawning Coconut at: {coconutPos}");
                // IMPORTANT: Change "Coconut" back to whatever the exact string was originally!
                Photon.Pun.PhotonNetwork.Instantiate("0_Items/Item_Coconut", spawnPos, UnityEngine.Quaternion.identity, 0);
        
                logger.LogInfo($"Spawning custom Cube at: {cubePos}");
                Photon.Pun.PhotonNetwork.Instantiate("Cube", spawnPos, Quaternion.identity, 0);
        
                logger.LogInfo("Both instantiation commands sent to Photon!");
            }
            else
            {
                logger.LogWarning("Spawn failed: Local player or character is null! Are you fully loaded into a map?");
            }
        }
        
        if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F3))
        {
            if (EnableVolcanoEvent.Value && !isRaining)
            {
                if (Player.localPlayer != null && Player.localPlayer.character != null)
                {
                    logger.LogInfo("F3 Pressed: Starting Volcano Rain Coroutine!");
                    StartCoroutine(VolcanoRainRoutine());
                }
            }
            else if (isRaining)
            {
                logger.LogWarning("It is already raining stones! Wait for the event to finish.");
            }
        }
        
        if (!hasLoadedWorld && Photon.Pun.PhotonNetwork.InRoom && Photon.Pun.PhotonNetwork.IsMasterClient)
        {
            // Make sure the local player character has actually loaded into the scene
            if (Player.localPlayer != null && Player.localPlayer.character != null)
            {
                OnLoadingWorld();
                hasLoadedWorld = true; // Set flag to true so this never runs again
            }
        }
    }
    
    private IEnumerator VolcanoRainRoutine()
    {
        isRaining = true;
        
        logger.LogInfo("Volcano Event Triggered: The earth shakes and the sky turns red!");

        // Temporarily change the ambient light to red for atmosphere
        Color originalAmbient = RenderSettings.ambientLight;
        RenderSettings.ambientLight = Color.red;

        // --- NEW: SCREEN SHAKE LOGIC ---
        // Grab the main camera to shake it
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // Save the original position so we can snap it back later
            Vector3 originalCamPos = mainCam.transform.localPosition;
            float shakeDuration = 2.0f;  // Shake lasts for 2 seconds
            float shakeMagnitude = 0.3f; // How violent the shake is
            float elapsed = 0.0f;

            // Loop every frame until the shake duration is over
            while (elapsed < shakeDuration)
            {
                float x = UnityEngine.Random.Range(-1f, 1f) * shakeMagnitude;
                float y = UnityEngine.Random.Range(-1f, 1f) * shakeMagnitude;

                // Apply the random offset to the camera
                mainCam.transform.localPosition = new Vector3(originalCamPos.x + x, originalCamPos.y + y, originalCamPos.z);

                elapsed += Time.deltaTime;
                yield return null; // Wait exactly one frame before looping again
            }

            // Snap the camera back to perfectly center when the shake is done
            mainCam.transform.localPosition = originalCamPos;
        }
        else
        {
            logger.LogWarning("Could not find Camera.main for screen shake!");
        }

        // --- NEW: THE 5 SECOND DELAY ---
        logger.LogInfo("Eruption building... Waiting 5 seconds...");
        yield return new WaitForSeconds(5.0f);
        
        logger.LogInfo("Incoming!!!");

        // How many items drop during the event (you can make this a config setting later!)
        int itemsToDrop = 20;

        for (int i = 0; i < itemsToDrop; i++)
        {
            // Always get the player's current position so the rain follows them if they run
            UnityEngine.Transform playerTransform = Player.localPlayer.character.transform;
            
            // Pick a random spot in a 25-meter radius around the player
            float randomOffsetX = UnityEngine.Random.Range(-25f, 25f);
            float randomOffsetZ = UnityEngine.Random.Range(-25f, 25f);
            
            // Spawn height is 40 meters straight up in the air
            UnityEngine.Vector3 rainSpawnPos = playerTransform.position + new UnityEngine.Vector3(randomOffsetX, 40f, randomOffsetZ);

            Photon.Pun.PhotonNetwork.Instantiate("0_Items/Item_Coconut", rainSpawnPos, UnityEngine.Random.rotation, 0);
            
            // Pause the loop based on your config setting before dropping the next one
            yield return new WaitForSeconds(StoneRainDropRate.Value);
        }

        // Clean up the event
        RenderSettings.ambientLight = originalAmbient;
        isRaining = false;
        logger.LogInfo("The volcano rain has stopped.");
    }

    private void OnLoadingWorld()
    {
        SpawnInitialWorldItems();
    }
    
    
    
    private void SpawnInitialWorldItems()
    {
        logger.LogInfo($"Starting one-time world generation. Spawning {MaxStones.Value} items...");

        int successfullySpawned = 0;

        for (int i = 0; i < MaxStones.Value; i++)
        {
            // Pick a random X and Z coordinate within your spawn radius
            float randomX = UnityEngine.Random.Range(-SpawnRadius.Value, SpawnRadius.Value);
            float randomZ = UnityEngine.Random.Range(-SpawnRadius.Value, SpawnRadius.Value);

            // Create a starting point high up in the sky (200 meters up)
            UnityEngine.Vector3 skyPosition = new UnityEngine.Vector3(randomX, 200f, randomZ);

            // Shoot a raycast straight down to find the ground
            // 500f is the max distance the laser will travel
            if (UnityEngine.Physics.Raycast(skyPosition, UnityEngine.Vector3.down, out UnityEngine.RaycastHit hit, 500f))
            {
                // We hit the ground! Spawn the coconut exactly slightly above the hit point so it doesn't clip into the floor
                UnityEngine.Vector3 groundPosition = hit.point + (UnityEngine.Vector3.up * 0.5f);

                Photon.Pun.PhotonNetwork.Instantiate("0_Items/Item_Coconut", groundPosition, UnityEngine.Random.rotation, 0);
                successfullySpawned++;
            }
        }

        logger.LogInfo($"World generation complete! Successfully spawned {successfullySpawned} items.");
    }
    
    private void PrintAllItems()
    {
        Item[] allGameItems = UnityEngine.Resources.LoadAll<Item>("0_Items");
    
        foreach(Item item in allGameItems)
        {
            logger.LogInfo("Found Item: " + item.gameObject.name + " | UI Name: " + item.UIData.itemName);
        }
        
    }
}
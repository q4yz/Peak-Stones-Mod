using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PEAKLib.Core;
using PEAKLib.Items;
using PEAKLib.Items.UnityEditor;
using UnityEngine;
using Photon.Pun;

namespace Stones;

[BepInAutoPlugin]
[BepInDependency(CorePlugin.Id)]
[BepInDependency(ItemsPlugin.Id)]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource logger { get; private set; } = null!;
    private const string HarmonyId = "q4y.Stones";

 
    internal static string ModId { get; private set; } = null!;
    
    public static PeakBundle PeakBundle { get; private set; } = null!;
    private const string BundleFileName = "stones.peakbundle";
    

    
    internal static readonly StonesItem[] StoneTiers =
    {
        new StonesItem("PebbleContent",  "Item_Small_Stone"),
        new StonesItem("RockContent",    "Item_Medium_Stone"),
        new StonesItem("BoulderContent", "Item_Big_Stone"),
        new StonesItem("LargeBoulderContent", "Item_Very_Big_Stone"),
    };
    
    internal static readonly StonesItem[] StormStoneTiers =
    {
        new StonesItem("StormPebbleContent",  "Item_Small_Storm_Stone"),
        new StonesItem("StormRockContent",    "Item_Medium_Storm_Stone"),
        new StonesItem("StormBoulderContent", "Item_Big_Storm_Stone"),
        new StonesItem("StormLargeBoulderContent", "Item_Very_Big_Storm_Stone"),
    };
    
    
    internal static readonly StonesItem[] OtherItems =
    {
        new StonesItem("GrenadeContent",  "Item_Grenade"),
        new StonesItem("ExplosionContent",  "Explosion"),
    };
    

    private void Awake()
    {
        logger = Logger;
        ModId = Info.Metadata.GUID;
        logger.LogInfo($"Plugin Stones is loaded! (GUID = {ModId})");

        StonesConfig.Bind(Config);
        Localization.CILocalization();
        
        new Harmony(HarmonyId).PatchAll(typeof(Plugin).Assembly);

        VulcanManager.EnsureInstance();
        this.LoadBundleWithName(BundleFileName, RegisterStonesContent);
        
        
    }
    
    private void RegisterStonesContent(PeakBundle peakBundle)
    {
        Plugin.PeakBundle = peakBundle;
        ModLogger.LogInfo($"[Stones] Registering stone content from '{BundleFileName}'...");
        
        void ProcessTierList(StonesItem[] tiers, Action<UnityItemContent, string> attachBehaviors)
        {
            foreach (StonesItem tier in tiers)
            {
                UnityItemContent content = peakBundle.LoadAsset<UnityItemContent>(tier.ContentName);
                if (content == null)
                {
                    ModLogger.LogError(
                        $"[Stones] UnityItemContent '{tier.ContentName}' not found in bundle '{BundleFileName}'. " +
                        "Re-author the asset in the Unity Editor and re-export the bundle.");
                    continue;
                }

                // Execute the specific behavior attachments passed in
                attachBehaviors(content, tier.ContentName);
            }
        }

        // Process standard stones
        ProcessTierList(StoneTiers, (content, name) =>
        {
            AttachBehavior<StoneBehavior>(content, name);
            AttachBehavior<MapStoneBehavior>(content, name);
        });

        // Process storm stones
        ProcessTierList(StormStoneTiers, (content, name) =>
        {
            AttachBehavior<StoneBehavior>(content, name);
            AttachBehavior<StormStoneBehavior>(content, name);
        });
        
        UnityItemContent content = peakBundle.LoadAsset<UnityItemContent>("ExplosionContent");
        AttachBehavior<AutoLightBehavior>(content, "ExplosionContent");
        
        content = peakBundle.LoadAsset<UnityItemContent>("GrenadeContent");
        AttachBehavior<GrenadeCookBehavior>(content, "GrenadeContent");
        
        ProcessTierList(OtherItems, (content, name) => { });

        // "0_Items/{mod.Id}:{item.name}".
        peakBundle.Mod.RegisterContent();

        ModLogger.LogInfo("[Stones] PEAKLib content registration batch complete.");
    }

    /// <summary>
    /// Attaches <see cref="StoneBehavior"/> to the content's
    /// <c>ItemPrefab</c> if it isn't already present. 
    /// </summary>
    private static void AttachBehavior<T>(UnityItemContent content, string contentName) where T : UnityEngine.Component
    {
        if (content == null) return;

        GameObject prefab = content.ItemPrefab;
        if (prefab == null)
        {
            ModLogger.LogError(
                $"[Stones] UnityItemContent '{contentName}' has no ItemPrefab assigned. " +
                "Re-author the asset in the Unity Editor.");
            return;
        }

        if (prefab.GetComponent<T>() == null)
        {
            prefab.AddComponent<T>();
            ModLogger.LogInfo($"[Stones] Attached {typeof(T).Name} to '{contentName}' ItemPrefab.");
        }
    }

    private void Update()
    {
        #if DEBUG
        if (Input.GetKeyDown(KeyCode.F2)) HandleF2();
        if (Input.GetKeyDown(KeyCode.F3)) HandleF3();
        if (Input.GetKeyDown(KeyCode.F4)) HandleF4();
        #endif
    }

    /// <summary>
    /// Master-client-only F2 debug spawn.
    /// </summary>
    private void HandleF2()
    {

        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }
        if (Player.localPlayer == null || Player.localPlayer.character == null)
        {
            ModLogger.LogWarning("F2: Local player or character is null - not in a map?");
            return;
        }

        Vector3 playerVektor = Player.localPlayer.character.Center;
        Vector3 playerRight = Player.localPlayer.character.transform.right;
        Vector3 chestPos = playerVektor + Vector3.up * 1f;
        
        ModLogger.LogInfo(
            "F2 pressed! Spawning one random stone + all four tiers " +
            "(Pebble, Rock, Boulder, LargeBoulder) in a row...");

        Vector3 randomPos = chestPos + playerRight * -3.0f;
        GameObject? randomStone = ItemSpawnHelper.SpawnRandomStone(randomPos, Quaternion.identity);
        if (randomStone == null)
        {
            ModLogger.LogError(
                "[Stones] SpawnRandomStone returned null - none of the stone " +
                "tiers are registered by PEAKLib?");
            return;
        }
        ItemSpawnHelper.LogSpawned("F2 (random)", randomStone, randomPos);
        SpawnStoneRow(chestPos, playerVektor, playerRight);
    }

    /// <summary>
    /// Spawns all four stone tiers in a horizontal row
    /// (Pebble, Rock, Boulder, LargeBoulder) 1.5 m apart, in front of the
    /// player at chest height.
    /// </summary>
    private void SpawnStoneRow(Vector3 chestPos, Vector3 playerVektor, Vector3 playerRight)
    {
        float[] offsets = { -2.25f, -0.75f, 0.75f, 2.25f };
        for (int i = 0; i < StoneTiers.Length; i++)
        {
            var tier = StoneTiers[i];
            Vector3 stonePos = chestPos + playerRight * offsets[i];
            
            ModLogger.LogInfo($"Spawning {tier.PrefabName} at: {stonePos}");
            GameObject? stone = ItemSpawnHelper.SpawnStone(tier.PrefabName, stonePos, Quaternion.identity);
            if (stone == null)
            {
                ModLogger.LogError($"[Stones] SpawnStone returned null - '{tier.PrefabName}' not registered by PEAKLib?");
                continue;
            }
            ItemSpawnHelper.LogSpawned($"F2 (row: {tier.PrefabName})", stone, stonePos);
        }
    }

    /// <summary>
    /// Cycles the F2 debug spawn mode: <c>all-four</c> → <c>Pebble</c> →
    /// <c>Rock</c> → <c>Boulder</c> → <c>LargeBoulder</c> → <c>all-four</c>.
    /// Every F2 press also drops a random stone alongside the deterministic
    /// spawn(s).
    /// </summary>
    private void HandleF4()
    {
        
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }
        if (Player.localPlayer != null && Player.localPlayer.character != null)
        {
            Vector3 playerPos = Player.localPlayer.character.Center;
            ModLogger.LogInfo($"[Stones] DEBUG F4: Player Center is exactly at X={playerPos.x:F2}, Y={playerPos.y:F2}, Z={playerPos.z:F2}");
        }
        else if (Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;
            ModLogger.LogInfo($"[Stones] DEBUG F4: Camera is exactly at X={camPos.x:F2}, Y={camPos.y:F2}, Z={camPos.z:F2}");
        }
    }

    private void HandleF3()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }
        if (!StonesConfig.EnableVolcanoEvent.Value)
        {
            ModLogger.LogInfo("F3: forcing a volcanic outbreak for debugging even though EnableVolcanoEvent is false.");
        }

        VulcanManager manager = VulcanManager.EnsureInstance();
        ModLogger.LogInfo("F3 pressed: forcing the volcanic outbreak immediately for debugging.");
        manager.StartVulcanOutbreak();
    }
}


internal readonly struct StonesItem
{
    public readonly string ContentName;
    public readonly string PrefabName;
    
    public StonesItem(string contentName, string prefabName)
    {
        ContentName = contentName;
        PrefabName = prefabName;
    }
}

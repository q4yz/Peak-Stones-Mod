using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using PEAKLib.Core;
using PEAKLib.Items;
using PEAKLib.Items.UnityEditor;
using UnityEngine;

namespace Stones;

/// <summary>
/// Entry point for the Stones BepInEx mod. Owns lifecycle (Awake/Update),
/// config binding, PEAKLib bundle/content registration, Harmony patching,
/// and keyboard input dispatch (F2 / F3 / F4).
///
/// <para>
/// <b>Item registration.</b> All four stone tiers (Pebble, Rock, Boulder, LargeBoulder)
/// are authored in the Unity Editor dummy project as
/// <c>UnityItemContent</c> ScriptableObjects (each holding a GameObject
/// <c>ItemPrefab</c> with baked-in size, mass, collider, and rigidbody),
/// packed into <c>stones.peakbundle</c>, and registered at runtime via the
/// official <c>PEAKLib.Items</c> pipeline: PEAKLib's <c>BundleLoader</c>
/// opens the bundle, we look up each <c>UnityItemContent</c> by name,
/// attach <see cref="StoneBehavior"/> to its <c>ItemPrefab</c>, then call
/// <c>peakBundle.Mod.RegisterContent()</c> to register every
/// <c>IContent</c> in the bundle (the four <c>UnityItemContent</c>
/// assets plus any <c>UnityModDefinition</c>).
/// PEAKLib owns insertion into the native item database and Photon's
/// <c>DefaultPool.ResourceCache</c>. This plugin never touches those
/// collections directly — and never reflects on
/// <c>Item.ALL_ACTIVE_ITEMS</c>, calls runtime <c>AddComponent&lt;Item&gt;()</c>,
/// or pokes Photon's prefab pool.
/// </para>
///
/// <para>
/// <b>No runtime randomization.</b> Sizes and masses live on the prefabs
/// themselves, so there is no loopback RPC for scale or mass — every
/// clone simply inherits the authored values via Unity serialization.
/// </para>
/// </summary>
[BepInAutoPlugin]
[BepInDependency(CorePlugin.Id)]
[BepInDependency(ItemsPlugin.Id)]
public partial class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource logger { get; private set; } = null!;
    private const string HarmonyId = "q4y.Stones";

    /// <summary>
    /// Cached mod GUID (BepInPlugin metadata). Set in <see cref="Awake"/>
    /// so <see cref="ItemSpawnHelper"/> can construct the full Photon
    /// prefab key without an instance reference.
    /// </summary>
    internal static string ModId { get; private set; } = null!;

    /// <summary>
    /// AssetBundle filename as packed by the Unity Editor. Resolved at
    /// runtime relative to <see cref="Paths.PluginPath"/>.
    /// </summary>
    private const string BundleFileName = "stones.peakbundle";
    
    /// <summary>
    /// Debug mode for F2: <c>-1</c> = spawn all four tiers next to each
    /// other; <c>0</c>/<c>1</c>/<c>2</c>/<c>3</c> = spawn only that single
    /// tier (Pebble/Rock/Boulder/LargeBoulder). Toggled with F4.
    /// </summary>
    private int debugTierIndex = -1;

    /// <summary>
    /// Per-tier metadata used by the F2 / F4 debug hotkeys and by
    /// <see cref="ItemSpawnHelper.SpawnRandomStone"/> to pick a random
    /// stone tier.
    ///
    /// <para>
    /// Each tier carries <b>two distinct identifiers</b>:
    /// <list type="bullet">
    /// <item><see cref="StoneTier.ContentName"/> – the name of the
    /// <c>UnityItemContent</c> ScriptableObject as packed into the bundle.
    /// Used to look the asset up via
    /// <c>peakBundle.LoadAsset&lt;UnityItemContent&gt;(...)</c>.</item>
    /// <item><see cref="StoneTier.PrefabName"/> – the name of the
    /// underlying GameObject <c>ItemPrefab</c> (e.g. <c>"rock"</c>).
    /// PEAKLib mutates that GameObject to
    /// <c>"{mod.Id}:{PrefabName}"</c> during registration and exposes
    /// it under <c>"0_Items/{mod.Id}:{PrefabName}"</c> in Photon's
    /// <c>DefaultPool</c>; <see cref="ItemSpawnHelper.SpawnStone"/>
    /// reconstructs that full key at spawn time.</item>
    /// </list>
    /// Mixing these up is the cause of the historic
    /// <c>DefaultPool failed to load "0_Items/q4y.Stones:RockContent"</c>
    /// NullReferenceException: the spawned instance was being requested
    /// under the ScriptableObject name rather than the GameObject name.
    /// </para>
    ///
    /// <para>
    /// Sizes and masses are baked into each prefab in the Unity Editor,
    /// so this struct no longer carries per-tier scale / mass fields —
    /// every clone inherits the authored values via Unity serialization,
    /// and no loopback RPC is required.
    /// </para>
    ///
    /// <para>
    /// The authoritative itemID is assigned by
    /// <c>PEAKLib.Items.ItemRegistrar.FinishRegisterItem</c> as
    /// <c>MD5(mod.Id + item.name).ToUInt16()</c>; this table's
    /// <see cref="StoneTier.ItemId"/> field is just a legacy debug tag.
    /// </para>
    /// </summary>
    internal static readonly StoneTier[] StoneTiers =
    {
        new StoneTier("PebbleContent",  "Item_Small_Stone",  (ushort)2001),
        new StoneTier("RockContent",    "Item_Medium_Stone",    (ushort)2002),
        new StoneTier("BoulderContent", "Item_Big_Stone", (ushort)2003),
        new StoneTier("LargeBoulderContent", "Item_Very_Big_Stone", (ushort)2004),
    };

    private void Awake()
    {
        logger = Logger;
        ModId = Info.Metadata.GUID;
        logger.LogInfo($"Plugin Stones is loaded! (GUID = {ModId})");

        StonesConfig.Bind(Config);

        // Inject display names into the game's localization table BEFORE
        // any item can be displayed in the UI. Idempotent - safe to call
        // on every Awake even if PEAK already populated the key. See
        // Localization.cs for the per-tier 13-entry list.
        Localization.CILocalization();

        // PatchAll(typeof(Assembly)) discovers every [HarmonyPatch] class in
        // the assembly - required so StoneHarmonyPatches gets applied.
        new Harmony(HarmonyId).PatchAll(typeof(Plugin).Assembly);

        VulcanStormManager.EnsureInstance();

        // PEAKLib's BundleLoader extension: invokes the callback once the
        // AssetBundle has been opened and is safe to query. Inside the
        // callback we attach StoneBehavior to each UnityItemContent's
        // ItemPrefab, then peakBundle.Mod.RegisterContent() registers every
        // IContent in the bundle (native item DB + Photon's DefaultPool).
        this.LoadBundleWithName(BundleFileName, RegisterStonesContent);
    }

    /// <summary>
    /// Called by <c>PEAKLib.Core.BundleLoader.LoadBundleWithName</c>
    /// after <c>stones.peakbundle</c> finishes loading. Loads each
    /// <c>UnityItemContent</c> ScriptableObject by name, attaches
    /// <see cref="StoneBehavior"/> to its <c>ItemPrefab</c> if missing,
    /// then registers every <c>IContent</c> in the bundle via
    /// <c>peakBundle.Mod.RegisterContent()</c>.
    ///
    /// <para>
    /// After this method returns, PEAKLib owns insertion into the
    /// native item database and Photon's <c>DefaultPool.ResourceCache</c>.
    /// We do NOT poke either of those collections ourselves.
    /// </para>
    /// </summary>
    private void RegisterStonesContent(PeakBundle peakBundle)
    {
        logger.LogInfo($"[Stones] Registering stone content from '{BundleFileName}'...");

        foreach (StoneTier tier in StoneTiers)
        {
            // Load the UnityItemContent ScriptableObject by its asset
            // name (ContentName). The GameObject name (PrefabName) is the
            // one PEAKLib later mutates and registers to Photon - we do
            // not look the prefab up by it here.
            UnityItemContent content = peakBundle.LoadAsset<UnityItemContent>(tier.ContentName);
            if (content == null)
            {
                logger.LogError(
                    $"[Stones] UnityItemContent '{tier.ContentName}' not found in bundle '" +
                    BundleFileName + "'. Re-author the asset in the Unity Editor " +
                    "and re-export the bundle.");
                continue;
            }

            AttachStoneBehavior(content, tier.ContentName);
        }

        // PEAKLib registers every IContent ScriptableObject in the bundle:
        // the three UnityItemContent assets above, plus any UnityModDefinition.
        // This single call inserts items into the native ItemDatabase,
        // assigns hash-based itemIDs, and registers Photon prefabs under
        // "0_Items/{mod.Id}:{item.name}".
        peakBundle.Mod.RegisterContent();

        logger.LogInfo("[Stones] PEAKLib content registration batch complete.");
    }

    /// <summary>
    /// Attaches <see cref="StoneBehavior"/> to the content's
    /// <c>ItemPrefab</c> if it isn't already present. The behavior owns
    /// the kinetic-energy combat hook (<c>OnCollisionEnter</c>); size
    /// and mass are baked into each prefab in the Unity Editor and need
    /// no runtime broadcasting.
    /// </summary>
    /// <param name="content">The <c>UnityItemContent</c> loaded from the bundle.</param>
    /// <param name="contentName">
    /// <c>UnityItemContent.name</c> as authored in the Unity Editor.
    /// Used only as a diagnostic label in the log message.
    /// </param>
    private static void AttachStoneBehavior(UnityItemContent content, string contentName)
    {
        if (content == null) return;

        GameObject prefab = content.ItemPrefab;
        if (prefab == null)
        {
            logger.LogError(
                $"[Stones] UnityItemContent '{contentName}' has no ItemPrefab assigned. " +
                "Re-author the asset in the Unity Editor.");
            return;
        }

        if (prefab.GetComponent<StoneBehavior>() == null)
        {
            prefab.AddComponent<StoneBehavior>();
            logger.LogInfo($"[Stones] Attached StoneBehavior to '{contentName}' ItemPrefab.");
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2)) HandleF2();
        if (Input.GetKeyDown(KeyCode.F3)) HandleF3();
        //if (Input.GetKeyDown(KeyCode.F4)) HandleF4();
        
        // Press F5 to log your current coordinates to the BepInEx console
        if (Input.GetKeyDown(KeyCode.F4))
        {
            if (Player.localPlayer != null && Player.localPlayer.character != null)
            {
                Vector3 playerPos = Player.localPlayer.character.Center;
                Plugin.logger.LogInfo($"[Stones] DEBUG F5: Player Center is exactly at X={playerPos.x:F2}, Y={playerPos.y:F2}, Z={playerPos.z:F2}");
            }
            else if (Camera.main != null)
            {
                Vector3 camPos = Camera.main.transform.position;
                Plugin.logger.LogInfo($"[Stones] DEBUG F5: Camera is exactly at X={camPos.x:F2}, Y={camPos.y:F2}, Z={camPos.z:F2}");
            }
        }
        
        
        
    }

    /// <summary>
    /// Master-client-only F2 debug spawn. Layout depends on
    /// <see cref="debugTierIndex"/>:
    ///
    /// <list type="bullet">
    /// <item><c>-1</c>: a random stone on the left + the full
    /// Pebble + Rock + Boulder + LargeBoulder row on the right.</item>
    /// <item><c>0/1/2/3</c>: a random stone on the left + the explicitly
    /// chosen tier (<see cref="StoneTier.PrefabName"/>) on the right.</item>
    /// </list>
    ///
    /// <para>
    /// <b>Random stone replaces coconut.</b> The F2 debug key used to
    /// drop a vanilla <c>Item_Coconut</c> as a sentinel; we now drop a
    /// randomly-tiered mod stone via
    /// <see cref="ItemSpawnHelper.SpawnRandomStone"/> instead, so the
    /// debug row exercises the same PEAKLib registration path that the
    /// mod ships with.
    /// </para>
    /// </summary>
    private void HandleF2()
    {
        if (Player.localPlayer == null || Player.localPlayer.character == null)
        {
            logger.LogWarning("F2: Local player or character is null - not in a map?");
            return;
        }

        Vector3 playerVektor = Player.localPlayer.character.Center;
        // Player-local right vector - replaces the Vector3.right workaround
        // so the F2 row stays perpendicular to whichever way the player faces.
        Vector3 playerRight = Player.localPlayer.character.transform.right;

        // Chest height so all spawned items are visible right in front of the camera.
        Vector3 chestPos = playerVektor + Vector3.up * 1f;

        if (debugTierIndex < 0)
        {
            // All-four mode: random stone (left) + Pebble/Rock/Boulder/LargeBoulder row (right).
            logger.LogInfo(
                "F2 pressed! Spawning one random stone + all four tiers " +
                "(Pebble, Rock, Boulder, LargeBoulder) in a row...");

            Vector3 randomPos = chestPos + playerRight * -3.0f;
            GameObject? randomStone = ItemSpawnHelper.SpawnRandomStone(randomPos, Quaternion.identity);
            if (randomStone == null)
            {
                logger.LogError(
                    "[Stones] SpawnRandomStone returned null - none of the stone " +
                    "tiers are registered by PEAKLib?");
                return;
            }
            ItemSpawnHelper.LogSpawned("F2 (random)", randomStone, randomPos);

            SpawnStoneRow(chestPos, playerVektor, playerRight);
        }
        else
        {
            // Single-tier mode: random stone (left) + chosen tier (right).
            var tier = StoneTiers[debugTierIndex];
            logger.LogInfo(
                $"F2 pressed! Spawning random stone + tier '{tier.PrefabName}' " +
                $"(mode = {debugTierIndex}). Press F4 to cycle back to all-four mode.");

            Vector3 randomPos = chestPos + playerRight * -1.5f;
            GameObject? randomStone = ItemSpawnHelper.SpawnRandomStone(randomPos, Quaternion.identity);
            if (randomStone == null)
            {
                logger.LogError(
                    "[Stones] SpawnRandomStone returned null - none of the stone " +
                    "tiers are registered by PEAKLib?");
                return;
            }
            ItemSpawnHelper.LogSpawned("F2 (random)", randomStone, randomPos);

            Vector3 stonePos = chestPos + playerRight * 1.5f;
            // Pass tier.PrefabName (the GameObject's name) into SpawnStone.
            // PEAKLib registered the prefab under that name in Photon's
            // DefaultPool; the previous bug was passing the
            // UnityItemContent ScriptableObject name ("RockContent")
            // which never matched what Photon had cached.
            logger.LogInfo($"Spawning {tier.PrefabName} at: {stonePos}");
            GameObject? stone = ItemSpawnHelper.SpawnStone(tier.PrefabName, stonePos, Quaternion.identity);
            if (stone == null)
            {
                logger.LogError(
                    $"[Stones] SpawnStone returned null - '{tier.PrefabName}' " +
                    "not registered by PEAKLib?");
                return;
            }
            ItemSpawnHelper.LogSpawned($"F2 ({tier.PrefabName})", stone, stonePos);
        }
    }

    /// <summary>
    /// Spawns all four stone tiers in a horizontal row
    /// (Pebble, Rock, Boulder, LargeBoulder) 1.5 m apart, in front of the
    /// player at chest height.
    /// </summary>
    private void SpawnStoneRow(Vector3 chestPos, Vector3 playerVektor, Vector3 playerRight)
    {
        // Four slots, 1.5 m spacing, centered around 0 -> total span 4.5 m.
        float[] offsets = { -2.25f, -0.75f, 0.75f, 2.25f };
        for (int i = 0; i < StoneTiers.Length; i++)
        {
            var tier = StoneTiers[i];
            Vector3 stonePos = chestPos + playerRight * offsets[i];
            // Pass tier.PrefabName (the GameObject's name) into SpawnStone
            // - PEAKLib registered the prefab under that name in Photon's
            // DefaultPool; passing the UnityItemContent ScriptableObject
            // name here previously caused "DefaultPool failed to load" +
            // NullReferenceException on the master client.
            logger.LogInfo(
                $"Spawning {tier.PrefabName} (itemID={tier.ItemId}) at: {stonePos}");
            GameObject? stone = ItemSpawnHelper.SpawnStone(tier.PrefabName, stonePos, Quaternion.identity);
            if (stone == null)
            {
                logger.LogError(
                    $"[Stones] SpawnStone returned null - '{tier.PrefabName}' " +
                    "not registered by PEAKLib?");
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
        debugTierIndex++;
        if (debugTierIndex >= StoneTiers.Length) debugTierIndex = -1;

        if (debugTierIndex < 0)
        {
            logger.LogInfo(
                "F4: F2 mode = spawn one random stone + all four tiers " +
                "(Pebble + Rock + Boulder + LargeBoulder) in a row.");
        }
        else
        {
            var tier = StoneTiers[debugTierIndex];
            logger.LogInfo(
                $"F4: F2 mode = spawn one random stone + a single '{tier.PrefabName}' " +
                $"(itemID={tier.ItemId}).");
        }
    }

    private void HandleF3()
    {
        if (!StonesConfig.EnableVolcanoEvent.Value)
        {
            logger.LogInfo("F3: forcing a volcanic outbreak for debugging even though EnableVolcanoEvent is false.");
        }

        VulcanStormManager manager = VulcanStormManager.EnsureInstance();
        logger.LogInfo("F3 pressed: forcing the volcanic outbreak immediately for debugging.");
        manager.StartVulcanOutbreak();
    }

    private void PrintAllItems()
    {
        Item[] allGameItems = Resources.LoadAll<Item>("0_Items");
        foreach (Item item in allGameItems)
        {
            logger.LogInfo("Found Item: " + item.gameObject.name + " | UI Name: " + item.UIData.itemName);
        }
    }
}

/// <summary>
/// Per-tier metadata used by the F2/F4 debug hotkeys and by
/// <see cref="ItemSpawnHelper.SpawnRandomStone"/> to identify each prefab.
/// Sizes and masses are baked into the prefab itself in the Unity Editor,
/// so this struct no longer carries per-tier scale / mass fields — every
/// clone simply inherits the authored values via Unity serialization.
///
/// <para>
/// The authoritative itemID is assigned by
/// <c>PEAKLib.Items.ItemRegistrar.FinishRegisterItem</c> at registration
/// time as an MD5 hash of <c>{mod.Id}:{item.name}</c>; this struct's
/// <see cref="ItemId"/> field is just a legacy debug tag.
/// </para>
/// </summary>
internal readonly struct StoneTier
{
    /// <summary>
    /// Name of the <c>UnityItemContent</c> ScriptableObject as authored
    /// in the Unity Editor (e.g. <c>"RockContent"</c>). Used as the key
    /// when calling <c>peakBundle.LoadAsset&lt;UnityItemContent&gt;(...)</c>
    /// to pull the asset out of <c>stones.peakbundle</c>.
    /// </summary>
    public readonly string ContentName;

    /// <summary>
    /// Name of the underlying GameObject <c>ItemPrefab</c> as authored
    /// in the Unity Editor (e.g. <c>"rock"</c>). PEAKLib mutates that
    /// GameObject to <c>"{mod.Id}:{PrefabName}"</c> and registers the
    /// Photon prefab under <c>"0_Items/{mod.Id}:{PrefabName}"</c>.
    /// Must be passed to <see cref="ItemSpawnHelper.SpawnStone"/> so
    /// Photon's <c>DefaultPool</c> can resolve the prefab.
    /// </summary>
    public readonly string PrefabName;

    /// <summary>Legacy per-tier debug tag (PEAKLib owns the real itemID).</summary>
    public readonly ushort ItemId;

    // Per-tier scale / mass fields removed: sizes and masses are baked
    // into each prefab in the Unity Editor, so we no longer carry or
    // broadcast them at runtime.

    public StoneTier(string contentName, string prefabName, ushort itemId)
    {
        ContentName = contentName;
        PrefabName = prefabName;
        ItemId = itemId;
    }
}

using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using ExitGames.Client.Photon;
using Photon.Realtime;

namespace Stones;

/// <summary>
/// Master-client-only Harmony patches on the <c>RunManager</c> lifecycle.
/// 
/// 1. StartRun: Triggers the initial stone coordinate generation and first 
/// batch of raycasts exactly once per run.
/// 2. EndGame: Resets the one-shot flag so the next run can scatter fresh stones.
/// </summary>
[HarmonyPatch(typeof(RunManager))]
public static class RunManagerStoneScatterPatches
{
    public const byte CUSTOM_START_RUN_EVENT = 43; 

    // ==========================================
    // INITIALIZATION
    // ==========================================

    // This static constructor runs automatically EXACTLY ONCE when the mod loads.
    // It hooks up our network listener directly, without needing a MonoBehaviour!
    static RunManagerStoneScatterPatches()
    {
        PhotonNetwork.NetworkingClient.EventReceived += OnNetworkEventReceived;
    }

    // ==========================================
    // HARMONY PATCHES (The Hooks)
    // ==========================================

    [HarmonyPatch(nameof(RunManager.StartRun))]
    [HarmonyPostfix]
    public static void StartRun_Postfix()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            ExecuteMasterClientScatterLogic();
            return;
        }
        
        BroadcastStartRunEvent();
    }

    [HarmonyPatch("EndGame")]
    [HarmonyPostfix]
    public static void EndGame_Postfix()
    {
        // 2. Clear the flags so the next run can happen
        ResetScatterFlag();
    }

    // ==========================================
    // NETWORK COMMUNICATION
    // ==========================================

    private static void BroadcastStartRunEvent()
    {
        ModLogger.LogInfo("[RunManager] A player started the run. Broadcasting StartRun event to all clients...");

        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        SendOptions sendOptions = new SendOptions { Reliability = true }; 

        PhotonNetwork.RaiseEvent(CUSTOM_START_RUN_EVENT, null, options, sendOptions);
    }

    private static void OnNetworkEventReceived(EventData photonEvent)
    {
        if (photonEvent.Code != CUSTOM_START_RUN_EVENT) return;

        ModLogger.LogInfo("[RunManager] StartRun network event received!");
        
        if (PhotonNetwork.IsMasterClient)
        {
            ExecuteMasterClientScatterLogic();
        }
    }

    // ==========================================
    // HELPER METHODS (Master Client Logic)
    // ==========================================

    private static void ExecuteMasterClientScatterLogic()
    {
        if (MapStoneSpawner._hasSpawnedThisRun)
        {
            ModLogger.LogInfo("[Stones] Scatter already fired for this run. Skipping duplicate.");
            return;
        }

        MapStoneSpawner._hasSpawnedThisRun = true;
        MapStoneSpawner host = EnsureMapStoneSpawnerExists();

        if (host == null)
        {
            MapStoneSpawner._hasSpawnedThisRun = false;
            ModLogger.LogError("[Stones] Failed to create MapStoneSpawner host GameObject; scatter aborted.");
            return;
        }

        host.InitializeSpawnQueue();

        ModLogger.LogInfo(
            $"[Stones] RunStart_StoneScatter_Patch: triggered queue initialization " +
            $"(host='{host.name}', totalStones={StonesConfig.MaxStones.Value}, " +
            $"X=[{host.mapMinX}..{host.mapMaxX}] m, " +
            $"Z=[{host.mapMinZ}..{host.mapMaxZ}] m).");
    }

    private static MapStoneSpawner EnsureMapStoneSpawnerExists()
    {
        MapStoneSpawner? host = MapStoneSpawner.Instance;
        
        if (host == null)
        {
            GameObject go = new GameObject("MapStoneSpawner (RunStart)");
            Object.DontDestroyOnLoad(go);
            host = go.AddComponent<MapStoneSpawner>();
        }

        return host;
    }

    private static void ResetScatterFlag()
    {
        if (!MapStoneSpawner._hasSpawnedThisRun) return;

        ModLogger.LogInfo("[Stones] RunManager.EndGame fired — resetting scatter flag for next run.");
        MapStoneSpawner._hasSpawnedThisRun = false;
    }
}
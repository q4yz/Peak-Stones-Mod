using HarmonyLib;
using Photon.Pun;
using UnityEngine;

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
    /// <summary>
    /// Postfix that fires immediately after <c>RunManager.StartRun()</c> returns.
    /// Lazily creates a persistent <see cref="MapStoneSpawner"/> host if needed,
    /// and triggers the pending queue initialization for the new chunk-loading system.
    /// </summary>
    [HarmonyPatch(nameof(RunManager.StartRun))]
    [HarmonyPostfix]
    public static void StartRun_Postfix()
    {
        // --- Guard 1: master-client-only -----------------------------
        if (!PhotonNetwork.IsMasterClient)
        {
            return;
        }

        // --- Guard 2: one-shot per run -------------------------------
        if (MapStoneSpawner._hasSpawnedThisRun)
        {
            ModLogger.LogInfo(
                "[Stones] RunStart_StoneScatter_Patch: scatter already fired " +
                "for this run, skipping duplicate RunManager.StartRun call.");
            return;
        }

        MapStoneSpawner._hasSpawnedThisRun = true;

        // --- Locate or create the host GameObject --------------------
        MapStoneSpawner? host = MapStoneSpawner.Instance;
        if (host == null)
        {
            GameObject go = new GameObject("MapStoneSpawner (RunStart)");
            Object.DontDestroyOnLoad(go);
            host = go.AddComponent<MapStoneSpawner>();
        }

        if (host == null)
        {
            MapStoneSpawner._hasSpawnedThisRun = false;
            ModLogger.LogError(
                "[Stones] RunStart_StoneScatter_Patch: failed to create " +
                "MapStoneSpawner host GameObject; scatter aborted for this run.");
            return;
        }

        // --- Kick off the new Pending Queue system -------------------
        host.InitializeSpawnQueue();

        ModLogger.LogInfo(
            $"[Stones] RunStart_StoneScatter_Patch: triggered queue initialization " +
            $"(host='{host.name}', totalStones={StonesConfig.MaxStones.Value}, " +
            $"X=[{host.mapMinX}..{host.mapMaxX}] m, " +
            $"Z=[{host.mapMinZ}..{host.mapMaxZ}] m).");
    }


    [HarmonyPatch("EndGame")]
    [HarmonyPostfix]
    public static void EndGame_Postfix()
    {
        if (!MapStoneSpawner._hasSpawnedThisRun) return;

        ModLogger.LogInfo(
            "[Stones] RunEnd_StoneScatter_FlagReset_Patch: " +
            "RunManager.EndGame fired — resetting scatter flag for next run.");
        MapStoneSpawner._hasSpawnedThisRun = false;
    }
}
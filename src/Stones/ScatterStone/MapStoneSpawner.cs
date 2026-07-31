using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

namespace Stones;

[DisallowMultipleComponent]
[AddComponentMenu("Stones/Map Stone Spawner")]
public class MapStoneSpawner : MonoBehaviourPunCallbacks
{
    [Header("Spawn Volume")]
    public float mapMinX = -175f;
    public float mapMaxX = 175f;
    public float mapMinZ = -300f;
    public float mapMaxZ = 2500f;

    [Header("Bounds")]
    public float lobbyExclusionRadius = 25f;
    [Min(0)] public int maxExclusionRetries = 8;

    [Header("Raycast")]
    public float skySpawnHeight = 4000f;
    public float raycastMaxDistance = 5000f;
    public LayerMask groundLayerMask;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    public float groundOffset = 0.01f;

    [Header("Performance")]
    [Min(1)] public int spawnsPerFrame = 5;

    // --- State ---
    public static bool _hasSpawnedThisRun = false;
    public static MapStoneSpawner? Instance { get; private set; }
    
    // Stores (X, Z) coordinates of stones waiting for chunks to load
    private List<Vector2> pendingSpawns = new List<Vector2>();

    private void Awake()
    {
        Instance = this;
        groundLayerMask = BuildDefaultGroundMask();
    }

    private void OnDestroy()
    {
        ModLogger.LogDebug("[Stones] MapStoneSpawner destroyed.");
        if (Instance == this) Instance = null;
    }
    
    public override void OnEnable()
    {
        base.OnEnable();
        if (Instance == null) Instance = this;
    }

    public override void OnLeftRoom()
    {
        _hasSpawnedThisRun = false;
        pendingSpawns.Clear(); // Reset queue on room exit
    }

    /// <summary>
    /// Generates all coordinates and runs the first batch for the starting area.
    /// Called by RunManager.StartRun() patch.
    /// </summary>
    public void InitializeSpawnQueue()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        pendingSpawns.Clear();
        float lobbyExclusionSqr = lobbyExclusionRadius * lobbyExclusionRadius;

        int totalStonesToSpawn = StonesConfig.MaxStones.Value;

        for (int i = 0; i < totalStonesToSpawn ; i++)
        {
            float x = 0f, z = 0f;
            bool outsideLobby = false;

            int attempts = 1 + Mathf.Max(0, maxExclusionRetries);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                x = Random.Range(mapMinX, mapMaxX);
                z = Random.Range(mapMinZ, mapMaxZ);
                if ((x * x + z * z) >= lobbyExclusionSqr)
                {
                    outsideLobby = true;
                    break;
                }
            }

            if (outsideLobby) pendingSpawns.Add(new Vector2(x, z));
        }

        ModLogger.LogDebug($"[Stones] Queued {pendingSpawns.Count} stone locations.");
        StartCoroutine(ProcessPendingSpawns());
    }

    /// <summary>
    /// Raycasts all queued coordinates. Spawns hits, keeps misses in the queue.
    /// </summary>
    public IEnumerator ProcessPendingSpawns()
    {
        if (!PhotonNetwork.IsMasterClient || pendingSpawns.Count == 0) yield break;

        List<Vector2> stillPending = new List<Vector2>();
        int successfulThisBatch = 0;

        for (int i = 0; i < pendingSpawns.Count; i++)
        {
            Vector2 pos = pendingSpawns[i];
            Vector3 skyPos = new Vector3(pos.x, skySpawnHeight, pos.y);

            if (Physics.Raycast(skyPos, Vector3.down, out RaycastHit hit, raycastMaxDistance, groundLayerMask.value, triggerInteraction))
            {
                Vector3 spawnPos = hit.point + Vector3.up * groundOffset;
                if (ItemSpawnHelper.SpawnRandomStone(spawnPos, Random.rotation) != null)
                {
                    successfulThisBatch++;
                }
            }
            else
            {
                stillPending.Add(pos); 
            }

            if (spawnsPerFrame > 0 && (i + 1) % spawnsPerFrame == 0) yield return null;
        }

        pendingSpawns = stillPending;
        ModLogger.LogInfo($"[Stones] Spawned {successfulThisBatch} stones this batch. {pendingSpawns.Count} waiting for unloaded chunks.");
    }

    /// <summary>
    /// Called by the Campfire patch. Waits for chunks to load, then retries the queue.
    /// </summary>
    public IEnumerator DelayedRetryQueue()
    {
        ModLogger.LogInfo("[Stones] Waiting 3s for new chunks to load before retrying stone spawns...");
        yield return new WaitForSeconds(10f);
        yield return StartCoroutine(ProcessPendingSpawns());
    }

    private void OnValidate()
    {
        int totalStonesToSpawn = StonesConfig.MaxStones.Value;
        if (totalStonesToSpawn < 0) totalStonesToSpawn = 0;
        if (lobbyExclusionRadius < 0f) lobbyExclusionRadius = 0f;
        if (maxExclusionRetries < 0) maxExclusionRetries = 0;
        if (skySpawnHeight < 0f) skySpawnHeight = 0f;
        if (groundOffset < 0f) groundOffset = 0f;
        if (raycastMaxDistance < 0f) raycastMaxDistance = 0f;
        if (spawnsPerFrame < 1) spawnsPerFrame = 1;

        if (mapMinX > mapMaxX) { (mapMinX, mapMaxX) = (mapMaxX, mapMinX); }
        if (mapMinZ > mapMaxZ) { (mapMinZ, mapMaxZ) = (mapMaxZ, mapMinZ); }
    }

    private static LayerMask BuildDefaultGroundMask()
    {
        return LayerMask.GetMask("Terrain", "Map");
    }
}
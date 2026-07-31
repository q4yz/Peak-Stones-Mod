using System.Collections;
using UnityEngine;
using Photon.Pun;

namespace Stones;

public static class ItemSpawnHelper
{
    public static GameObject? SpawnStone(string prefabId, Vector3 pos, Quaternion rot, bool startSleeping = true)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            ModLogger.LogWarning("[Stones] Aborting SpawnStone: Only the Master Client should spawn networked stones.");
            return null;
        }

        if (string.IsNullOrEmpty(prefabId))
        {
            ModLogger.LogError("[Stones] SpawnStone called with null/empty prefabId.");
            return null;
        }

        string modId = Plugin.ModId;
        string photonKey = $"0_Items/{modId}:{prefabId}";

        object[] instantiationData = new object[] { startSleeping };
        
        var go = PhotonNetwork.Instantiate(photonKey, pos, rot, 0, instantiationData);
        
        if (go == null)
        {
            ModLogger.LogError(
                $"[Stones] PhotonNetwork.Instantiate returned null - '{photonKey}' " +
                "not registered. Did PEAKLib.ItemsPlugin.RegisterContent run for " +
                "this tier before this call?");
            return null;
        }

        var item = go.GetComponent<global::Item>();
        if (item == null)
        {
            ModLogger.LogWarning(
                $"[Stones] Spawned '{prefabId}' has no Item component - pickup/throw won't work.");
            return go;
        }

        item.itemState = ItemState.Ground;
        item.SetKinematicNetworked(false, pos, rot);

        return go;
    }

    public static GameObject? SpawnRandomStone(Vector3 pos, Quaternion rot)
    {
        return SpawnRandomFromTiers(Plugin.StoneTiers, pos, rot);
    }

    public static GameObject? SpawnRandomStormStone(Vector3 pos, Quaternion rot)
    {
        return SpawnRandomFromTiers(Plugin.StormStoneTiers, pos, rot);
    }

    private static GameObject? SpawnRandomFromTiers(StonesItem[] tiers, Vector3 pos, Quaternion rot)
    {
        if (tiers.Length <= 1)
        {
            ModLogger.LogError("[Stones] No stone tiers registered - cannot spawn random stone.");
            return null;
        }
        
        StonesItem chosen = tiers[Random.Range(1, tiers.Length)];
        return SpawnStone(chosen.PrefabName, pos, rot);
    }
    public static void LogSpawned(string label, GameObject go, Vector3 pos)
    {
        var item  = go.GetComponent<global::Item>();
        var rb    = go.GetComponent<Rigidbody>();
        var col   = go.GetComponent<Collider>();
        var pv    = go.GetComponent<PhotonView>();
        var stone = go.GetComponent<StoneBehavior>();

        ModLogger.LogDebug(
            $"[Stones] {label} spawned:\n" +
            $"  Name: {go.name}\n" +
            $"  Position: {pos}\n" +
            $"  PhotonView: {(pv != null ? $"yes(viewID={pv.ViewID})" : "MISSING")}\n" +
            $"  Item: {(item != null ? $"yes(state={item.itemState}, itemID={item.itemID})" : "MISSING")}\n" +
            $"  Rigidbody: {(rb != null ? $"yes(mass={rb.mass:F2})" : "MISSING")}\n" +
            $"  Collider: {(col != null ? col.GetType().Name : "MISSING")}\n" +
            $"  StoneBehavior: {(stone != null ? "yes" : "MISSING")}\n" +
            $"  IsMasterClient: {PhotonNetwork.IsMasterClient}");
    }
}

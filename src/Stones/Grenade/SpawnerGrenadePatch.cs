using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Stones;

[HarmonyPatch(typeof(Spawner), "GetObjectsToSpawn")]
public class SpawnerGrenadePatch
{
    // ==========================================
    // HARMONY PATCH (The Main Flow)
    // ==========================================

    static void Postfix(Spawner __instance, ref List<GameObject> __result)
    {
        // 1. If grenades are allowed, let the game spawn them normally.
        if (StonesConfig.EnableGrenades.Value) return;

        // 2. If nothing spawned, do nothing.
        if (__result == null || __result.Count == 0) return;

        // 3. Filter the spawn list.
        ReplaceGrenadesInSpawnList(__instance, __result);
    }

    // ==========================================
    // HELPER METHODS
    // ==========================================

    private static void ReplaceGrenadesInSpawnList(Spawner spawner, List<GameObject> spawnList)
    {
        for (int i = 0; i < spawnList.Count; i++)
        {
            if (IsGrenade(spawnList[i]))
            {
                spawnList[i] = DetermineReplacementItem(spawner, spawnList);
            }
        }
    }

    private static bool IsGrenade(GameObject? obj)
    {
        return obj != null && obj.name.Contains("Grenade");
    }

    private static GameObject? DetermineReplacementItem(Spawner spawner, List<GameObject> spawnList)
    {
        // STRATEGY A: Find another safe item in the same chest to duplicate.
        // Example: If the chest rolled [Grenade, Flashlight], it becomes [Flashlight, Flashlight].
        GameObject? replacement = FindSafeItemInList(spawnList);

        // STRATEGY B: If the chest ONLY rolled grenades (very rare), use the spawner's built-in fallback item.
        if (replacement == null && spawner.fallbackSpawn != null)
        {
            replacement = spawner.fallbackSpawn;
        }
        return replacement;
    }

    private static GameObject? FindSafeItemInList(List<GameObject> spawnList)
    {
        foreach (var item in spawnList)
        {
            if (!IsGrenade(item))
            {
                return item;
            }
        }
        return null;
    }
}
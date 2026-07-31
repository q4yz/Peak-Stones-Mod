using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Stones;

[HarmonyPatch(typeof(Spawner), "GetObjectsToSpawn")]
public class SpawnerGrenadePatch
{
    // Make sure to add the Spawner __instance parameter so we can access its fallback item!
    static void Postfix(Spawner __instance, ref List<GameObject> __result)
    {
        // 1. If the config says grenades are enabled, do nothing! Let them spawn.
        if (StonesConfig.EnableGrenades.Value) return;

        if (__result == null || __result.Count == 0) return;

        for (int i = 0; i < __result.Count; i++)
        {
            // 2. Did this slot roll a Grenade?
            if (__result[i] != null && __result[i].name.Contains("Grenade"))
            {
                GameObject replacementItem = null;

                // 3. STRATEGY A: Find another item in this same chest that ISN'T a grenade and duplicate it.
                // Example: If the chest rolled [Grenade, Flashlight], it becomes [Flashlight, Flashlight].
                foreach (var safeItem in __result)
                {
                    if (safeItem != null && !safeItem.name.Contains("Grenade"))
                    {
                        replacementItem = safeItem;
                        break;
                    }
                }

                // 4. STRATEGY B: If the chest ONLY rolled grenades (very rare), use the spawner's built-in fallback item.
                if (replacementItem == null && __instance.fallbackSpawn != null)
                {
                    replacementItem = __instance.fallbackSpawn;
                }

                // 5. Swap the grenade out for the safe replacement. The slot is no longer empty!
                __result[i] = replacementItem;
            }
        }
    }
}
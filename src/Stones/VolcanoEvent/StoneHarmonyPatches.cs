using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Stones;



[HarmonyPatch(typeof(WindChillZone), "RPCA_ToggleWind")]
public static class WindChillZone_VulcanHijack_Patch
{
    [HarmonyPrefix] // Changed to Prefix
    public static bool Prefix(bool set, Vector3 windDir, float untilSwitch, WindChillZone __instance)
    {
        if (!StonesConfig.EnableVolcanoEvent.Value)
        {
            return true; 
        }

        VulcanStormManager.EnsureInstance();

        // 1. STORM ENDING
        if (!set)
        {
            VulcanStormManager.Instance?.StopVulcanOutbreak();
            return true; 
        }

        // 2. STORM STARTING - Master Client makes the decision
        if (PhotonNetwork.IsMasterClient)
        {
            if (UnityEngine.Random.value <= StonesConfig.VulcanOutbreakChance.Value)
            {
                ModLogger.LogInfo(
                    $"[Vulcan] Hijacked storm start. Launching volcanic outbreak.");
                
                VulcanStormManager.Instance?.StartVulcanOutbreak();
                return false; 
            }
        }
        else
        {
            if (VulcanStormManager.Instance != null && VulcanStormManager.Instance.IsVulcanOutbreakActive)
            {
                return false;
            }
        }
        Traverse.Create(WindChillZone.instance).Field("untilSwitch").SetValue(-1f);
        return true; 
    }
}

[HarmonyPatch(typeof(WindChillZone), "ApplyStatus")]
public static class WindChillZone_VulcanStatus_Patch
{
    private static bool s_loggedMissingBurnStatus;

    [HarmonyPrefix]
    public static bool Prefix(Character character, WindChillZone __instance)
    {
        if (!StonesConfig.EnableVolcanoEvent.Value)
        {
            return true;
        }

        VulcanStormManager? manager = VulcanStormManager.Instance;
        if (manager == null)
        {
            return true;
        }

        if (!manager.IsVulcanOutbreakActive)
        {
            return true;
        }

        float windPlayerFactor = Mathf.Clamp01(__instance.windIntensity);

        float climbingStamMinimumMultiplier = Mathf.Max(
            __instance.grabStaminaMultiplierDuringWind * windPlayerFactor * 4f,
            1f);
        character.refs.climbing.climbingStamMinimumMultiplier = climbingStamMinimumMultiplier;

        if (TryGetVulcanStatusType(out CharacterAfflictions.STATUSTYPE vulcanStatusType))
        {
            float vulcanStatusAmount = windPlayerFactor * __instance.statusApplicationPerSecond * Time.deltaTime * 2f;
            character.refs.afflictions.AddStatus(vulcanStatusType, vulcanStatusAmount);
        }
        else if (!s_loggedMissingBurnStatus)
        {
            s_loggedMissingBurnStatus = true;
            ModLogger.LogWarning(
                "[Vulcan] No burn-like status enum found. The outbreak will only increase wind strain and skip the custom affliction.");
        }

        return false;
    }

    private static bool TryGetVulcanStatusType(out CharacterAfflictions.STATUSTYPE statusType)
    {
        string[] candidateNames = { "Burning", "Burn", "Heat", "Fire" };
        foreach (string candidateName in candidateNames)
        {
            if (Enum.TryParse(candidateName, true, out statusType))
            {
                return true;
            }
        }

        statusType = default;
        return false;
    }
}



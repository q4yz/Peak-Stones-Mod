using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace Stones;

[HarmonyPatch(typeof(WindChillZone), "RPCA_ToggleWind")]
public static class VulcanPatches
{
    [HarmonyPrefix]
    public static bool Prefix(bool set, Vector3 windDir, float untilSwitch, WindChillZone __instance)
    {
        VulcanManager vulcanManager = VulcanManager.EnsureInstance();
        
        if (!set)
        {
            vulcanManager.StopVulcanOutbreak();
            return true; 
        }
        
        if (!vulcanManager.VulcanOutbreakEnabled())
        {
            return true; 
        }
        
        if (IsMaster() && RollForVulcanOutbreak())
        {
            ModLogger.LogInfo($"[Vulcan] Hijacked storm start. Launching volcanic outbreak.");
            vulcanManager.StartVulcanOutbreak();
            return false; 
        }
        
        if(!IsMaster() && vulcanManager.IsVulcanOutbreakActive)
        {
            return false;
        }
        
        Traverse.Create(WindChillZone.instance).Field("untilSwitch").SetValue(-1f);
        return true; 
    }

    private static bool IsMaster()
    {
        return PhotonNetwork.IsMasterClient;
    }
    private static bool RollForVulcanOutbreak()
    {
        return UnityEngine.Random.value <= StonesConfig.VulcanOutbreakChance.Value;
    }
}




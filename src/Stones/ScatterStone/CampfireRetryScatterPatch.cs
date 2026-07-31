using HarmonyLib;
using Photon.Pun;
    
namespace Stones;

[HarmonyPatch(typeof(Campfire), "Light_Rpc")]
public static class CampfireRetryScatterPatch
{
    [HarmonyPostfix]
    public static void Postfix(bool updateSegment, Campfire __instance)
    {
        if (!PhotonNetwork.IsMasterClient || !updateSegment)
        {
            return;
        }

        if (MapStoneSpawner.Instance != null)
        {
            MapStoneSpawner.Instance.StartCoroutine(MapStoneSpawner.Instance.DelayedRetryQueue());
        }
    }
}
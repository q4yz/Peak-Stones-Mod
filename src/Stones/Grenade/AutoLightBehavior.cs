using UnityEngine;
using Photon.Pun;

namespace Stones;

/// <summary>
/// Attaches to a custom explosive item. The moment it hits the ground (thrown or dropped),
/// it spawns a real native vanilla Dynamite via Photon, sets its fuse to instantly detonate,
/// and destroys the custom item.
/// </summary>
[RequireComponent(typeof(Item))]
public class AutoLightBehavior : MonoBehaviourPun
{
    private Item item = null!;
    private bool hasTriggered = false;

    private void Awake()
    {
        item = GetComponent<Item>();
    }

    private void Update()
    {
        if (hasTriggered || !photonView.IsMine || !IsOnGround())
        {
            return;
        }
        TriggerExplosiveSwap();
    }

    private bool IsOnGround()
    {
        return item.itemState == ItemState.Ground;
    }

    private void TriggerExplosiveSwap()
    {
        hasTriggered = true;
        ModLogger.LogInfo("[AutoLight] Custom explosive dropped/thrown. Spawning native vanilla Dynamite swap...");

        SpawnAndIgniteVanillaDynamite();
        DespawnCustomItem();
    }

    private void SpawnAndIgniteVanillaDynamite()
    {
        try
        {
            GameObject realDynamiteObj =
                PhotonNetwork.Instantiate("0_Items/Dynamite", transform.position, transform.rotation);

            if (realDynamiteObj != null)
            {
                Dynamite realDynamite = realDynamiteObj.GetComponent<Dynamite>();
                if (realDynamite != null)
                {
                    realDynamite.startingFuseTime = 0.01f;
                    realDynamite.LightFlare();
                }
            }
        }
        catch (System.Exception ex)
        {
            ModLogger.LogError($"[AutoLight] Failed to spawn native dynamite: {ex.Message}");
        }
    }

    private void DespawnCustomItem()
    {
        item.ClearDataFromBackpack();
        PhotonNetwork.Destroy(gameObject);
    }
}

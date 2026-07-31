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
        if (hasTriggered) return;
        
        // Only the master client or owner handles the network spawn translation
        if (!photonView.IsMine) return;

        // The moment the item hits the ground (thrown/dropped)
        if (item.itemState == ItemState.Ground)
        {
            hasTriggered = true;

            Vector3 spawnPos = transform.position;
            Quaternion spawnRot = transform.rotation;

            ModLogger.LogInfo("[AutoLight] Custom explosive dropped/thrown. Spawning native vanilla Dynamite swap...");

            try
            {
                // 1. Spawn the real base-game Dynamite through Photon DefaultPool ("0_Items/Dynamite" or matching registered name)
                // Note: Ensure "Dynamite" matches the exact registered Photon prefab name for vanilla dynamite in the game's resource pool.
                GameObject realDynamiteObj = PhotonNetwork.Instantiate("0_Items/Dynamite", spawnPos, spawnRot);
                
                if (realDynamiteObj != null)
                {
                    Dynamite realDynamite = realDynamiteObj.GetComponent<Dynamite>();
                    if (realDynamite != null)
                    {
                        // Set the fuse time super short so it blows up instantly
                        realDynamite.startingFuseTime = 0.01f;
                        
                        // Force light it immediately
                        realDynamite.LightFlare();
                    }
                }
            }
            catch (System.Exception ex)
            {
                ModLogger.LogError($"[AutoLight] Failed to spawn native dynamite: {ex.Message}");
            }

            // 2. Clean up and destroy our custom item instance instantly across the network
            item.ClearDataFromBackpack();
            PhotonNetwork.Destroy(base.gameObject);
        }
    }
}
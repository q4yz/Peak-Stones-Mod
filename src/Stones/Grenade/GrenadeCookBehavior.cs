using UnityEngine;
using Photon.Pun;

namespace Stones;

/// <summary>
/// Attaches to a custom grenade item. Instantly triggers an explosion 
/// by spawning your custom registered explosion item via Photon the moment it becomes cooked.
/// </summary>
[RequireComponent(typeof(Item))]
public class GrenadeCookBehavior : MonoBehaviourPun
{
    private Item item = null!;
    private bool hasExploded = false;

    private void Awake()
    {
        item = GetComponent<Item>();
    }

    private void Update()
    {
        if (hasExploded || !photonView.IsMine || !IsCooked(item))
        {
            return;
        }
        TriggerExplosion();
    }

    private void TriggerExplosion()
    {
        hasExploded = true;
        ModLogger.LogInfo("[GrenadeCook] Grenade cooked! Spawning custom 'Explosion' item via Photon...");
        
        SpawnExplosionPrefab();
        RemoveFromPlayerHands();
        DespawnGrenade();
    }

    private void DespawnGrenade()
    {
        item.ClearDataFromBackpack();
        PhotonNetwork.Destroy(base.gameObject);
    }

    private void RemoveFromPlayerHands()
    {
        if (Character.localCharacter != null && Character.localCharacter.data.currentItem == item)
        {
            Player.localPlayer.EmptySlot(Character.localCharacter.refs.items.currentSelectedSlot);
        }
    }

    private void SpawnExplosionPrefab()
    {
        Vector3 spawnPos = transform.position;
        Quaternion spawnRot = transform.rotation;
        
        try
        {
            string explosionKey = $"0_Items/{Plugin.ModId}:Explosion";
            
            GameObject explosionObj = PhotonNetwork.Instantiate(explosionKey, spawnPos, spawnRot);
            if (explosionObj != null)
            {
                Item explosionItem = explosionObj.GetComponent<Item>();
                if (explosionItem != null && explosionItem.rig != null)
                {
                    explosionItem.rig.linearVelocity = item.rig != null ? item.rig.linearVelocity : Vector3.zero;
                }
            }
        }
        catch (System.Exception ex)
        {
            ModLogger.LogError($"[GrenadeCook] Failed to spawn custom explosion item: {ex.Message}");
        }
    }

    private bool IsCooked(Item item)
    {
        if (item.data != null && item.data.TryGetDataEntry<IntItemData>(DataEntryKey.CookedAmount, out var cookedData))
        {
            if (cookedData.Value > 0)
            {
                return  true;
            }
        }
        return false;
    }
}
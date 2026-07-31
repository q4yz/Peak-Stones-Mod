using Photon.Pun;
using UnityEngine;

namespace Stones;

[RequireComponent(typeof(PhotonView))]
public class StoneBehavior : MonoBehaviourPun
{
    private float lastHitTime = -1f;

    private void OnCollisionEnter(Collision collision)
    {
        var stoneItem = GetComponent<global::Item>();
        if (stoneItem == null) return;
        
        if (stoneItem.itemState != ItemState.Ground) return;
        
        var hitPart = collision.gameObject.GetComponent<Bodypart>();
        if (hitPart == null) return;

        Character victim = hitPart.GetComponentInParent<Character>();
        if (victim == null) return;
        

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (victim.photonView.IsMine)
        {
            if (Time.time - lastHitTime < 1.0f) return;

            float weight = stoneItem.CarryWeight;
            if (weight <= 4f) return;
            
            float stoneFactor = Mathf.Clamp01(weight / 20f);
            float speedFactor = Mathf.InverseLerp(10f, 25f, impactSpeed);
            float damageAmount = speedFactor * stoneFactor * 0.3f;

            if (damageAmount > 0f)
            {
                lastHitTime = Time.time;
                try
                {
                    victim.refs.afflictions.AddStatus(
                        CharacterAfflictions.STATUSTYPE.Injury,
                        damageAmount);

                    ModLogger.LogInfo(
                        $"[Stone] Injury -> victim='{victim.characterName}', " +
                        $"weight={weight:F2}, speed={impactSpeed:F2}m/s, " +
                        $"stoneFactor={stoneFactor:F3}, amount={damageAmount:F3}.");
                }
                catch (System.Exception ex)
                {
                    ModLogger.LogError(
                        $"[Stone] AddStatus threw {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC(
                nameof(PlayImpactVisuals),
                RpcTarget.AllBuffered,
                impactSpeed);
        }
    }
    
    [PunRPC]
    private void PlayImpactVisuals(float impactSpeed, PhotonMessageInfo info)
    {
        ModLogger.LogInfo(
            $"[Stone] PlayImpactVisuals - speed={impactSpeed:F2}m/s, " +
            $"sender={info.Sender.ActorNumber}, " +
            $"IsMasterClient={PhotonNetwork.IsMasterClient}, " +
            $"ItemState={GetItemStateString()}");
    }

    private string GetItemStateString()
    {
        var item = GetComponent<global::Item>();
        return item != null ? item.itemState.ToString() : "(no Item)";
    }
}

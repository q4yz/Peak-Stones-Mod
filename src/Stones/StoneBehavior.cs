using Photon.Pun;
using UnityEngine;

namespace Stones;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(global::Item))]
public class StoneBehavior : MonoBehaviourPun
{
    private float _lastHitTime = -1f;
    private ItemState _previousState;
    private float _timeBecameGrounded = -1f;
    private int _throwerActorNumber = -1;
     required public global::Item stoneItem;
    
    
    private void Awake()
    {
        stoneItem = GetComponent<global::Item>();
        
        if (stoneItem == null)
        {
            ModLogger.LogError($"[Stone] Missing Item component on {gameObject.name}! Destroying StoneBehavior.");
            Destroy(this); 
        }
    }

    private void Update()
    {
        if (stoneItem.itemState != _previousState)
        {
            if (stoneItem.itemState == ItemState.Ground)
            {
                _timeBecameGrounded = Time.time;
                _throwerActorNumber = photonView.OwnerActorNr; 
            }
            _previousState = stoneItem.itemState;
        }
    }

   private void OnCollisionEnter(Collision collision)
    {
        
        
        if (stoneItem.itemState != ItemState.Ground) return;
        
        var hitPart = collision.gameObject.GetComponent<Bodypart>();
        if (hitPart == null) return;

        Character victim = hitPart.GetComponentInParent<Character>();
        if (victim == null) return;
        
        float timeInAir = Time.time - _timeBecameGrounded;
        bool isThrower = (victim.photonView.OwnerActorNr == _throwerActorNumber);
        
        if (isThrower && timeInAir < 1.0f) return;

        float impactSpeed = collision.relativeVelocity.magnitude;

        if (victim.photonView.IsMine)
        {
            if (Time.time - _lastHitTime < 1.0f) return;

            float weight = stoneItem.CarryWeight;
            if (weight <= 4f) return;
            
            float stoneFactor = Mathf.Clamp01(weight / 20f);
            float speedFactor = Mathf.InverseLerp(10f, 25f, impactSpeed);
            float damageAmount = speedFactor * stoneFactor * 0.3f;

            if (damageAmount > 0f)
            {
                _lastHitTime = Time.time;
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
        return  stoneItem.itemState.ToString() ;
    }
}

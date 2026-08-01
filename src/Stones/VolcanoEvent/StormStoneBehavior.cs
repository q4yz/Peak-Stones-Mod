using UnityEngine;
using Photon.Pun;

namespace Stones;

public class StormStoneBehavior : MonoBehaviour
{
    
    private float maxLife = 25f; // Maximum time before forced despawn
    private float gracePeriod = 2f; // Wait 2 seconds before checking velocity so it doesn't despawn mid-air
    private float tumbleSpeed = 15f; // Adjust this to make it spin faster or slower
    
    private float lifeTimer = 0f;
    private Rigidbody? rb;
    private global::Item? itemComponent;
    private Breakable? breakableComponent;
    private PhotonView? view;
    
    private void Awake()
    {
        FetchComponents();
        MakeUnpickupable();
        DisableBreakableBehaviors();
    }

    private void Start()
    {
        ApplyRandomTumble();
    }

    private void Update()
    {
        if (!IsMaster()) return;

        HandleLifetimeAndDespawn();
    }

    // ==========================================
    // HELPER METHODS
    // ==========================================

    private void FetchComponents()
    {
        rb = GetComponent<Rigidbody>();
        itemComponent = GetComponent<global::Item>();
        breakableComponent = GetComponent<Breakable>();
        view = GetComponent<PhotonView>();
    }

    private void MakeUnpickupable()
    {
        if (itemComponent != null)
        {
            itemComponent.blockInteraction = true; 
        }
    }

    private void DisableBreakableBehaviors()
    {
        // 1. Disable the main breakable component
        if (breakableComponent != null)
        {
            breakableComponent.breakOnCollision = false;
            breakableComponent.enabled = false;
        }

        // 2. SAFETY: Disable any child components containing "break" or "damage"
        var breakables = GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in breakables)
        {
            if (mb == null) continue;
            
            string typeName = mb.GetType().Name.ToLowerInvariant();
            if (typeName.Contains("break") || typeName.Contains("damage"))
            {
                mb.enabled = false;
            }
        }
    }

    private void ApplyRandomTumble()
    {
        if (!IsMaster() || rb == null) return;

        // Random.insideUnitSphere generates a random 3D direction vector.
        // Multiplying it by tumbleSpeed applies random rotational momentum on all 3 axes.
        rb.angularVelocity = Random.insideUnitSphere * tumbleSpeed;
    }

    private void HandleLifetimeAndDespawn()
    {
        lifeTimer += Time.deltaTime;
        
        // Force despawn if it exists too long
        if (lifeTimer >= maxLife)
        {
            Despawn();
            return;
        }
        
        // Despawn early if it has hit the ground and stopped rolling
        if (lifeTimer >= gracePeriod && HasStoppedMoving())
        {
            Despawn();
        }
    }

    private bool HasStoppedMoving()
    {
        if (rb == null) return false;
        
        return rb.linearVelocity.sqrMagnitude < 0.1f;
    }

    private void Despawn()
    {
        if (view != null && view.IsMine)
        {
            PhotonNetwork.Destroy(gameObject);
        }
        else if (view == null)
        {
            Destroy(gameObject);
        }
    }

    private bool IsMaster()
    {
        return PhotonNetwork.IsMasterClient;
    }
}
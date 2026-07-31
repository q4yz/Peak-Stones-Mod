using UnityEngine;
using Photon.Pun;

namespace Stones;

public class StormStoneBehavior : MonoBehaviour
{
    private Rigidbody? rb;
    private global::Item? itemComponent;
    private Breakable? breakableComponent;
    private PhotonView? view;

    private float lifeTimer = 0f;
    private float maxLife = 25f; // Maximum time before forced despawn
    private float gracePeriod = 2f; // Wait 2 seconds before checking velocity so it doesn't despawn mid-air
    
    private float tumbleSpeed = 15f; // Adjust this to make it spin faster or slower
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        itemComponent = GetComponent<global::Item>();
        breakableComponent = GetComponent<Breakable>();
        view = GetComponent<PhotonView>();

        // 1. MAKE IT UN-PICKUPABLE
        if (itemComponent != null)
        {
            itemComponent.blockInteraction = true; 
        }

        // 2. DISABLE BREAKABLE BEHAVIOR SO IT DOESN'T SHATTER OR SPAWN DEBRIS
        if (breakableComponent != null)
        {
            breakableComponent.breakOnCollision = false;
            breakableComponent.enabled = false;
        }

        // 3. SAFETY: Disable any child components containing "break" or "damage"
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
    private void Start()
    {
        // --- NEW: Apply random spin on spawn ---
        // Only the Master Client should apply physical forces to networked objects.
        if (PhotonNetwork.IsMasterClient && rb != null)
        {
            // Random.insideUnitSphere generates a random 3D direction vector.
            // Multiplying it by tumbleSpeed applies random rotational momentum on all 3 axes.
            rb.angularVelocity = Random.insideUnitSphere * tumbleSpeed;
        }
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        lifeTimer += Time.deltaTime;
        
        if (lifeTimer >= maxLife)
        {
            Despawn();
            return;
        }
        
        if (lifeTimer >= gracePeriod && rb != null)
        {
            if (rb.linearVelocity.sqrMagnitude < 0.1f) 
            {
                Despawn();
            }
        }
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
}
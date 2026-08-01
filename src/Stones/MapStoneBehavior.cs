using Photon.Pun;
using UnityEngine;

namespace Stones;
[RequireComponent(typeof(PhotonView))]

public class MapStoneBehavior : MonoBehaviourPun
{
    private void Start()
    {
        object[] data = photonView.InstantiationData;
        
        bool shouldSleep = data != null && data.Length > 0 && data[0] is bool sleepFlag && sleepFlag;

        if (shouldSleep)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
        }
    }
}
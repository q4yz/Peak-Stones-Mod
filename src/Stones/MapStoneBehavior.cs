
using Photon.Pun;
using UnityEngine;

namespace Stones;
[RequireComponent(typeof(PhotonView))]

public class MapStoneBehavior : MonoBehaviourPun
{
    private void Start()
    {
        // Read the custom initialization data passed during PhotonNetwork.Instantiate
        object[] data = photonView.InstantiationData;

        // Check if our custom "startSleeping" flag exists and is set to true
        bool shouldSleep = data != null && data.Length > 0 && data[0] is bool sleepFlag && sleepFlag;

        if (shouldSleep)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                //Srb.Sleep();
                rb.isKinematic = true;
            }
        }
    }
}
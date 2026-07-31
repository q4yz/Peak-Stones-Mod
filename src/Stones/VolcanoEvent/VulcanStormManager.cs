using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEngine;

namespace Stones;

/// <summary>
/// Keeps the volcanic outbreak state in sync with the game's own
/// storm lifecycle. No timer drives this component; it only reacts to
/// WindChillZone RPCs and room-property updates.
/// </summary>
[DisallowMultipleComponent]
public sealed class VulcanStormManager : MonoBehaviourPunCallbacks
{
    private const string VulcanOutbreakRoomKey = "Stones.VulcanOutbreakActive";

    public static VulcanStormManager? Instance { get; private set; }

    public bool IsVulcanOutbreakActive => isVulcanOutbreakActive;

    private bool isVulcanOutbreakActive;

    public static VulcanStormManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject go = new GameObject("VulcanStormManager");
        DontDestroyOnLoad(go);
        return go.AddComponent<VulcanStormManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SyncFromRoomProperties();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void OnJoinedRoom()
    {
        SyncFromRoomProperties();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!propertiesThatChanged.ContainsKey(VulcanOutbreakRoomKey))
        {
            return;
        }

        SyncFromRoomProperties();
    }

    public override void OnLeftRoom()
    {
        ClearLocalState();
    }

    public void StartVulcanOutbreak()
    {
        if (isVulcanOutbreakActive)
        {
            return;
        }

        isVulcanOutbreakActive = true;
        ModLogger.LogInfo("[Vulcan] A volcanic outbreak has begun.");

        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            SetRoomOutbreakState(true);
        }

        StartCoroutine(VolcanoEvent.Run());
    }

    public void StopVulcanOutbreak()
    {
        if (!isVulcanOutbreakActive)
        {
            return;
        }

        isVulcanOutbreakActive = false;
        ModLogger.LogInfo("[Vulcan] The volcanic outbreak has cleared.");

        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
        {
            SetRoomOutbreakState(false);
        }
    }

    private void SyncFromRoomProperties()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
        {
            return;
        }

        object? value = null;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(VulcanOutbreakRoomKey, out object roomValue))
        {
            value = roomValue;
        }

        bool shouldBeActive = value is bool boolValue && boolValue;
        if (shouldBeActive)
        {
            StartVulcanOutbreak();
        }
        else
        {
            StopVulcanOutbreak();
        }
    }

    private void ClearLocalState()
    {
        isVulcanOutbreakActive = false;
    }

    private void SetRoomOutbreakState(bool active)
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            return;
        }

        Hashtable properties = new Hashtable
        {
            [VulcanOutbreakRoomKey] = active,
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }

  
}
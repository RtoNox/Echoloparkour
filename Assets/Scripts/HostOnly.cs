using Unity.Netcode;
using UnityEngine;

public class HostOnlyUI : MonoBehaviour
{
    void Update()
    {
        if (NetworkManager.Singleton == null)
            return;

        Debug.Log(
            $"IsHost: {NetworkManager.Singleton.IsHost}"
        );
    }
}
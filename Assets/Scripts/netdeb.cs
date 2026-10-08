using Unity.Netcode;
using UnityEngine;

public class NetworkDebug : MonoBehaviour
{
    void Update()
    {
        if (NetworkManager.Singleton == null)
            return;

        Debug.Log(
            $"Host:{NetworkManager.Singleton.IsHost} " +
            $"Client:{NetworkManager.Singleton.IsClient} " +
            $"Listening:{NetworkManager.Singleton.IsListening}"
        );
    }
}

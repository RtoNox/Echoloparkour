using Unity.Netcode;
using UnityEngine;

public class CameraOwnerCheck : NetworkBehaviour
{
    public Camera playerCamera;

    public override void OnNetworkSpawn()
    {
        if (playerCamera != null)
            playerCamera.enabled = IsOwner;
    }
}
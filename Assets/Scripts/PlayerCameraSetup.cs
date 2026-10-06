using Unity.Netcode;
using UnityEngine;

public class PlayerCameraSetup : NetworkBehaviour
{
    public Camera playerCamera;

    public override void OnNetworkSpawn()
    {
        // This enables the camera ONLY for the player who owns this object.
        // For everyone else, it stays disabled.
        if (playerCamera != null)
        {
            playerCamera.enabled = IsOwner;
        }
        
        base.OnNetworkSpawn();
    }
}
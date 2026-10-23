using Unity.Netcode;
using UnityEngine;

public class GoalTrigger : NetworkBehaviour
{
    [SerializeField] private WinManager winManager;

    private void OnTriggerEnter(Collider other)
    {
        // Only the server should validate the win condition
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            // Get the NetworkObject to identify which player won
            NetworkObject playerNetObj = other.GetComponent<NetworkObject>();
            
            if (playerNetObj != null)
            {
                // Tell the WinManager who won and the current time
                winManager.PlayerReachedGoal(playerNetObj.OwnerClientId, Time.timeSinceLevelLoad);
            }
        }
    }
}
using Unity.Netcode;
using UnityEngine;

public class GameSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject playerPrefab;


    private void Start()
    {
        Debug.Log("Spawner Start");

        Debug.Log("IsServer = " + NetworkManager.Singleton.IsServer);

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            Debug.Log("Client Found: " + clientId);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            var player = Instantiate(playerPrefab);

            player.GetComponent<NetworkObject>()
                .SpawnAsPlayerObject(clientId);
        }
    }
}
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    public void StartGame()
    {
        Debug.Log("START BUTTON CLICKED");

        NetworkManager.Singleton.SceneManager.LoadScene(
            "SampleScene",
            LoadSceneMode.Single
        );
    }
}
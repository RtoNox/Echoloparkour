using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class MenuManager : MonoBehaviour
{
    [Header("Menu Panel")]
    [SerializeField] private GameObject menuPanel;

    [Header("Scene Settings")]
    [SerializeField] private string lobbySceneName = "NewLobby";

    private bool isMenuOpen = false;

    private void Start()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isMenuOpen = !isMenuOpen;
            menuPanel.SetActive(isMenuOpen);
        }
    }

    // ---------- RESUME ----------
    public void ResumeGame()
    {
        isMenuOpen = false;
        menuPanel.SetActive(false);
    }

    // ---------- EXIT TO LOBBY ----------
    public void ExitToLobby()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        SceneManager.LoadScene(lobbySceneName);
    }
}
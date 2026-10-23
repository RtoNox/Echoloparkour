using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class WinManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Text winnerNameText;
    [SerializeField] private Text winTimeText;

    private float gameStartTime;
    private bool hasGameEnded = false;

    public override void OnNetworkSpawn()
    {
        // Record the start time on the server when the game begins
        if (IsServer)
        {
            gameStartTime = Time.timeSinceLevelLoad;
        }
    }

    // Called by the GoalTrigger on the server
    public void PlayerReachedGoal(ulong winningClientId, float reachTime)
    {
        if (hasGameEnded) return;
        hasGameEnded = true;

        // Calculate duration (using the time passed from the trigger)
        float duration = reachTime - gameStartTime;

        // Get the player's name. 
        // Note: You need a way to get the player's name from the clientId. 
        // If you don't have a name system, you can use "Player " + winningClientId.
        string winnerName = "Player " + winningClientId; 

        // Send the data to all clients to show the UI
        ShowWinScreenClientRpc(winnerName, duration);
    }

    [ClientRpc]
    private void ShowWinScreenClientRpc(string winnerName, float duration)
    {
        // Activate the panel and set the text
        winPanel.SetActive(true);
        
        winnerNameText.text = $"Winner: {winnerName}!";
        
        // Format time to MM:SS
        int minutes = Mathf.FloorToInt(duration / 60);
        int seconds = Mathf.FloorToInt(duration % 60);
        winTimeText.text = $"Time: {minutes:00}:{seconds:00}";

        // Optional: Unlock the cursor so the player can click the Confirm button
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // --- Return to Main Menu Logic ---
    public void OnConfirmButtonPressed()
    {
        // Shut down the network session and load the main menu
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        
        // Load the main menu scene (make sure this matches your scene name)
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }
}
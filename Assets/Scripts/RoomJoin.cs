using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using ParrelSync;
#endif

public class RoomJoin : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField roomCodeInput;
    [SerializeField] private Text statusText;
    [SerializeField] private GameObject joinButton;
    [SerializeField] private GameObject returnToMainMenuButton;

    [Header("Scene Settings")]
    [SerializeField] private string roomSceneName = "NewLobby";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private ISession currentSession;

    private bool servicesReady;
    private bool isJoining;
    private bool isReturningToMenu;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private async void Start()
    {
        if (joinButton != null)
            joinButton.SetActive(true);

        if (returnToMainMenuButton != null)
            returnToMainMenuButton.SetActive(true);

        if (statusText != null)
            statusText.text = "Initializing services...";

        await InitializeUnityServices();
    }

    // =========================================================
    // UNITY SERVICES
    // =========================================================

    private async Task InitializeUnityServices()
    {
        try
        {
#if UNITY_EDITOR
            InitializationOptions options = new InitializationOptions();

            if (ClonesManager.IsClone())
            {
                string cloneArgument = ClonesManager.GetArgument();

                options.SetProfile(
                    "Clone_" + cloneArgument + "_Profile"
                );

                Debug.Log(
                    "ParrelSync profile: Clone_" +
                    cloneArgument + "_Profile"
                );
            }
            else
            {
                options.SetProfile("Original_Profile");
                Debug.Log("Original Unity Editor profile");
            }

            if (UnityServices.State !=
                ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync(options);
            }
#else
            if (UnityServices.State !=
                ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }
#endif

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance
                    .SignInAnonymouslyAsync();
            }

            servicesReady = true;

            Debug.Log("Unity Services initialized.");
            Debug.Log(
                "Player ID: " +
                AuthenticationService.Instance.PlayerId
            );

            if (statusText != null)
                statusText.text = "Ready to join a room";
        }
        catch (Exception e)
        {
            Debug.LogError("Unity Services initialization failed!");
            Debug.LogException(e);

            if (statusText != null)
                statusText.text = "Initialization failed";
        }
    }

    // =========================================================
    // JOIN ROOM
    // =========================================================

    public async void JoinRoom()
    {
        if (isJoining || isReturningToMenu)
            return;

        if (!servicesReady)
        {
            if (statusText != null)
                statusText.text = "Services are not ready";

            return;
        }

        if (roomCodeInput == null)
        {
            Debug.LogError("Room code input is not assigned!");

            if (statusText != null)
                statusText.text = "Room code input is missing";

            return;
        }

        string roomCode = roomCodeInput.text.Trim();

        if (string.IsNullOrEmpty(roomCode))
        {
            if (statusText != null)
                statusText.text = "Please enter a room code";

            return;
        }

        isJoining = true;

        if (joinButton != null)
            joinButton.SetActive(false);

        if (statusText != null)
            statusText.text = "Joining room...";

        try
        {
            // Clear any stale session reference before joining.
            SharedSession.CurrentSession = null;
            currentSession = null;

            currentSession =
                await MultiplayerService.Instance
                    .JoinSessionByCodeAsync(roomCode);

            if (currentSession == null)
            {
                throw new Exception(
                    "Joining the session returned null."
                );
            }

            // Share the session with RoomManager.
            SharedSession.CurrentSession = currentSession;

            Debug.Log(
                "Successfully joined room: " +
                currentSession.Code
            );

            if (statusText != null)
                statusText.text = "Joined room. Loading room...";

            // Prevent the finally block from re-enabling the button
            // after the scene transition has been requested.
            SceneManager.LoadScene(roomSceneName);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "Failed to join room with code: " + roomCode
            );

            Debug.LogException(e);

            // Clean up references if joining failed.
            SharedSession.CurrentSession = null;
            currentSession = null;

            if (statusText != null)
            {
                statusText.text =
                    "Failed to join room. Check the code, " +
                    "room availability, and player limit.";
            }

            if (joinButton != null)
                joinButton.SetActive(true);
        }
        finally
        {
            isJoining = false;
        }
    }

    // =========================================================
    // RETURN TO MAIN MENU
    // =========================================================

    public async void ReturnToMainMenu()
    {
        if (isReturningToMenu || isJoining)
            return;

        isReturningToMenu = true;

        if (joinButton != null)
            joinButton.SetActive(false);

        if (returnToMainMenuButton != null)
            returnToMainMenuButton.SetActive(false);

        if (statusText != null)
            statusText.text = "Returning to Main Menu...";

        // Use the shared session if this component doesn't have one.
        if (currentSession == null)
            currentSession = SharedSession.CurrentSession;

        try
        {
            if (currentSession != null)
            {
                if (currentSession.IsHost)
                {
                    // Host leaves: delete and disband the room.
                    Debug.Log("Host is deleting the room.");

                    await currentSession.AsHost().DeleteAsync();

                    Debug.Log("Room deleted successfully.");
                }
                else
                {
                    // Client leaves: remove only this player.
                    Debug.Log("Client is leaving the room.");

                    await currentSession.LeaveAsync();

                    Debug.Log("Client left the room.");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to leave or delete the room.");
            Debug.LogException(e);
        }
        finally
        {
            // Clear shared session references so an old room
            // cannot accidentally be reused.
            if (SharedSession.CurrentSession == currentSession)
                SharedSession.CurrentSession = null;

            currentSession = null;

            // Return to the menu even if the session operation failed.
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene(mainMenuSceneName);
        }
    }
}
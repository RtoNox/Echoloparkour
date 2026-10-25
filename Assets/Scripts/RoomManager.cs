using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

#if UNITY_EDITOR
using ParrelSync;
#endif

public enum RoomState
{
    RoomScreen,
    AbilityScreen
}

public class RoomManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text roomIDText;
    [SerializeField] private Text playerCountText;
    [SerializeField] private Text statusText;

    [Header("Buttons")]
    [SerializeField] private GameObject startButton;

    [Header("Game Settings")]
    [SerializeField] private int maxPlayers = 4;
    [SerializeField] private string gameSceneName = "Map1";
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Room Settings")]
    [SerializeField] private bool lockRoomWhenGameStarts = true;

    public GameObject roomScreenUI;
    public GameObject abilityScreenUI;
    public RoomState currentState { get; private set; }

    private ISession currentSession;

    private bool servicesReady;
    private bool creatingRoom;
    private bool roomCreated;
    private bool leavingRoom;
    private bool startingGame;

    private async void Start()
    {
        if (startButton != null)
            startButton.SetActive(false);

        // Reuse the session created or joined in the previous scene.
        if (SharedSession.CurrentSession != null)
        {
            currentSession = SharedSession.CurrentSession;
            roomCreated = true;

            currentSession.Changed += OnSessionChanged;

            Debug.Log("Using existing room: " + currentSession.Code);

            UpdateRoomUI();

            if (statusText != null)
            {
                statusText.text = currentSession.IsHost
                    ? "Room created. Waiting for players..."
                    : "Joined room. Waiting for host...";
            }

            return;
        }

        // No session exists, so initialize services and create a room.
        if (statusText != null)
            statusText.text = "Initializing services...";

        await InitializeUnityServices();

        if (servicesReady)
            await CreateRoom();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) &&
            currentState == RoomState.AbilityScreen)
        {
            ChangeState(RoomState.RoomScreen);
        }
    }

    // =========================================================
    // ROOM UI STATES
    // =========================================================

    public void ChangeState(RoomState newState)
    {
        StopAllCoroutines();
        StartCoroutine(TransitionToState(newState));
        currentState = newState;
    }

    public void RoomToAbility()
    {
        ChangeState(RoomState.AbilityScreen);
    }

    public void AbilityToRoom()
    {
        ChangeState(RoomState.RoomScreen);
    }

    private IEnumerator TransitionToState(RoomState newState)
    {
        if (newState != RoomState.RoomScreen)
            yield return new WaitForSecondsRealtime(0);

        currentState = newState;
        HandleStateChange();
    }

    private void HandleStateChange()
    {
        HideAllMenu();

        switch (currentState)
        {
            case RoomState.RoomScreen:
                if (roomScreenUI != null)
                    roomScreenUI.SetActive(true);
                break;

            case RoomState.AbilityScreen:
                if (abilityScreenUI != null)
                    abilityScreenUI.SetActive(true);
                break;
        }
    }

    private void HideAllMenu()
    {
        if (roomScreenUI != null)
            roomScreenUI.SetActive(false);

        if (abilityScreenUI != null)
            abilityScreenUI.SetActive(false);
    }

    // =========================================================
    // RETURN TO MAIN MENU / LEAVE ROOM
    // =========================================================

    public async void RoomToMainMenu()
    {
        if (leavingRoom)
            return;

        leavingRoom = true;

        if (statusText != null)
            statusText.text = "Leaving room...";

        // Use the shared reference if this component has none.
        if (currentSession == null)
            currentSession = SharedSession.CurrentSession;

        try
        {
            if (currentSession != null)
            {
                if (currentSession.IsHost)
                {
                    // HOST LEAVES:
                    // Delete the session so nobody can join it again.
                    Debug.Log("Host is deleting the room.");

                    await currentSession.AsHost().DeleteAsync();

                    Debug.Log("Room deleted successfully.");
                }
                else
                {
                    // CLIENT LEAVES:
                    // Remove only this player from the session.
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
            // Do not reuse this session when entering another room.
            if (SharedSession.CurrentSession == currentSession)
                SharedSession.CurrentSession = null;

            currentSession = null;
            roomCreated = false;

            // Stop this player's local NGO connection.
            // The host's session deletion disconnects the other clients.
            NetworkManager nm = NetworkManager.Singleton;

            if (nm != null && nm.IsListening)
                nm.Shutdown();

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene(mainMenuSceneName);
        }
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
                options.SetProfile("Clone_" + cloneArgument + "_Profile");

                Debug.Log("ParrelSync profile: Clone_" +
                          cloneArgument + "_Profile");
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

            Debug.Log("Player ID: " +
                      AuthenticationService.Instance.PlayerId);

            if (statusText != null)
                statusText.text = "Services ready";
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
    // CREATE ROOM — HOST PATH
    // =========================================================

    private async Task CreateRoom()
    {
        if (!servicesReady || creatingRoom || roomCreated)
            return;

        creatingRoom = true;

        if (statusText != null)
            statusText.text = "Creating room...";

        try
        {
            SessionOptions options = new SessionOptions
            {
                MaxPlayers = maxPlayers,
                IsLocked = false
            }.WithRelayNetwork();

            currentSession =
                await MultiplayerService.Instance.CreateSessionAsync(
                    options
                );

            if (currentSession == null)
                throw new Exception("Session creation returned null.");

            SharedSession.CurrentSession = currentSession;
            roomCreated = true;

            currentSession.Changed += OnSessionChanged;

            Debug.Log("ROOM CREATED: " + currentSession.Code);

            UpdateRoomUI();

            if (statusText != null)
                statusText.text = "Room created. Waiting for players...";

            LogNetworkStatus();
        }
        catch (Exception e)
        {
            Debug.LogError("ROOM CREATION FAILED");
            Debug.LogException(e);

            if (statusText != null)
                statusText.text = "Failed to create room";
        }
        finally
        {
            creatingRoom = false;
        }
    }

    // =========================================================
    // ROOM UI / PLAYER COUNT
    // =========================================================

    private void OnSessionChanged()
    {
        UpdateRoomUI();
    }

    private void UpdateRoomUI()
    {
        if (currentSession == null)
            return;

        if (roomIDText != null)
            roomIDText.text = "Room ID: " + currentSession.Code;

        UpdatePlayerCount();

        if (startButton != null)
        {
            startButton.SetActive(
                currentSession.IsHost && !startingGame && !leavingRoom
            );
        }
    }

    private void UpdatePlayerCount()
    {
        if (currentSession == null || playerCountText == null)
            return;

        playerCountText.text =
            "Players: " + currentSession.Players.Count +
            " / " + currentSession.MaxPlayers;
    }

    // =========================================================
    // NETWORK STATUS
    // =========================================================

    private void LogNetworkStatus()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("NetworkManager.Singleton is null.");
            return;
        }

        Debug.Log("NGO listening: " +
                  NetworkManager.Singleton.IsListening);

        Debug.Log("NGO IsHost: " +
                  NetworkManager.Singleton.IsHost);

        Debug.Log("NGO IsClient: " +
                  NetworkManager.Singleton.IsClient);
    }

    // =========================================================
    // START GAME — HOST ONLY
    // =========================================================

    public async void StartGame()
    {
        if (startingGame || leavingRoom)
            return;

        if (currentSession == null)
        {
            Debug.LogError("There is no active session.");

            if (statusText != null)
                statusText.text = "Room session is missing";

            return;
        }

        if (!currentSession.IsHost)
        {
            Debug.LogWarning("Only the host can start the game.");
            return;
        }

        NetworkManager nm = NetworkManager.Singleton;

        if (nm == null)
        {
            Debug.LogError("NetworkManager is missing!");

            if (statusText != null)
                statusText.text = "NetworkManager is missing";

            return;
        }

        if (!nm.IsListening)
        {
            Debug.LogError("Netcode is not listening.");

            if (statusText != null)
                statusText.text = "Network is not ready";

            return;
        }

        if (nm.SceneManager == null)
        {
            Debug.LogError(
                "Network Scene Management is unavailable. " +
                "Enable Scene Management in NetworkManager."
            );

            if (statusText != null)
                statusText.text = "Network scene management unavailable";

            return;
        }

        startingGame = true;

        if (startButton != null)
            startButton.SetActive(false);

        if (statusText != null)
            statusText.text = "Preparing game...";

        try
        {
            // Lock the room so nobody new can join once the game starts.
            if (lockRoomWhenGameStarts)
            {
                IHostSession hostSession = currentSession.AsHost();

                hostSession.IsLocked = true;

                await hostSession.SavePropertiesAsync();
            }

            if (statusText != null)
                statusText.text = "Starting game...";

            // Keep the session alive for the players already in the game.
            nm.SceneManager.LoadScene(
                gameSceneName,
                LoadSceneMode.Single
            );

            Debug.Log("Game scene load requested: " + gameSceneName);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to start the game.");
            Debug.LogException(e);

            if (statusText != null)
                statusText.text = "Failed to start game";

            startingGame = false;

            if (startButton != null && currentSession != null)
                startButton.SetActive(currentSession.IsHost);
        }
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    private void OnDestroy()
    {
        if (currentSession != null)
            currentSession.Changed -= OnSessionChanged;
    }
}
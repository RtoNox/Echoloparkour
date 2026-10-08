using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    private ISession activeSession;

    public string JoinCode => activeSession?.Code ?? "";

    private async void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log("Signed in as: " +
                AuthenticationService.Instance.PlayerId);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public async void CreateLobby()
    {
        try
        {
            SessionOptions options = new SessionOptions
            {
                MaxPlayers = 4,
                IsPrivate = false,
                IsLocked = false
            }
            .WithRelayNetwork();

            activeSession =
                await MultiplayerService.Instance.CreateSessionAsync(options);

            Debug.Log("Lobby Created!");
            Debug.Log("Join Code: " + activeSession.Code);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public async void JoinLobby(string joinCode)
    {
        try
        {
            activeSession =
                await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);

            Debug.Log("Joined Lobby!");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public async void LeaveLobby()
    {
        if (activeSession == null)
            return;

        try
        {
            await activeSession.LeaveAsync();
            activeSession = null;

            Debug.Log("Left Lobby");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
}
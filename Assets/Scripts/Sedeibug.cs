using Unity.Services.Multiplayer;
using UnityEngine;

public class SessionDebug : MonoBehaviour
{
    public void OnJoinedSession(ISession session)
    {
        Debug.Log("Joined Session");
        Debug.Log("Session Host: " + session.IsHost);
    }
}
using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using ParrelSync;
#endif

public class PlayerName : NetworkBehaviour
{
    [Header("Name Tag")]
    [SerializeField] private Text nameText;

    private NetworkVariable<Unity.Collections.FixedString64Bytes> playerName =
        new NetworkVariable<Unity.Collections.FixedString64Bytes>(
            new Unity.Collections.FixedString64Bytes(""),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public override void OnNetworkSpawn()
    {
        playerName.OnValueChanged += OnNameChanged;

        if (IsOwner)
        {
            string savedName = GetSavedPlayerName();

            Debug.Log(
                "Player " + OwnerClientId +
                " is using name: " + savedName
            );

            SetPlayerNameServerRpc(
                new Unity.Collections.FixedString64Bytes(savedName)
            );
        }

        UpdateNameText(playerName.Value.ToString());
    }

    private void OnDestroy()
    {
        playerName.OnValueChanged -= OnNameChanged;
    }

    private string GetSavedPlayerName()
    {
        string key = GetPlayerNameKey();

        string savedName = PlayerPrefs.GetString(key, "");

        Debug.Log(
            "Reading player name. Key: " +
            key +
            " | Value: " +
            savedName
        );

        if (string.IsNullOrWhiteSpace(savedName))
        {
            savedName = "Player_" + Random.Range(100, 999);

            Debug.Log(
                "No name found. Generated: " +
                savedName
            );
        }

        if (savedName.Length > 63)
            savedName = savedName.Substring(0, 63);

        return savedName;
    }

    private string GetPlayerNameKey()
    {
#if UNITY_EDITOR
        if (ClonesManager.IsClone())
        {
            string cloneArgument = ClonesManager.GetArgument();

            return "PlayerName_Clone_" + cloneArgument;
        }

        return "PlayerName_Original";
#else
        return "PlayerName";
#endif
    }

    private void OnNameChanged(
        Unity.Collections.FixedString64Bytes oldName,
        Unity.Collections.FixedString64Bytes newName)
    {
        Debug.Log(
            "Player " + OwnerClientId +
            " name changed: " +
            oldName.ToString() +
            " -> " +
            newName.ToString()
        );

        UpdateNameText(newName.ToString());
    }

    private void UpdateNameText(string newName)
    {
        if (nameText != null)
        {
            nameText.text = newName;
        }
    }

    [ServerRpc]
    private void SetPlayerNameServerRpc(
        Unity.Collections.FixedString64Bytes newName)
    {
        if (newName.Length == 0)
        {
            newName =
                new Unity.Collections.FixedString64Bytes(
                    "Player_" + Random.Range(100, 999)
                );
        }

        playerName.Value = newName;
    }

    public string PlayerNameValue
    {
        get {return playerName.Value.ToString();}
    }

}
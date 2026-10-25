using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using ParrelSync;
#endif

public class MainMenuManager : MonoBehaviour
{
    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }
    
    public GameObject profileScreenUI;

    [Header("Profile")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Text savedNameText;

    public void OnHostButtonClicked()
    {
        //SavePlayerName();

        PlayerPrefs.SetString("UserRole", "Host");
        PlayerPrefs.Save();

        SceneManager.LoadScene("NewLobby");
    }

    public void OnJoinButtonClicked()
    {
        //SavePlayerName();

        PlayerPrefs.SetString("UserRole", "Client");
        PlayerPrefs.Save();
        
        SceneManager.LoadScene("JoinRoomByCode");
    }
}
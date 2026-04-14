using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Views.LobbyView
{
    public class LobbyView : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject roomInfoPanel;

        [Header("Inputs & Texts")]
        [SerializeField] private TMP_InputField joinCodeInputField;
        [SerializeField] private TMP_Text displayCodeText;
        [SerializeField] private TMP_Text statusText;

        [Header("Buttons")]
        public Button randomMatchButton; // ✨ 랜덤 매치 버튼 추가
        public Button createRoomButton;
        public Button joinRoomButton;
        public Button cancelButton;

        public string GetInputCode() => joinCodeInputField.text.ToUpper();

        public void ShowMainMenu()
        {
            mainMenuPanel.SetActive(true);
            roomInfoPanel.SetActive(false);
        }

        public void ShowRoomInfo(string code, bool isRandomMatch = false)
        {
            mainMenuPanel.SetActive(false);
            roomInfoPanel.SetActive(true);
            
            if (isRandomMatch)
                displayCodeText.text = $"Waiting for random challenger";
            else
                displayCodeText.text = $"Join code: {code}";
        }

        public void UpdateStatus(string message)
        {
            statusText.text = message;
            Debug.Log(message);
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace DefaultNamespace
{
    public class LeftUpperController : MonoBehaviour
    {
        public static LeftUpperController Instance { get; private set; }

        private LeftUpper_Common ui_leftUpper;
        private Action backAction = null;
        
        private bool isOptionOpen = false;
        
        private void Awake() 
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start() {
            // CommonUIController의 스택 상태 변경 이벤트 구독
            if (CommonUIController.Instance != null)
            {
                // 처음 시작할 때 초기 상태 1회 동기화 (보통 로비이므로 false)
                UpdateBackButtonActive();
            }
        }

        public void RegisterView( LeftUpper_Common ui ) {
            ui_leftUpper = ui;
            
            // 일단 뒤로가기 버튼을 꺼
            if (IsBattle()) {
                ui_leftUpper.SetBackButtonActive(false);
            }
            
            // View의 이벤트 구독
            ui_leftUpper.OnClicked_Option += HandleOptionClicked;
            ui_leftUpper.OnClicked_Friend += HandleFriendClicked;
            ui_leftUpper.OnClicked_Back += HandleBackClicked;
        }
        
        private void HandleOptionClicked() {
            bool isLobby = IsLobby();
            
            if (!isOptionOpen) {
                UILoader.Instance.ShowUI(isLobby ? "Option_Lobby_Popup" : "Option_InGame_Popup");
            }
            else {
                UILoader.Instance.HideUI(isLobby ? "Option_Lobby_Popup" : "Option_InGame_Popup");
            }
            isOptionOpen = !isOptionOpen;
        }
        
        private void HandleFriendClicked() {
            FriendPopupController.Instance.ToggleOnOff_MainWindow();
        }

        private void HandleBackClicked()
        {
            // 1순위: 누군가 Set해둔 특수 동작이 있다면 그것부터 실행
            if (backAction != null)
            {
                backAction.Invoke();
            }
            // 2순위: Set된 게 없다면, 기본 스택 Pop 동작 실행
            else if (CommonUIController.Instance != null)
            {
                CommonUIController.Instance.GoBackToPreviousFullScreen();
            }
        }

        public void SetBackAction(Action action) {
            backAction = action;
            UpdateBackButtonActive();
        }

        // 뒤로가기 버튼 SetActive 최신화
        public void UpdateBackButtonActive() {
            if (ui_leftUpper == null) return;

            // 커스텀 액션이 있거나(특수 상황), 스택에 돌아갈 화면이 남아있다면(일반 상황) 켠다.
            bool hasCustomAction = backAction != null;
            bool hasHistory = CommonUIController.Instance != null && CommonUIController.Instance.CanGoBack;

            ui_leftUpper.SetBackButtonActive(hasCustomAction || hasHistory);
        }
        
        private bool IsLobby() {
            var currentScene = SceneManager.GetActiveScene();
            return currentScene.name == "01_Lobby_Crocobob";
        }

        private bool IsBattle() {
            var currentScene = SceneManager.GetActiveScene();
            return currentScene.name == "02_Battle_Crocobob";
        }
    }
}

using System;
using System.Collections.Generic;
using Cards.CardUIDatas;
using Models.SpellPayloads;
using UnityEngine;
using WebSocketSharp;

namespace DefaultNamespace
{
    public class CommonUIController : MonoBehaviour
    {
        #region Singleton & initialization
        public static CommonUIController Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        #endregion
        
        [SerializeField] private Stack<string> fullScreenUiHistoryStack = new Stack<string>();
        public string currentFullScreenUI = string.Empty;
        
        public bool CanGoBack => fullScreenUiHistoryStack.Count > 0;
        
        public void ShowRedAlert( string text ) {
            UILoader.Instance.ShowUI<string>("RedAlert_Common", text);
        }
        
        public void ShowBlackAlert( string text ) {
            UILoader.Instance.ShowUI<string>("BlackAlert_Common", text);
        }
        
        public void ShowLoading() {
            UILoader.Instance.ShowUI("Loading_Common");
        }
        
        public void DoneLoading() {
            UILoader.Instance.HideUI("Loading_Common");
        }
        
        public void ChangeFullScreen(string target) {
            // 현재 열려있는 화면을 스택에 저장 (최초 화면 제외)
            if (currentFullScreenUI == target) {
                Debug.LogWarning("이미 해당 Full Screen UI가 활성화되어 있습니다.");
                return;
            }
            
            // 직전 화면으로 돌아가려고 하면
            if (fullScreenUiHistoryStack.Count > 0 && fullScreenUiHistoryStack.Peek() == target) {
                Debug.Log($"[UI 스택] 직전 화면({target})으로 돌아갑니다. 스택 Pop 실행.");
                
                fullScreenUiHistoryStack.Pop();
                SwitchUI(target);
                
                return;
            }

            // 새로운 화면으로 갈 때
            if (!string.IsNullOrEmpty(currentFullScreenUI))
            {
                // 기존 화면을 히스토리 스택에 저장
                fullScreenUiHistoryStack.Push(currentFullScreenUI);
            }

            // 실제 UI 활성화 처리
            SwitchUI(target);

            if (currentFullScreenUI == "Lobby_FullScreen") {
                fullScreenUiHistoryStack = new Stack<string>();
                LeftUpperController.Instance.SetBackAction(null);
            }
        }
        
        // 실제 게임오브젝트를 켜고 끄는 내부 로직
        private void SwitchUI(string targetUIName)
        {
            if (!currentFullScreenUI.IsNullOrEmpty()) {
                UILoader.Instance.HideUI(currentFullScreenUI);
            }
            UILoader.Instance.ShowUI(targetUIName);
            
            currentFullScreenUI = targetUIName;

            LeftUpperController.Instance.UpdateBackButtonActive();
        }
                
        public void GoBackToPreviousFullScreen() {
            if (fullScreenUiHistoryStack.Count <= 0) return;
            
            string previousUI = fullScreenUiHistoryStack.Peek(); 
            ChangeFullScreen(previousUI);
        }

        public void InitFullScreenStack() {
            fullScreenUiHistoryStack.Clear();
            currentFullScreenUI = string.Empty;
        }



        [ContextMenu("SpellActive Test Start")]
        public void SpellActiveTest() {
            UILoader.Instance.ShowUI("SpellActive_FullScreen", ("이 멋진 세계에 축복을!", Property.Fire));
        }

        [ContextMenu("SpellActive Test Stop")]
        public void SpellActiveStop() {
            UILoader.Instance.HideUI("SpellActive_FullScreen");
        }

        
        [ContextMenu("Show Spell UI")]
        public void ShowSpellUI() {
            UILoader.Instance.ShowUI("Spell_FullScreen", new SpellPayload());

        }

        [ContextMenu("Show Spell Active UI")]
        public void ShowSpellActiveUI() {
            UILoader.Instance.ShowUI("SpellActive_FullScreen", ("testtesttesttest", Property.Fire));
        }
    }
}

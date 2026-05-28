using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace
{
    public class FriendPopupController : MonoBehaviour {
        
        public static FriendPopupController Instance { get; private set; }
        private void Awake() {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        
        
        private Friend_MainWindow ui_mainWindow;
        private Friend_SearchWindow _uiSearchWindowWindow;
        
        private bool isMainWindowOpen = false;
        private bool isSearchWindowOpen = false;

        #region MainWindow
        public void RegisterMainWindow(Friend_MainWindow ui) {
            ui_mainWindow = ui;

            ui_mainWindow.OnClick_AddFriend += ToggleOnOff_SearchWindow;
            ui_mainWindow.OnClick_Alert += OpenAlert;
            
            ui_mainWindow.OnClick_FriendDetail += ShowFriendDetail;
        }
        
        public void UpdateUI_MainWindow() 
        {
            var myProfile = LoadMyProfile();
            var friendList = LoadFriendList();
            
            ui_mainWindow.UpdateUI(myProfile, friendList);
        }
        
        public void ToggleOnOff_MainWindow() {
            if (!isMainWindowOpen) {
                UILoader.Instance.ShowUI("Friend_MainWindow");
            }
            else {
                UILoader.Instance.HideUI("Friend_MainWindow");
            }
            isMainWindowOpen = !isMainWindowOpen;
        }


        public void OpenAlert() {
            
        }

        public void ShowFriendDetail(int userId) {
            // TODO : 유저 ID를 바탕으로 서버에서 상세 정보를 긁어와서 새로운 팝업 띄우기
            Debug.Log($"[유저 ID: {userId}] 친구 상세정보 창 팝업 Controller에서 실행됨!");
        }
        
        
        private (int, int, string) LoadMyProfile() 
        {
            // TODO: 실제 서버나 로컬 데이터 연동
            return (5, 12334, "Crocobob");
        }
        
        private List<FriendDataForUI> LoadFriendList() 
        {
            // 테스트용 더미 데이터
            return new List<FriendDataForUI> 
            {
                new FriendDataForUI { userId = 1, tier = 1, score = 4, name = "name1", onlineStatus = OnlineStatus.Online },
                new FriendDataForUI { userId = 2, tier = 2, score = 5, name = "name2", onlineStatus = OnlineStatus.Away },
                new FriendDataForUI { userId = 3, tier = 3, score = 6, name = "name3", onlineStatus = OnlineStatus.Offline }
            };
        }
        
        #endregion
        
        
        #region Search
        public void RegisterSearchWindow(Friend_SearchWindow ui) 
        {
            _uiSearchWindowWindow = ui;

            // 이벤트 구독
            _uiSearchWindowWindow.OnSubmit_Search += RequestSearchUser;
            _uiSearchWindowWindow.OnClick_AddFriend += RequestSendFriend;
        }
        
        public void ToggleOnOff_SearchWindow() {
            if (!isSearchWindowOpen) {
                UILoader.Instance.ShowUI("Friend_SearchWindow");
            }
            else {
                UILoader.Instance.HideUI("Friend_SearchWindow");
            }
            isSearchWindowOpen = !isSearchWindowOpen;
        }

        // 1. 유저 검색 처리
        private void RequestSearchUser(string searchName) 
        {
            Debug.Log($"서버에 '{searchName}' 유저 검색을 요청합니다...");
            Debug.Log("지금은 구현된 게 없으니, 아무튼 됐다 치고 진행합니다?");

            // TODO: 실제 서버 API(UGS 등) 호출 및 콜백 대기

            // --- 서버 응답 더미 시뮬레이션 ---
            bool isUserFound = true; // 서버에서 찾았다고 가정

            if (isUserFound) 
            {
                var resultData = new FriendDataForUI {
                    userId = 777, // 서버가 내려준 고유 ID
                    name = searchName,
                    onlineStatus = OnlineStatus.Online
                };
                var resultList = new List<FriendDataForUI>() {
                    resultData
                };
                
                // View에 데이터 전달하여 결과 띄우기
                _uiSearchWindowWindow.ShowSearchResult(resultList);
            }
            else 
            {
                _uiSearchWindowWindow.ClearSearchResult();
                CommonUIController.Instance.ShowBlackAlert("해당 이름을 가진 유저가 없습니다.");
            }
        }

        // 2. 친구 추가 요청 처리
        private void RequestSendFriend(int targetUserId) 
        {
            Debug.Log("미구현입니다. 대충 보냈따고 칩니다?");
            Debug.Log($"서버에 유저 ID [{targetUserId}] 에게 친구 요청 패킷을 발송합니다.");

            // TODO: 친구 요청 서버 API 호출 로직
        }
        #endregion
    }
    
    
    public enum OnlineStatus {
        Online,
        Away,
        Offline
    }

    public struct FriendDataForUI {
        public int userId;
        public int tier;
        public int score;
        public string name;
        public OnlineStatus onlineStatus;
    }
}

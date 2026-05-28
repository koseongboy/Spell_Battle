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
        private Friend_SearchWindow ui_SearchWindowWindow;
        private Friend_DetailWindow ui_detailWindow;
        
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
            ui_SearchWindowWindow = ui;

            // 이벤트 구독
            ui_SearchWindowWindow.OnSubmit_Search += RequestSearchUser;
            ui_SearchWindowWindow.OnClick_AddFriend += RequestSendFriend;
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
                ui_SearchWindowWindow.ShowSearchResult(resultList);
            }
            else 
            {
                ui_SearchWindowWindow.ClearSearchResult();
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
        
        #region DetailWindow
        public void RegisterDetailWindow(Friend_DetailWindow ui) 
        {
            ui_detailWindow = ui;
        }

        // 닫기 함수
        public void CloseDetailWindow() 
        {
            UILoader.Instance.HideUI("Friend_DetailWindow");
        }

        // 🌟 메인 친구 리스트에서 특정 친구의 Piece를 클릭했을 때 실행됨
        public void ShowFriendDetail(int userId) 
        {
            // 1. 내가 가지고 있는 전체 친구 리스트 데이터 중에서, 해당 ID를 가진 유저 정보를 찾습니다.
            var currentFriendList = LoadFriendList(); // 실제로는 클래스 전역에 캐싱해둔 리스트를 사용해야 합니다.
    
            // LINQ 등을 사용해 ID 매칭
            FriendDataForUI targetFriendData = currentFriendList.Find(f => f.userId == userId);

            // 2. 만약 데이터를 찾았다면, UILoader의 <T> 제네릭 기능을 이용해 창을 띄우면서 데이터를 쏴줍니다.
            if (targetFriendData.userId != 0) // struct 초기화 검증 (또는 별도 예외처리)
            {
                UILoader.Instance.ShowUI<FriendDataForUI>("Friend_DetailWindow", targetFriendData);
            }
            else
            {
                Debug.LogWarning("해당 친구의 데이터를 찾을 수 없습니다.");
            }
        }

        // [게임 초대] 버튼 클릭 시
        public void RequestInviteGame(int targetUserId)
        {
            Debug.Log($"서버를 통해 유저 ID [{targetUserId}] 에게 게임 초대장(로비 코드 등)을 발송합니다.");
            // TODO: Unity Relay/Lobby의 '초대 코드(Join Code)'를 대상 유저에게 전송하는 API 연동
    
            // 필요 시 초대 완료 후 알림 팝업
        }

        // [친구 삭제] 버튼 클릭 시
        public void RequestDeleteFriend(int targetUserId)
        {
            Debug.Log($"서버에 유저 ID [{targetUserId}] 친구 삭제를 요청합니다.");
    
            // TODO: UGS Friends API를 통한 삭제 연동
    
            // 삭제가 완료되면 상세창을 닫고 메인 리스트를 갱신합니다.
            CloseDetailWindow();
            UpdateUI_MainWindow();
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

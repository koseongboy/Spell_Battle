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
        private bool isWindowOpen = false;
        
        
        public void RegisterMainWindow(Friend_MainWindow ui) {
            ui_mainWindow = ui;

            ui_mainWindow.OnClick_AddFriend += OpenAddFriend;
            ui_mainWindow.OnClick_Alert += OpenAlert;
            
            ui_mainWindow.OnClick_FriendDetail += ShowFriendDetail;
        }
        
        public void UpdateUI_MainWindow() 
        {
            var myProfile = LoadMyProfile();
            var friendList = LoadFriendList();
            
            ui_mainWindow.UpdateUI(myProfile, friendList);
        }
        
        public void ToggleOnOff() {
            if (!isWindowOpen) {
                UILoader.Instance.ShowUI("Friend_MainWindow");
            }
            else {
                UILoader.Instance.HideUI("Friend_MainWindow");
            }
            isWindowOpen = !isWindowOpen;
        }
        
        public void OpenAddFriend() {
            
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

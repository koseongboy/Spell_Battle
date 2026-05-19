using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace DefaultNamespace
{
    public class Friend_MainWindow : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.Popup;
        
        [SerializeField] private TextMeshProUGUI txt_Tier;
        [SerializeField] private TextMeshProUGUI txt_Score;
        [SerializeField] private TextMeshProUGUI txt_Name;

        public void CloseUI() {
            UILoader.Instance.HideUI("Friend_MainWindow");
        }

        public void OpenRequestUI() {
            
        }

        public void OpenSearchUI() {
            
        }

        public void OpenFriendDetailUI() {
            
        }
        
        
        
        
        private void OnEnable() {
            Debug.Log("[Friend_MainWindow] OnEnable]");
            (int rank, int score, string name) myProfileData = LoadMyProfile();
            List<FriendDataForUI> friendDataList = LoadFriendList();

            UpdateUI( myProfileData, friendDataList);
        }

        private void UpdateUI( (int,int,string) profileData, List<FriendDataForUI> fDataList ) {
            UpdateUI_MyProfile( profileData );
            UpdateUI_FriendList( fDataList );
        }

        private void UpdateUI_MyProfile((int tier, int score, string name) profileData) {
            txt_Tier.text = profileData.tier.ToString();
            txt_Score.text = profileData.score.ToString();
            txt_Name.text = profileData.name;
        }
        
        private void UpdateUI_FriendList( List<FriendDataForUI> fDataList ) {
            
        }

        
        private (int, int, string) LoadMyProfile() {
            // TODO
            return (5, 12334, "Crocobob");
        }
        
        private List<FriendDataForUI> LoadFriendList() {
            // TODO
            
            FriendDataForUI data1 = new FriendDataForUI();
            FriendDataForUI data2 = new FriendDataForUI();
            FriendDataForUI data3 = new FriendDataForUI();
            
            data1.tier = 1;
            data2.tier = 2;
            data3.tier = 3;
            
            data1.score = 4;
            data2.score = 5;
            data3.score = 6;
            
            data1.tier = 1;
            data2.tier = 2;
            data3.tier = 3;
            
            data1.userId = 1;
            data2.userId = 2;
            data3.userId = 3;
            
            data1.name = "name1";
            data2.name = "name2";
            data3.name = "name3";
                        
            data1.onlineStatus = OnlineStatus.Online;
            data2.onlineStatus = OnlineStatus.Away;
            data3.onlineStatus = OnlineStatus.Offline;
            
            return new List<FriendDataForUI> { data1, data2, data3 };
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

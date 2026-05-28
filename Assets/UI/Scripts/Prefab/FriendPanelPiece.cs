using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class FriendPanelPiece : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txt_Name;
        [SerializeField] private TextMeshProUGUI txt_Status;
        [SerializeField] private Button btn_Detail; // 상세정보 보기 버튼
        [SerializeField] private Image img_Status;
        
        private int currentUserId;
        private Action<int> onClickDetailCallback;

        // 풀에서 처음 생성될 때 딱 한 번 호출되는 초기화 함수
        public void Init(Action<int> detailCallback) 
        {
            onClickDetailCallback = detailCallback;
            
            // 내 버튼이 눌리면, 주입받은 Action에 내 ID를 담아서 발사함
            btn_Detail.onClick.AddListener(() => 
            {
                onClickDetailCallback?.Invoke(currentUserId);
            });
        }

        // 리스트가 갱신될 때마다 데이터를 덮어씌우는 함수
        public void SetData(FriendDataForUI data) 
        {
            currentUserId = data.userId;
            txt_Name.text = data.name;
            txt_Status.text = data.onlineStatus.ToString();

            if (data.onlineStatus == OnlineStatus.Online) {
                img_Status.color = Color.green;
                txt_Name.color = Color.white;
                txt_Status.color = Color.white;
            }else if (data.onlineStatus == OnlineStatus.Away) {
                img_Status.color = Color.yellow;
                txt_Name.color = Color.white;
                txt_Status.color = Color.white;
            }else if (data.onlineStatus == OnlineStatus.Offline) {
                img_Status.color = Color.gray;
                txt_Name.color = Color.gray;
                txt_Status.color = Color.gray;
            }
        }
    }
}

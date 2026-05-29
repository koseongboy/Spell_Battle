using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class FriendRequestPiece : MonoBehaviour 
    {
        [SerializeField] private TextMeshProUGUI txt_Name;
        [SerializeField] private Button btn_Accept; // 초록색 체크 버튼
        [SerializeField] private Button btn_Reject; // 빨간색 X 버튼

        private int currentUserId;
        private Action<int> onAcceptCallback;
        private Action<int> onRejectCallback;

        // 🌟 Controller로 수락/거절 이벤트를 각각 쏴주기 위해 2개의 Action을 주입받습니다.
        public void Init(Action<int> onAccept, Action<int> onReject) 
        {
            onAcceptCallback = onAccept;
            onRejectCallback = onReject;
            
            // 기존 리스너 초기화 후 재할당 (오브젝트 풀 재활용 대비)
            btn_Accept.onClick.RemoveAllListeners();
            btn_Accept.onClick.AddListener(() => onAcceptCallback?.Invoke(currentUserId));

            btn_Reject.onClick.RemoveAllListeners();
            btn_Reject.onClick.AddListener(() => onRejectCallback?.Invoke(currentUserId));
        }

        public void SetData(FriendDataForUI data) 
        {
            currentUserId = data.userId;
            txt_Name.text = data.name;
        }
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class FriendSearchPiece : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txt_Name;
        [SerializeField] private Image img_OnlineStatus;
        [SerializeField] private Button btn_AddFriend;

        private int currentUserId;
        private Action<int> onClickAddFriendCallback;

        public void Init(Action<int> addFriendCallback) 
        {
            onClickAddFriendCallback = addFriendCallback;
            
            // 기존에 연결된 리스너가 있다면 지우고 새로 연결 (풀링 재활용 시 중복 방지)
            btn_AddFriend.onClick.RemoveAllListeners();
            btn_AddFriend.onClick.AddListener(() => 
            {
                onClickAddFriendCallback?.Invoke(currentUserId);
            });
        }

        public void SetData(FriendDataForUI data) 
        {
            currentUserId = data.userId;
            txt_Name.text = data.name;
            
            img_OnlineStatus.color = data.onlineStatus switch {
                OnlineStatus.Online => Color.green,
                OnlineStatus.Away => Color.yellow,
                OnlineStatus.Offline => Color.gray,
                _ => Color.gray
            };
        }
    }
}

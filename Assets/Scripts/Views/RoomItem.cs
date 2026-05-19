using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Lobbies.Models;
using System;

namespace Views.LobbyView
{
    public class RoomItem : MonoBehaviour
    {
        [Header("개발용 룸 아이디 보기")]
        [SerializeField] private string selectedLobbyId; // 현재 유저가 클릭한 방의 ID 보관용
        
        [Header("UI 텍스트 컴포넌트")]
        public TextMeshProUGUI roomNameText;    // 방 제목 텍스트 (예: 초보만 오세요)
        public TextMeshProUGUI hostNameText;    // 방장 이름 텍스트 (예: 고성보이)
        public TextMeshProUGUI playerCountText; // 인원수 텍스트 (예: 1 / 2)

        [Header("상호작용 버튼")]
        public Button itemButton;               // 이 프리팹을 클릭할 수 있게 해주는 전체 버튼
        

        // 내 방의 고유 정보를 기억할 변수들
        private string myLobbyId;
        private string myRoomName;
        
        // 부모(DevLobbyView)에게 클릭 사실을 알릴 연락망(콜백 함수)
        private Action<string, string> onSelectedCallback;

        // 🌟 부모(DevLobbyView)가 이 프리팹을 생성하자마자 가장 먼저 호출해 주는 세팅 함수
        public void Setup(Lobby lobby, Action<string, string> callback, string hostName = "Host")
        {
            myLobbyId = lobby.Id;
            myRoomName = lobby.Name;
            onSelectedCallback = callback;

            // 1. 텍스트 UI 업데이트
            if (roomNameText != null) roomNameText.text = lobby.Name;
            if (playerCountText != null) playerCountText.text = $"{lobby.Players.Count} / {lobby.MaxPlayers}";

            // (참고: 현재 유니티 로비 서버 기본 정보에는 방장의 '닉네임'이 문자열로 들어있지 않습니다. 
            // 추후 방 생성 시 Data에 닉네임을 같이 넣어주거나 유저 계정 연동 전까지는 임시로 Host로 표시합니다.) todo.
            if (hostNameText != null) hostNameText.text = hostName; 

            // 2. 버튼 클릭 이벤트 연결
            if (itemButton != null)
            {
                // 혹시 모를 중복 클릭 구독을 방지하기 위해 싹 지우고 다시 연결합니다.
                itemButton.onClick.RemoveAllListeners();
                itemButton.onClick.AddListener(OnClickItem);
            }
        }

        // 🌟 유저가 화면에서 이 방을 마우스로 클릭했을 때 실행되는 함수
        private void OnClickItem()
        {
            // 부모가 넘겨준 연락망(함수)이 비어있지 않다면, 내 ID와 이름을 담아서 찔러줍니다!
            onSelectedCallback?.Invoke(myLobbyId, myRoomName);
        }
    }
}
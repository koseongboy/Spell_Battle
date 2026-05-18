using System;
using Unity.Netcode;
using UnityEngine;

namespace Models.CardModels
{
    public class GraveyardModel : NetworkBehaviour
    {
        // ==========================================
        // 📢 [공용 데이터] 무덤은 모두가 아는 정보이므로 NetworkList가 완벽한 정답입니다!
        // ==========================================
        public NetworkList<int> PublicGraveyard;

        // View 단에서 화면을 갱신하기 위해 구독할 이벤트 (선택 사항)
        public event Action<NetworkListEvent<int>> OnGraveyardChanged;

        public void Awake()
        {
            PublicGraveyard = new NetworkList<int>(
                new int[0], 
                NetworkVariableReadPermission.Everyone, 
                NetworkVariableWritePermission.Server
            );
        }

        public override void OnNetworkSpawn()
        {
            // 리스트에 변화가 생기면 이벤트를 발생시키도록 구독
            PublicGraveyard.OnListChanged += HandleGraveyardChanged;
        }

        public override void OnNetworkDespawn()
        {
            // 메모리 누수 방지
            PublicGraveyard.OnListChanged -= HandleGraveyardChanged;
        }

        // ==========================================
        // 🌟 자동화된 동기화 로직
        // ==========================================
        private void HandleGraveyardChanged(NetworkListEvent<int> changeEvent)
        {
            // 누군가 카드를 썼거나 버려서 무덤에 추가/삭제되었을 때
            // View(UI) 스크립트 쪽에 "무덤 UI 갱신해!" 라고 알려줍니다.
            OnGraveyardChanged?.Invoke(changeEvent);
            
            if (changeEvent.Type == NetworkListEvent<int>.EventType.Add)
            {
                Debug.Log($"[Client & Server] {changeEvent.Value}번 카드가 무덤으로 들어왔습니다. (현재 {PublicGraveyard.Count}장)");
            }
        }

        // ==========================================
        // [서버 영역] 카드를 사용하거나 버릴 때 호출됨
        // ==========================================
        public void AddCardToGraveyard(int cardId)
        {
            // Write 권한이 Server에게 있으므로, 반드시 서버에서만 추가해야 합니다.
            if (!IsServer) return;

            // 🌟 이 한 줄만 실행하면, 나머지 모든 클라이언트의 OnListChanged가 자동으로 터집니다!
            PublicGraveyard.Add(cardId);
        }

        
    }
}

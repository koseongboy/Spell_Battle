using Unity.Netcode;
using UnityEngine;

namespace Models.CardModels
{
    public class GraveyardModel : NetworkBehaviour
    {
        // ==========================================
        // 📢 [공용 데이터] 무덤에 들어간 카드들의 ID 리스트 (모든 클라이언트 동기화)
        // ==========================================
        public NetworkList<int> PublicGraveyard;

        public void Awake()
        {
            // NetworkList는 반드시 Awake에서 공간을 할당해 주어야 합니다.
            PublicGraveyard = new NetworkList<int>(
                null, 
                NetworkVariableReadPermission.Everyone, 
                NetworkVariableWritePermission.Server
            );
        }

        // ==========================================
        // [서버 영역] 카드를 사용하거나 버릴 때 호출됨
        // ==========================================
        public void AddCardToGraveyard(int cardId)
        {
            // Write 권한이 Server에게 있으므로, 반드시 서버에서만 추가
            if (!IsServer) return;

            // 리스트에 추가하는 순간, 모든 클라이언트의 OnListChanged 이벤트가 자동으로 터집니다!
            PublicGraveyard.Add(cardId);
            Debug.Log($"[Server] {cardId}번 카드가 무덤으로 이동. (현재 무덤: {PublicGraveyard.Count}장)");
        }
    }
}
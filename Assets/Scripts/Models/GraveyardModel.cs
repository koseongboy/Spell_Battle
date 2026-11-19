using Unity.Netcode;
using UnityEngine;

namespace Models.CardModels
{
    public class GraveyardModel : NetworkBehaviour
    {
        // 무덤은 공개 정보이므로 NetworkList로 양측에 그대로 동기화 (쓰기는 서버만)
        public NetworkList<int> PublicGraveyard;

        public void Awake()
        {
            // NetworkList는 NetworkBehaviour 초기화 전에 생성되어야 하므로 Awake에서 할당
            PublicGraveyard = new NetworkList<int>(
                null, 
                NetworkVariableReadPermission.Everyone, 
                NetworkVariableWritePermission.Server
            );
        }

        // [서버] 사용한 카드를 무덤에 추가. 클라이언트에는 OnListChanged로 전파됨
        public void AddCardToGraveyard(int cardId)
        {
            if (!IsServer) return;

            PublicGraveyard.Add(cardId);
            Debug.Log($"[Server] {cardId}번 카드가 무덤으로 이동. (현재 무덤: {PublicGraveyard.Count}장)");
        }
    }
}
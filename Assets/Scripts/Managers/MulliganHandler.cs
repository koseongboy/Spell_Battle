using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Models.PlayerModels;
using Models.TurnModel;

namespace DefaultNamespace
{
    public class MulliganHandler : NetworkBehaviour
    {
        [Header("연결된 플레이어 모델")]
        public PlayerModel model;

        // 선택된 멀리건 인덱스 저장소
        private HashSet<int> _selectedMulliganIndices = new HashSet<int>();

        private void Awake() {
            model = this.GetComponent<PlayerModel>();
        }


        // 멀리건 UI 화면(또는 단축키)에서 호출할 토글 함수
        public void ToggleMulliganIndex(int index)
        {
            if (_selectedMulliganIndices.Contains(index))
            {
                _selectedMulliganIndices.Remove(index);
                Debug.Log($"[Mulligan] ❌ {index + 1}번 카드 교체 등록 취소");
            }
            else
            {
                _selectedMulliganIndices.Add(index);
                Debug.Log($"[Mulligan] 🛡️ {index + 1}번 카드 교체 등록");
            }
        }

        // 멀리건 UI 화면 하단의 '교환' 버튼(또는 M키)에서 호출할 제출 함수
        public void SubmitFinalMulligan()
        {
            List<int> replaceCardIds = new List<int>();

            foreach (int index in _selectedMulliganIndices)
            {
                // 인덱스를 실제 카드 고유 ID로 변환
                int cardId = model.Hand.GetCardIdAt(index); 
                replaceCardIds.Add(cardId);
            }

            Debug.Log($"[Mulligan] 🚀 총 {replaceCardIds.Count}장의 카드 교체를 서버에 요청합니다!");

            // 🌟 수정됨: TurnController가 아닌 MatchManager로 RPC 발송!
            MatchManager.Instance.SubmitMulliganServerRpc(replaceCardIds.ToArray());

            // 기록 초기화
            _selectedMulliganIndices.Clear();
        }
    }
}
using Unity.Netcode;
using UnityEngine;
using Models.PlayerModels;
using Views.PlayerView;

namespace Controllers.PlayerController
{
    public class PlayerController : NetworkBehaviour
    {
        [Header("MVP References")]
        public PlayerModel model;
        public PlayerView view;

        public override void OnNetworkSpawn()
        {
            // ==========================================
            // 1. 초기 화면 세팅 (스폰 직후 한 번 실행)
            // ==========================================
            view.UpdateHealth(model.CurrentHealth.Value);
            view.UpdateMana(model.CurrentMana.Value);
            view.UpdateStatuses(model.ActiveStatuses);

            // ==========================================
            // 2. 데이터 변경 '구독' (핵심 파트!)
            // ==========================================
            
            // 체력이 변할 때 -> View의 UpdateHealth 실행
            model.CurrentHealth.OnValueChanged += (oldValue, newValue) => 
            {
                view.UpdateHealth(newValue);
            };

            // 마나가 변할 때 -> View의 UpdateMana 실행
            model.CurrentMana.OnValueChanged += (oldValue, newValue) => 
            {
                view.UpdateMana(newValue);
            };

            // 🌟 상태이상(리스트)이 변할 때 -> View의 UpdateStatuses 실행
            model.ActiveStatuses.OnListChanged += HandleStatusChanged;
        }

        public override void OnNetworkDespawn()
        {
            // 구독 해제 (메모리 누수 방지)
            model.CurrentHealth.OnValueChanged -= (oldValue, newValue) => view.UpdateHealth(newValue);
            model.CurrentMana.OnValueChanged -= (oldValue, newValue) => view.UpdateMana(newValue);
            model.ActiveStatuses.OnListChanged -= HandleStatusChanged;
        }

        // NetworkList의 이벤트 핸들러
        private void HandleStatusChanged(NetworkListEvent<StatusData> changeEvent)
        {
            // 리스트에 추가, 삭제, 갱신 등 어떤 변화가 생기든
            // View에게 "리스트 전체 줄 테니까 다시 그려!" 라고 던져줌
            view.UpdateStatuses(model.ActiveStatuses);
        }
    }
}

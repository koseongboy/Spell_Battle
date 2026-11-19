using System;
using Unity.Netcode;
using UnityEngine;

namespace Models.TurnModel
{
    public enum GamePhase
    {
        Wait,          // 0. 양측 플레이어 접속, 덱 제출 및 레디 대기
        Mulligan,      // 1. 선후공 결정 후 초기 손패 교체 (게임 중 1회만 발생)
        Draw,          // 2. 턴 시작, 덱에서 카드 뽑기
        Select,        // 3. 사용할 카드를 고르는 단계
        Incantation,   // 4. 마이크로 주문을 영창하고 AI 평가를 받는 단계
        Battle,        // 5. 서버가 평가 결과를 검증하고 카드 효과를 집행하는 단계
        End,           // 6. 턴 종료 처리 및 상대에게 턴 넘기기
        EndGame        // 7. 게임 종료
    }

    /// <summary>
    /// 턴/페이즈 상태를 담는 네트워크 모델. 값 쓰기는 서버(PhaseManager, MatchManager)만 수행한다.
    /// </summary>
    public class TurnModel : NetworkBehaviour
    {
        public static TurnModel Instance { get; private set; }
        public NetworkVariable<ulong> CurrentTurnPlayerId = new NetworkVariable<ulong>(0);
        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Wait);

        [Header("NGO상 플레이어 아이디")]
        public NetworkVariable<ulong> HostId = new NetworkVariable<ulong>();
        public NetworkVariable<ulong> GuestId = new NetworkVariable<ulong>();

        public NetworkVariable<ulong> FirstPlayerId = new NetworkVariable<ulong>(0); // 선공 플레이어


        // UI 등 로컬 구독자용 이벤트: (새 페이즈, 내 턴 여부)
        public event Action<GamePhase, bool> OnPhaseChangedEvent;

        private void Awake()
        {
            if (Instance == null) 
            {
                Instance = this;
            }
            else 
            {
                Destroy(gameObject);
            }
        }

        public override void OnNetworkSpawn()
        {
            CurrentPhase.OnValueChanged += (oldPhase, newPhase) =>
            {
              bool isMyTurn = NetworkManager.Singleton.LocalClientId == CurrentTurnPlayerId.Value;
              OnPhaseChangedEvent?.Invoke(newPhase, isMyTurn);
            };
        }
    }
}

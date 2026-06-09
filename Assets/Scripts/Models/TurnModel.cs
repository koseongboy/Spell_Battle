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
        Incantation,   // 4. 스페이스바를 눌러 마법을 영창(마이크/타이핑)하는 단계
        Battle,        // 5. 서버가 영창을 평가하고 효과 및 데미지를 집행하는 단계
        End,            // 6. 턴 종료 처리 및 상대방에게 턴 넘기기
        EndGame         // 7. 게임 종료
    }
    
    public class TurnModel : NetworkBehaviour
    {
        public static TurnModel Instance { get; private set; }
        public NetworkVariable<ulong> CurrentTurnPlayerId = new NetworkVariable<ulong>(0);
        public NetworkVariable<GamePhase> CurrentPhase = new NetworkVariable<GamePhase>(GamePhase.Wait);

        [Header("NGO상 플레이어 아이디")]
        public NetworkVariable<ulong> HostId = new NetworkVariable<ulong>();
        public NetworkVariable<ulong> GuestId = new NetworkVariable<ulong>();

        public NetworkVariable<ulong> FirstPlayerId = new NetworkVariable<ulong>(0);
        

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

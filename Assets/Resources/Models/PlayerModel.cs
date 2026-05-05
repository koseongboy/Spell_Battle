using Unity.Netcode;
using UnityEngine;
using System;
using Models.TurnModel;

namespace Models.PlayerModel
{

    // 기획서에 명시된 상태이상 종류들
    public enum StatusType
    {
        None,
        Ignite,      // 발화 (불 - 도트뎀)
        Freeze,      // 빙결 (얼음 - 마나 감소 및 스택 폭발)
        Prophecy,    // 예언 (공허 - 공격력 강화 스택)
        ArcaneStack, // 응축 (비전 - 터뜨려서 데미지 증가)
        Shield       // 보호막 (흙)
    }

    // 🌟 네트워크로 전송할 상태이상 '택배 상자' (구조체)
    public struct StatusData : INetworkSerializable, IEquatable<StatusData>
    {
    public StatusType Type;
    public int Stacks;     // 중첩 수
    public int Duration;   // 지속 턴 수

    // NGO가 이 데이터를 0과 1로 변환(직렬화)하는 규칙
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Type);
        serializer.SerializeValue(ref Stacks);
        serializer.SerializeValue(ref Duration);
    }

    public bool Equals(StatusData other)
    {
        return Type == other.Type && Stacks == other.Stacks && Duration == other.Duration;
    }
}
    public class PlayerModel : NetworkBehaviour
    {
        [Header("Stats")]
        public NetworkVariable<int> MaxHealth = new NetworkVariable<int>(100);
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(100);
        
        public NetworkVariable<int> MaxMana = new NetworkVariable<int>(1);
        public NetworkVariable<int> FinalMana = new NetworkVariable<int>(10);
        public NetworkVariable<int> CurrentMana = new NetworkVariable<int>(10);

        // 🌟 핵심: 네트워크로 자동 동기화되는 상태이상 리스트
        // (일반 List나 Dictionary는 동기화가 안 되기 때문에 반드시 NetworkList를 써야 합니다!)
        public NetworkList<StatusData> ActiveStatuses;

        private void Awake()
        {
            // NetworkList는 반드시 Awake에서 공간을 할당해 주어야 합니다.
            ActiveStatuses = new NetworkList<StatusData>();
        }

        public override void OnNetworkSpawn()
        {
            // 턴 매니저 구독: 턴이 바뀔 때 발화 데미지를 입거나 마나를 채우기 위함
            if (TurnModel.TurnModel.Instance != null)
            {
                TurnModel.TurnModel.Instance.OnPhaseChangedEvent += HandlePhaseEffects;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (TurnModel.TurnModel.Instance != null)
            {
                TurnModel.TurnModel.Instance.OnPhaseChangedEvent -= HandlePhaseEffects;
            }
        }

        // ==========================================
        // [서버 전용 권한] 스탯 조작 함수들 (카드가 발동될 때 호출됨)
        // ==========================================

        public void TakeDamage(int amount)
        {
            if (!IsServer) return; // 오직 서버(방장)만 체력을 깎을 수 있음!
            
            // TODO: 나중에 여기에 "보호막(Shield)이 있으면 보호막부터 깎는다"는 로직을 추가합니다.
            
            CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - amount);
            Debug.Log($"[Player {OwnerClientId}] 데미지 {amount} 피격! 남은 체력: {CurrentHealth.Value}");
        }

        public void Heal(int amount)
        {
            if (!IsServer) return;
            CurrentHealth.Value = Mathf.Min(MaxHealth.Value, CurrentHealth.Value + amount);
        }

        public bool TryUseMana(int amount)
        {
            if (!IsServer) return false;
            if (CurrentMana.Value - amount >= 0)
            {
                CurrentMana.Value -= amount;
                return true;
            } else
            {
                return false;
            }
        }

        public void ManaHeal(int amount)
        {
            if (!IsServer) return;
            CurrentMana.Value = Mathf.Min(MaxMana.Value, CurrentMana.Value + amount);
        }

        public void IncreaseMaxMana(int amount)
        {
            if (!IsServer) return;
            MaxMana.Value = Mathf.Max(MaxMana.Value + amount, FinalMana.Value);
        }

        // ==========================================
        // [서버 전용 권한] 상태이상(스택) 관리 시스템
        // ==========================================

        public void AddStatus(StatusType type, int stacks, int duration = 1)
        {
            if (!IsServer) return;

            // 1. 이미 같은 상태이상이 있는지 확인
            for (int i = 0; i < ActiveStatuses.Count; i++)
            {
                if (ActiveStatuses[i].Type == type)
                {
                    // 기존 상태이상 덮어쓰기 (스택 누적)
                    var status = ActiveStatuses[i];
                    status.Stacks += stacks;
                    status.Duration = Mathf.Max(status.Duration, duration); // 지속 턴 갱신 (todo)
                    ActiveStatuses[i] = status; 
                    
                    Debug.Log($"[Player {OwnerClientId}] {type} 중첩 증가! 총 {status.Stacks}스택");
                    return;
                }
            }

            // 2. 없다면 리스트에 새로 추가
            ActiveStatuses.Add(new StatusData { Type = type, Stacks = stacks, Duration = duration });
            Debug.Log($"[Player {OwnerClientId}] {type} {stacks}스택 새로 부여됨!");
        }

        public int GetStatusStack(StatusType type)
        {
            foreach (var status in ActiveStatuses)
            {
                if (status.Type == type) return status.Stacks;
            }
            return 0; // 없으면 0 반환
        }

        // "깨뜨림(빙결 3스택 터짐)"이나 "연성(비전 스택 모두 소모)" 같은 카드를 쓸 때 호출
        public void ConsumeAllStatus(StatusType type)
        {
            if (!IsServer) return;
            for (int i = ActiveStatuses.Count - 1; i >= 0; i--)
            {
                if (ActiveStatuses[i].Type == type)
                {
                    ActiveStatuses.RemoveAt(i);
                    break;
                }
            }
        }

        // ==========================================
        // 턴 자동 동기화 (기획서 반영)
        // ==========================================
        private void HandlePhaseEffects(GamePhase phase, bool isMyTurn)
        {
            if (!IsServer) return; 

            // 1. 내 턴이 끝날 때(End) -> 발화 데미지 입기
            if (phase == GamePhase.End && isMyTurn)
            {
                int igniteStacks = GetStatusStack(StatusType.Ignite);
                if (igniteStacks > 0)
                {
                    TakeDamage(igniteStacks); // 스택 당 데미지
                    Debug.Log($"🔥 발화 효과 발동! 데미지: {igniteStacks}");
                    
                    // TODO: 지속 턴(Duration)을 1 감소시키고 0이 되면 삭제하는 로직 추가 필요
                }
            }

            // 2. 내 턴이 시작할 때(Draw) -> 코스트(마나) 회복
            if (phase == GamePhase.Draw && isMyTurn)
            {
                //todo: 최대마나 증가 필요하다면 구현
                CurrentMana.Value = MaxMana.Value;
                Debug.Log($"[Player {OwnerClientId}] 턴 시작, 마나가 가득 찼습니다.");
                // TODO: 빙결 등으로 최대 마나가 감소했다면 여기서 적용
            }
        }
    }
}

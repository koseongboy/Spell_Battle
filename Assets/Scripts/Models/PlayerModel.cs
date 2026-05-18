using Unity.Netcode;
using UnityEngine;
using System;
using Models.TurnModel;
using Controllers.TurnController;
using Cards.CardUIDatas;
using Models.CardModels;
using Managers.LocalDataManagers;

namespace Models.PlayerModels
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
        public NetworkVariable<int> MaxHealth = new NetworkVariable<int>(30);
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(30);
        
        public NetworkVariable<int> MaxMana = new NetworkVariable<int>(1);
        public NetworkVariable<int> FinalMana = new NetworkVariable<int>(10);
        public NetworkVariable<int> CurrentMana = new NetworkVariable<int>(1);
        public NetworkVariable<int> Shield = new NetworkVariable<int>(0);
        public NetworkVariable<Property> LastProperty = new NetworkVariable<Property>(Property.None);

        // 🌟 핵심: 네트워크로 자동 동기화되는 상태이상 리스트
        // (일반 List나 Dictionary는 동기화가 안 되기 때문에 반드시 NetworkList를 써야 합니다!)
        public NetworkList<StatusData> ActiveStatuses;

        [Header("Card Modules")]
        public DeckModel Deck;
        public GraveyardModel Graveyard;
        public HandModel Hand; 

        private void Awake()
        {
            // NetworkList는 반드시 Awake에서 공간을 할당해 주어야 합니다.
            ActiveStatuses = new NetworkList<StatusData>();
        }

        public override void OnNetworkSpawn()
        {
            if(TurnController.Instance != null)
            {
                if(IsOwner)
                {
                    TurnController.Instance.MyPlayer = this;
                    Debug.Log("방장 캐릭 턴 매니져에 등록 완료");
                    // 중앙 매니저가 스폰되면, 덱 부서에게 "너 팩스 보내!" 라고 지시만 합니다.
                    // int[] myDeckArray = LocalDataManager.Instance.MyCustomDeck.ToArray();
                    // Deck.SubmitDeckServerRpc(myDeckArray);
                } 
                else
                {
                    TurnController.Instance.EnemyPlayer = this;
                    Debug.Log("상대 캐릭 턴 매니져에 등록 완료");
                }
            }
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

            
            if(Shield.Value > 0)
            {
                if(Shield.Value >= amount) Shield.Value -= amount;
                else
                {
                    int damageAfterShield = amount - Shield.Value;
                    Shield.Value = 0;
                    CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - damageAfterShield);
                }
            }
            else
            {
                CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - amount);
            }
            
            Debug.Log($"[Player {OwnerClientId}] 데미지 {amount} 피격! 남은 체력: {CurrentHealth.Value}, 남은 쉴드: {Shield.Value}");
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

        public void AddShield(int amount)
        {
            if(!IsServer) return;
            Shield.Value += amount;
        }

        // ==========================================
        // [서버 전용 권한] 상태이상(스택) 관리 시스템
        // ==========================================

        public void AddStatus(StatusType type, int stacks, int duration = 1)
        {
            if (!IsServer) return;

            // 1. 불(발화) : 무조건 별개의 인스턴스로 분리해서 추가
            if (type == StatusType.Ignite)
            {
                ActiveStatuses.Add(new StatusData { Type = type, Stacks = stacks, Duration = duration });
                Debug.Log($"[Player {OwnerClientId}] {type} 별도 부여됨! ({duration}턴)");
                return;
            }

            // 2. 얼음(빙결), 비전(응축), 공허(예언) : 하나로 합치는 로직
            for (int i = 0; i < ActiveStatuses.Count; i++)
            {
                if (ActiveStatuses[i].Type == type)
                {
                    var status = ActiveStatuses[i];
                    status.Stacks += stacks;

                    // 속성에 따른 갱신 규칙 적용
                    if (type == StatusType.Freeze)
                    {
                        // 빙결: 기획서대로 들어온 duration으로 덮어씌움 (갱신)
                        status.Duration = duration; 
                    }
                    else if (type == StatusType.ArcaneStack || type == StatusType.Prophecy)
                    {
                        // 비전/공허: 영원히 사라지지 않음 (-1을 무한대의 의미로 사용)
                        status.Duration = -1;
                    }
                    // 그 외 상태이상(Shield 등)은 가장 긴 턴 수를 유지하거나 기획에 맞게 처리
                    else 
                    {
                        status.Duration = Mathf.Max(status.Duration, duration);
                    }

                    ActiveStatuses[i] = status; 
                    Debug.Log($"[Player {OwnerClientId}] {type} 중첩 및 갱신! 총 {status.Stacks}스택");
                    return;
                }
            }

            // 기존에 없었다면 새로 추가 (비전/공허는 애초에 무한대(-1)로 세팅)
            if (type == StatusType.ArcaneStack || type == StatusType.Prophecy) duration = -1;
            ActiveStatuses.Add(new StatusData { Type = type, Stacks = stacks, Duration = duration });
            Debug.Log($"[Player {OwnerClientId}] {type} {stacks}스택 새로 부여됨!");
        }

        public int GetStatusStack(StatusType type)
        {
            int totalStacks = 0;
            // 발화처럼 여러 개로 나뉘어 있을 수 있으므로 모두 합산해서 반환합니다.
            foreach (var status in ActiveStatuses)
            {
                if (status.Type == type) totalStacks += status.Stacks;
            }
            return totalStacks;
        }

        // ==========================================
        // 턴 자동 동기화 (기획서 반영)
        // ==========================================
        private void HandlePhaseEffects(GamePhase phase, bool isMyTurn)
        {
            if (!IsServer) return; 

            // 내 턴이 끝날 때(End) 처리
            if (phase == GamePhase.End && isMyTurn)
            {
                // 1. 발화 데미지 적용 (분산된 발화 스택의 총합 데미지)
                int totalIgniteStacks = GetStatusStack(StatusType.Ignite);
                if (totalIgniteStacks > 0)
                {
                    TakeDamage(totalIgniteStacks);
                    Debug.Log($"🔥 발화 효과 발동! 데미지: {totalIgniteStacks}");
                }

                // 2. 턴 지속시간 감소 및 만료된 상태이상 제거
                // 🌟 주의: 리스트의 항목을 삭제할 때는 무조건 '뒤에서부터(역순으로)' 검사해야 버그가 안 납니다!
                for (int i = ActiveStatuses.Count - 1; i >= 0; i--)
                {
                    var status = ActiveStatuses[i];
                    
                    // Duration이 -1(영구 지속)인 예언/응축은 깎지 않음
                    if (status.Duration > 0)
                    {
                        status.Duration--; // 1턴 감소
                        
                        if (status.Duration == 0)
                        {
                            ActiveStatuses.RemoveAt(i); // 턴이 다 되면 소멸
                            Debug.Log($"[Player {OwnerClientId}] {status.Type} 효과가 종료되었습니다.");
                        }
                        else
                        {
                            ActiveStatuses[i] = status; // 깎인 턴 수 다시 저장
                        }
                    }
                }
            }

            // 내 턴이 시작할 때(Draw) -> 코스트(마나) 회복
            if (phase == GamePhase.Draw && isMyTurn)
            {
                CurrentMana.Value = MaxMana.Value;
                Debug.Log($"[Player {OwnerClientId}] 턴 시작, 마나가 가득 찼습니다.");
            }
        }
    }
}

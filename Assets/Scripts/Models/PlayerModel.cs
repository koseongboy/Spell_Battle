using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections.Generic;
using Models.TurnModel;
using Controllers.TurnControllers;
using Cards.CardUIDatas;
using Cards.EffectInfos;
using Models.CardModels;

namespace Models.PlayerModels {
    // 데미지의 출처를 명시
    public enum DamageType {
        Direct, // 일반 타격
        Ignite, // 발화로 인한 도트 데미지
        Reflect // 반사 데미지
    }

    // 기획서에 명시된 상태이상 종류들
    public enum StatusType {
        None,
        Ignite, // 발화 (불 - 도트뎀)
        Freeze, // 빙결 (얼음 - 마나 감소 및 스택 폭발)
        Prophecy, // 예언 (공허 - 공격력 강화 스택)
        ArcaneStack, // 응축 (비전 - 터뜨려서 데미지 증가)
        Shield, // 보호막


        // --- 아래부터 새롭게 추가된 수치 변조용(Modifier) 상태이상 ---
        IgniteDamageMultiplier, // 발화 데미지 배수 (분출, 확산용 / 스택=배수)
        DamageReduction, // 받는 데미지 감소 (암주, 신념용 / 스택=감소율%)
        DamageReduction_Turn, // 턴 단위 데미지 감소 (Duration = 턴 수)
        DamageReduction_Hit, // 공격 단위 데미지 감소 (Duration = 남은 횟수)
        ShieldGainBoost, // 보호막 획득량 증가 (대지모신용 / 스택=증가율%)
        StatusApplyMultiplier, // 상태이상 부여량 증가 (설옥, 무류용 / 스택=배수)
        DamageReflect // 반사 (금강, 환각용 / 스택=반사율%)
    }

    // 🌟 네트워크로 전송할 상태이상 '택배 상자' (구조체)
    public struct StatusData : INetworkSerializable, IEquatable<StatusData> {
        public StatusType Type;
        public int Stacks; // 중첩 수
        public int Duration; // 지속 턴 수
        
        // NGO가 이 데이터를 0과 1로 변환(직렬화)하는 규칙
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref Stacks);
            serializer.SerializeValue(ref Duration);
        }

        public bool Equals(StatusData other) {
            return Type == other.Type && Stacks == other.Stacks && Duration == other.Duration;
        }

        public string GetTranslateStatus() {
            switch (Type) {
                case StatusType.Ignite: return "발화";
                case StatusType.Freeze: return "빙결";
                case StatusType.Prophecy: return "예언";
                case StatusType.ArcaneStack: return "응축";
                case StatusType.Shield: return "보호막";
                
                case StatusType.IgniteDamageMultiplier: return "발화 데미지 증가";
                case StatusType.DamageReduction: return "데미지 감소";
                case StatusType.DamageReduction_Turn: return "턴 단위 데미지 감소";
                case StatusType.DamageReduction_Hit: return "공격 단위 데미지 감소";
                case StatusType.ShieldGainBoost: return "보호막 획득량 증가";
                case StatusType.StatusApplyMultiplier: return "상태이상 부여량 증가";
                case StatusType.DamageReflect: return "반사";
                
                
                default: return "(알수없음)";
            }
        }
    }
    
    
    // 덱 정보
    [System.Serializable]
    public class DeckData {
        public string id; 
        public string deckName;
        public List<int> cardIds = new List<int>();
        public string cardCountSummary; 
        public Property representativeProperty;

        public DeckData(string id, string name, List<int> ids, string summary, Property repProp) {
            this.id = string.IsNullOrEmpty(id) ? Guid.NewGuid().ToString() : id;
            this.deckName = name;
            this.cardIds = new List<int>(ids);
            this.cardCountSummary = summary;
            this.representativeProperty = repProp;
        }
    }
    
    // PlayerPrefs에 여러 덱을 한 번에 JSON으로 저장하기 위한 래퍼 클래스
    [System.Serializable]
    public class DeckStorageWrapper {
        public List<DeckData> decks = new List<DeckData>();
    }

    public class PlayerModel : NetworkBehaviour {
        [Header("Stats")] public NetworkVariable<int> MaxHealth = new NetworkVariable<int>(30);
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(30);

        public NetworkVariable<int> MaxMana = new NetworkVariable<int>(1);
        public NetworkVariable<int> FinalMana = new NetworkVariable<int>(10);
        public NetworkVariable<int> CurrentMana = new NetworkVariable<int>(1);
        public NetworkVariable<int> Shield = new NetworkVariable<int>(0);
        public NetworkVariable<Property> LastProperty = new NetworkVariable<Property>(Property.None);

        // 🌟 핵심: 네트워크로 자동 동기화되는 상태이상 리스트
        // (일반 List나 Dictionary는 동기화가 안 되기 때문에 반드시 NetworkList를 써야 합니다!)
        public NetworkList<StatusData> ActiveStatuses;

        [Header("Card Modules")] public DeckModel Deck;
        public GraveyardModel Graveyard;
        public HandModel Hand;

        private void Awake() {
            // NetworkList는 반드시 Awake에서 공간을 할당해 주어야 합니다.
            ActiveStatuses = new NetworkList<StatusData>();
        }

        public override void OnNetworkSpawn() {
            if (TurnController.Instance != null) {
                if (IsOwner) {
                    TurnController.Instance.MyPlayer = this;
                    Debug.Log("방장 캐릭 턴 매니져에 등록 완료");
                }
                else {
                    TurnController.Instance.EnemyPlayer = this;
                    Debug.Log("상대 캐릭 턴 매니져에 등록 완료");
                }
            }

            // 턴 매니저 구독: 턴이 바뀔 때 발화 데미지를 입거나 마나를 채우기 위함
            if (TurnModel.TurnModel.Instance != null) {
                TurnModel.TurnModel.Instance.OnPhaseChangedEvent += HandlePhaseEffects;
            }
        }

        public override void OnNetworkDespawn() {
            if (TurnModel.TurnModel.Instance != null) {
                TurnModel.TurnModel.Instance.OnPhaseChangedEvent -= HandlePhaseEffects;
            }
        }

        // ==========================================
        // [서버 전용 권한] 스탯 조작 함수들 (카드가 발동될 때 호출됨)
        // ==========================================

        public void TakeDamage(int amount, DamageType dmgType = DamageType.Direct) {
            if (!IsServer) return;
            int finalDamage = amount;

            // [턴 기반 데미지 감소]
            int turnReductionPct = GetStatusStack(StatusType.DamageReduction_Turn);
            if (turnReductionPct > 0) {
                finalDamage = Mathf.RoundToInt(finalDamage * (100f - turnReductionPct) / 100f);
            }

            // [횟수 기반 데미지 감소]
            int hitReductionPct = GetStatusStack(StatusType.DamageReduction_Hit);
            if (hitReductionPct > 0) {
                finalDamage = Mathf.RoundToInt(finalDamage * (100f - hitReductionPct) / 100f);

                // 데미지를 줄인 직후, 횟수(Duration) 1 차감
                ConsumeStatusCharge(StatusType.DamageReduction_Hit, 1);
            }

            // 1. 발화 데미지 증폭 체크 (분출, 확산 카드 대응)
            if (dmgType == DamageType.Ignite) {
                int igniteMultiplier = GetStatusStack(StatusType.IgniteDamageMultiplier);
                if (igniteMultiplier > 0) {
                    finalDamage *= igniteMultiplier; // 분출(스택 2)이면 데미지 2배
                }
            }

            // 2. 받는 데미지 감소 체크 (신념, 암주 대응)
            int damageReductionPct = GetStatusStack(StatusType.DamageReduction);
            if (damageReductionPct > 0) {
                // 30% 감소라면, (100 - 30) / 100 적용
                finalDamage = Mathf.RoundToInt(finalDamage * (100f - damageReductionPct) / 100f);
            }

            // 3. 반사 데미지 로직 (금강, 환각 대응 - 상대방에게 데미지 리턴)
            int reflectPct = GetStatusStack(StatusType.DamageReflect);
            if (reflectPct > 0 && dmgType == DamageType.Direct) {
                int reflectDamage = Mathf.RoundToInt(finalDamage * (reflectPct / 100f));
                // TODO: 공격자(Enemy) 타겟을 찾아 TakeDamage(reflectDamage, DamageType.Reflect) 호출 로직 필요
            }

            // --- 이후 기존과 동일한 쉴드 깎기 및 체력 차감 로직 ---
            if (Shield.Value > 0) {
                if (Shield.Value >= finalDamage) Shield.Value -= finalDamage;
                else {
                    int damageAfterShield = finalDamage - Shield.Value;
                    Shield.Value = 0;
                    CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - damageAfterShield);
                }
            }
            else {
                CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - finalDamage);
            }
        }

        public void Heal(int amount) {
            if (!IsServer) return;
            CurrentHealth.Value = Mathf.Min(MaxHealth.Value, CurrentHealth.Value + amount);
        }

        public bool TryUseMana(int amount) {
            if (!IsServer) return false;
            if (CurrentMana.Value - amount >= 0) {
                CurrentMana.Value -= amount;
                return true;
            }
            else {
                return false;
            }
        }

        public void ConsumeAllStatus(StatusType type) {
            if (!IsServer) return;

            // NetworkList 삭제 시에는 반드시 역순으로 순회해야 인덱스 오류가 발생하지 않습니다.
            for (int i = ActiveStatuses.Count - 1; i >= 0; i--) {
                if (ActiveStatuses[i].Type == type) {
                    ActiveStatuses.RemoveAt(i);
                }
            }

            Debug.Log($"[Player {OwnerClientId}] {type} 스택이 모두 소모/기폭되었습니다.");
        }


        // 특정 상태이상의 'Duration(여기서는 횟수로 취급)'을 원하는 만큼만 깎는 함수
        public void ConsumeStatusCharge(StatusType type, int chargesToConsume = 1) {
            if (!IsServer) return;

            // NetworkList 삭제/수정은 반드시 역순으로 진행
            for (int i = ActiveStatuses.Count - 1; i >= 0; i--) {
                if (ActiveStatuses[i].Type == type) {
                    var status = ActiveStatuses[i];
                    status.Duration -= chargesToConsume; // Duration을 횟수로 취급하여 차감

                    if (status.Duration <= 0) {
                        ActiveStatuses.RemoveAt(i);
                        Debug.Log($"[Player {OwnerClientId}] {type} 방어 횟수가 소모되어 파괴되었습니다.");
                    }
                    else {
                        ActiveStatuses[i] = status; // 차감된 횟수로 NetworkList 갱신
                        Debug.Log($"[Player {OwnerClientId}] {type} 방어 횟수 차감! 남은 횟수: {status.Duration}");
                    }
                }
            }
        }

        public void ManaHeal(int amount) {
            if (!IsServer) return;
            CurrentMana.Value = Mathf.Min(MaxMana.Value, CurrentMana.Value + amount);
        }

        public void IncreaseMaxMana(int amount) {
            if (!IsServer) return;
            MaxMana.Value = Mathf.Max(MaxMana.Value + amount, FinalMana.Value);
        }

        public void AddShield(int amount) {
            if (!IsServer) return;

            int finalAmount = amount;

            // 보호막 획득량 증가 체크 (대지모신 대응)
            int shieldBoostPct = GetStatusStack(StatusType.ShieldGainBoost);

            // 지핵(체력 10 이하일 때 50% 증가) 조건부 하드코딩
            if (CurrentHealth.Value <= 10) shieldBoostPct += 50;

            if (shieldBoostPct > 0) {
                finalAmount = Mathf.RoundToInt(finalAmount * (1f + (shieldBoostPct / 100f)));
            }

            Shield.Value += finalAmount;
        }

        // ==========================================
        // [서버 전용 권한] 상태이상(스택) 관리 시스템
        // ==========================================

        public void AddStatus(StatusType type, int stacks, int duration = 1) {
            if (!IsServer) return;

            // 1. 불(발화) : 무조건 별개의 인스턴스로 분리해서 추가
            if (type == StatusType.Ignite) {
                ActiveStatuses.Add(new StatusData { Type = type, Stacks = stacks, Duration = duration });
                Debug.Log($"[Player {OwnerClientId}] {type} 별도 부여됨! ({duration}턴)");
                return;
            }

            // 2. 얼음(빙결), 비전(응축), 공허(예언) : 하나로 합치는 로직
            for (int i = 0; i < ActiveStatuses.Count; i++) {
                if (ActiveStatuses[i].Type == type) {
                    var status = ActiveStatuses[i];
                    status.Stacks += stacks;

                    // 속성에 따른 갱신 규칙 적용
                    if (type == StatusType.Freeze) {
                        // 빙결: 기획서대로 들어온 duration으로 덮어씌움 (갱신)
                        status.Duration = duration;
                    }
                    else if (type == StatusType.ArcaneStack || type == StatusType.Prophecy) {
                        // 비전/공허: 영원히 사라지지 않음 (-1을 무한대의 의미로 사용)
                        status.Duration = -1;
                    }
                    // 그 외 상태이상(Shield 등)은 가장 긴 턴 수를 유지하거나 기획에 맞게 처리
                    else {
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

        public int GetStatusStack(StatusType type) {
            int totalStacks = 0;
            // 발화처럼 여러 개로 나뉘어 있을 수 있으므로 모두 합산해서 반환합니다.
            foreach (var status in ActiveStatuses) {
                if (status.Type == type) totalStacks += status.Stacks;
            }

            return totalStacks;
        }

        // ==========================================
        // [서버 전용 권한] 카드 이동 메인 로직
        // ==========================================
        public void ProcessCardMovement(EffectType moveType, int count, string specificCardId = "") {
            if (!IsServer) return;

            switch (moveType) {
                case EffectType.DrawCard:
                    // 1. 카드를 count만큼 뽑음 (HandModel 내부에서 주인에게만 자동 RPC 전송됨)
                    for (int i = 0; i < count; i++) {
                        Deck.DrawCard();
                    }

                    // 2. 상대방 클라이언트에게는 "N장 뽑았다"는 사실만 전송하여 뒷면 애니메이션 재생
                    ulong enemyId = GetEnemyClientId();
                    PlayEnemyDrawAnimationClientRpc(count, RpcTarget.Single(enemyId, RpcTargetUse.Temp));
                    break;

                case EffectType.ShuffleSpecificCardToDeck:
                    // string으로 들어온 특정 카드 ID를 int로 파싱 (기획상 ID가 int이므로)
                    if (int.TryParse(specificCardId, out int parsedCardId)) {
                        for (int i = 0; i < count; i++) {
                            // shuffleAfter를 false로 두고 다 넣은 뒤 마지막에 한 번만 셔플
                            Deck.InsertCard(parsedCardId, shuffleAfter: false);
                        }

                        Deck.Shuffle();
                    }

                    break;

                case EffectType.DiscardRandom:
                    // HandModel에 무작위로 카드를 버리는 함수 호출
                    for (int i = 0; i < count; i++) {
                        int discardedId = Hand.DiscardRandomCardFromServer();
                        if (discardedId != -1) {
                            // 무덤 모델이 있다면 여기에 추가: Graveyard.AddCard(discardedId);

                            // 버려진 카드는 모두가 알아야 하므로 전체 클라이언트에게 알림
                            NotifyCardDiscardedClientRpc(discardedId);
                        }
                    }

                    break;
            }
        }

        // ==========================================
        // 턴 자동 동기화 (기획서 반영)
        // ==========================================
        private void HandlePhaseEffects(GamePhase phase, bool isMyTurn) {
            if (!IsServer) return;

            // 내 턴이 끝날 때(End) 처리
            if (phase == GamePhase.End && isMyTurn) {
                if (!IsServer) return;

                // 1. 발화 데미지 적용
                int totalIgniteStacks = GetStatusStack(StatusType.Ignite);
                if (totalIgniteStacks > 0) {
                    // 팩트 체크: 여기서 DamageType.Ignite로 명시해서 보냅니다.
                    TakeDamage(totalIgniteStacks, DamageType.Ignite);
                    Debug.Log($"🔥 발화 효과 발동! 기본 데미지: {totalIgniteStacks}");
                }

                // 2. 턴 지속시간 감소 및 만료된 상태이상 제거
                // 🌟 주의: 리스트의 항목을 삭제할 때는 무조건 '뒤에서부터(역순으로)' 검사해야 버그가 안 납니다!
                for (int i = ActiveStatuses.Count - 1; i >= 0; i--) {
                    var status = ActiveStatuses[i];

                    // 횟수제(Hit) 기반 상태이상은 턴 종료 시점에 턴을 깎지 않고 건너뜀
                    if (status.Type == StatusType.DamageReduction_Hit) continue;

                    // Duration이 -1(영구 지속)인 예언/응축 스택은 깎지 않음
                    if (status.Duration > 0) {
                        status.Duration--; // 1턴 감소
                        if (status.Duration == 0) {
                            ActiveStatuses.RemoveAt(i); // 턴이 다 되면 소멸
                        }
                        else {
                            ActiveStatuses[i] = status; // 깎인 턴 수 다시 저장
                        }
                    }
                }
            }

            // 내 턴이 시작할 때(Draw) -> 코스트(마나) 회복
            if (phase == GamePhase.Draw && isMyTurn) {
                CurrentMana.Value = MaxMana.Value;
                Debug.Log($"[Player {OwnerClientId}] 턴 시작, 마나가 가득 찼습니다.");
            }
        }


        // ==========================================
        // [클라이언트 연출용 RPC] 서버가 클라이언트에게 지시
        // ==========================================

        [Rpc(SendTo.SpecifiedInParams)]
        private void PlayEnemyDrawAnimationClientRpc(int count, RpcParams rpcParams = default) {
            Debug.Log($"[Client] 상대방이 카드 {count}장을 뽑았습니다. (뒷면 애니메이션 재생)");
            // TODO: 상대방 덱 -> 상대방 손패 위치로 카드 뒷면 프리팹 날아가는 UI 연출
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyCardDiscardedClientRpc(int discardedCardId) {
            Debug.Log($"[Client] {discardedCardId}번 카드가 버려졌습니다.");
            // TODO: 손패(또는 덱)에서 카드가 무덤으로 날아가는 UI 연출
        }

        private ulong GetEnemyClientId() {
            return OwnerClientId == 0 ? 1ul : 0ul;
        }
    }
}
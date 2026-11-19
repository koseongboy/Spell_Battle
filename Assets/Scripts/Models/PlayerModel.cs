using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections.Generic;
using Models.TurnModel;
using Controllers.SpellControllers;
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

    // 상태이상 종류
    public enum StatusType {
        None,
        Ignite, // 발화 (불 - 도트뎀)
        Freeze, // 빙결 (얼음 - 마나 감소 및 스택 폭발)
        Prophecy, // 예언 (공허 - 공격력 강화 스택)
        ArcaneStack, // 응축 (비전 - 터뜨려서 데미지 증가)
        Shield, // 보호막

        // --- 수치 보정(Modifier)용 상태이상: Stacks를 배수/비율 값으로 사용 ---
        IgniteDamageMultiplier, // 발화 데미지 배수 (분출, 확산용 / 스택=배수)
        DamageReduction, // 받는 데미지 감소 (암주, 신념용 / 스택=감소율%)
        DamageReduction_Turn, // 턴 단위 데미지 감소 (Duration = 턴 수)
        DamageReduction_Hit, // 공격 단위 데미지 감소 (Duration = 남은 횟수)
        ShieldGainBoost, // 보호막 획득량 증가 (대지모신용 / 스택=증가율%)
        StatusApplyMultiplier, // 상태이상 부여량 증가 (설옥, 무류용 / 스택=배수)
        DamageReflect // 반사 (금강, 환각용 / 스택=반사율%)
    }

    // NetworkList로 동기화하기 위한 상태이상 데이터 (INetworkSerializable + IEquatable 필요)
    public struct StatusData : INetworkSerializable, IEquatable<StatusData> {
        public StatusType Type;
        public int Stacks; // 중첩 수 (Modifier 계열은 배수/비율)
        public int Duration; // 남은 턴 수. -1은 영구 지속, *_Hit 계열은 남은 횟수

        // NGO 네트워크 직렬화 규칙
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Type);
            serializer.SerializeValue(ref Stacks);
            serializer.SerializeValue(ref Duration);
        }

        public bool Equals(StatusData other) {
            return Type == other.Type && Stacks == other.Stacks && Duration == other.Duration;
        }


    }

    /// <summary>
    /// 플레이어 1명의 전투 상태(체력·마나·보호막·상태이상)와 카드 모듈(덱·손패·무덤)을 보유한다.
    /// 모든 수치는 NetworkVariable/NetworkList로 동기화되며, 변경 함수는 서버에서만 동작한다.
    /// </summary>
    public class PlayerModel : NetworkBehaviour
    {
        public static event Action<PlayerModel> OnPlayerSpawned;

        [Header("Default Settings (Inspector)")]
        [SerializeField] private int defaultMaxHealth = 30;
        [SerializeField] private int defaultMaxMana = 4;

        [Header("Stats")]
        public NetworkVariable<int> MaxHealth = new NetworkVariable<int>(30);
        public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(30);

        public NetworkVariable<int> MaxMana = new NetworkVariable<int>(4);
        public NetworkVariable<int> FinalMana = new NetworkVariable<int>(20); // 최대 마나 성장 상한
        public NetworkVariable<int> CurrentMana = new NetworkVariable<int>(4);
        public NetworkVariable<int> Shield = new NetworkVariable<int>(0);
        public NetworkVariable<Property> LastProperty = new NetworkVariable<Property>(Property.None); // 직전 주문의 메인 속성


        // 적용 중인 상태이상 목록 (컬렉션 동기화를 위해 NetworkList 사용)
        public NetworkList<StatusData> ActiveStatuses;

        [Header("Card Modules")] public DeckModel Deck;
        public GraveyardModel Graveyard;
        public HandModel Hand;

        // 카드 선택 중 사용 예정 마나 (로컬 UI 미리보기 전용, 동기화하지 않음)
        private int _expectedManaCost = 0;
        public int ExpectedManaCost
        {
            get { return _expectedManaCost; }
            set 
            {
                if (_expectedManaCost != value) 
                {
                    _expectedManaCost = value;
                    OnExpectedManaChanged?.Invoke(_expectedManaCost);
                }
            }
        }
        public event Action<int> OnExpectedManaChanged;

        private void Awake() {
            // NetworkList는 NetworkBehaviour 초기화 전에 생성되어야 하므로 Awake에서 할당
            ActiveStatuses = new NetworkList<StatusData>();
        }
        public static event Action<PlayerModel> OnPlayerDespawned;
        


        public override void OnNetworkSpawn() {
            if (IsServer) {
                MaxHealth.Value = defaultMaxHealth;
                CurrentHealth.Value = defaultMaxHealth;
                
                MaxMana.Value = defaultMaxMana;
                CurrentMana.Value = defaultMaxMana;
            }
            OnPlayerSpawned?.Invoke(this);

            Debug.Log($"[PlayerModel] {gameObject.name} 스폰 완료, 이벤트 방송 송출!");
            // SpellController가 이미 존재해 이벤트 구독 전 스폰된 경우를 대비한 직접 등록
            if (SpellController.Instance != null) {
                if (IsOwner) {
                    SpellController.Instance.MyPlayer = this;
                    Debug.Log("방장 캐릭 턴 매니져에 등록 완료");
                }
                else {
                    SpellController.Instance.EnemyPlayer = this;
                    Debug.Log("상대 캐릭 턴 매니져에 등록 완료");
                }
            }
        }

        public override void OnNetworkDespawn() {
            OnPlayerDespawned?.Invoke(this);
            base.OnNetworkDespawn();
        }

        // ==========================================
        // [서버 전용] 스탯 조작 (주로 EffectCommand에서 호출)
        // ==========================================

        /// <summary>
        /// 피해 적용. 감소/증폭 보정 → 반사 → 보호막 → 체력 순으로 처리한다.
        /// </summary>
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

                // 피격 1회마다 남은 횟수(Duration) 1 차감
                ConsumeStatusCharge(StatusType.DamageReduction_Hit, 1);
            }

            // [발화 피해 증폭] 분출, 확산 (스택 = 배수)
            if (dmgType == DamageType.Ignite) {
                int igniteMultiplier = GetStatusStack(StatusType.IgniteDamageMultiplier);
                if (igniteMultiplier > 0) {
                    finalDamage *= igniteMultiplier;
                }
            }

            // [받는 피해 감소] 신념, 암주 (스택 = 감소율 %)
            int damageReductionPct = GetStatusStack(StatusType.DamageReduction);
            if (damageReductionPct > 0) {
                finalDamage = Mathf.RoundToInt(finalDamage * (100f - damageReductionPct) / 100f);
            }

            // [피해 반사] 금강, 환각 (스택 = 반사율 %). 반사 피해끼리 무한 반사되지 않도록 Direct만 대상
            int reflectPct = GetStatusStack(StatusType.DamageReflect);
            if (reflectPct > 0 && dmgType == DamageType.Direct) {
                int reflectDamage = Mathf.RoundToInt(finalDamage * (reflectPct / 100f));
                // TODO: 공격자에게 TakeDamage(reflectDamage, DamageType.Reflect) 적용
            }

            // 보호막이 먼저 피해를 흡수하고, 초과분만 체력에서 차감
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

        // 마나가 충분할 때만 차감하고 성공 여부를 반환 (주문 제출 시 서버 측 검증에 사용)
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

            // 순회 중 삭제하므로 인덱스가 밀리지 않도록 역순 순회
            for (int i = ActiveStatuses.Count - 1; i >= 0; i--) {
                if (ActiveStatuses[i].Type == type) {
                    ActiveStatuses.RemoveAt(i);
                }
            }

            Debug.Log($"[Player {OwnerClientId}] {type} 스택이 모두 소모/기폭되었습니다.");
        }


        // 횟수제 상태이상(*_Hit)의 남은 횟수(Duration)를 차감하고, 0 이하가 되면 제거
        public void ConsumeStatusCharge(StatusType type, int chargesToConsume = 1) {
            if (!IsServer) return;

            for (int i = ActiveStatuses.Count - 1; i >= 0; i--) {
                if (ActiveStatuses[i].Type == type) {
                    var status = ActiveStatuses[i];
                    status.Duration -= chargesToConsume;

                    if (status.Duration <= 0) {
                        ActiveStatuses.RemoveAt(i);
                        Debug.Log($"[Player {OwnerClientId}] {type} 방어 횟수가 소모되어 파괴되었습니다.");
                    }
                    else {
                        ActiveStatuses[i] = status; // struct 복사본이므로 다시 대입해야 동기화됨
                        Debug.Log($"[Player {OwnerClientId}] {type} 방어 횟수 차감! 남은 횟수: {status.Duration}");
                    }
                }
            }
        }

        public void ManaHeal(int amount) {
            if (!IsServer) return;
            CurrentMana.Value = Mathf.Min(MaxMana.Value, CurrentMana.Value + amount);
        }

        // 최대 마나 성장 (FinalMana를 넘지 않음)
        public void IncreaseMaxMana(int amount) {
            if (!IsServer) return;
            MaxMana.Value = Mathf.Min(MaxMana.Value + amount, FinalMana.Value);
        }

        public void AddShield(int amount) {
            if (!IsServer) return;

            int finalAmount = amount;

            // 보호막 획득량 증가율(%) = 상태이상 보정 + 조건부 보정
            int shieldBoostPct = GetStatusStack(StatusType.ShieldGainBoost);

            // 지핵: 체력 10 이하일 때 +50%
            if (CurrentHealth.Value <= 10) shieldBoostPct += 50;

            if (shieldBoostPct > 0) {
                finalAmount = Mathf.RoundToInt(finalAmount * (1f + (shieldBoostPct / 100f)));
            }

            Shield.Value += finalAmount;
        }

        // ==========================================
        // [서버 전용] 상태이상(스택) 관리
        // ==========================================

        /// <summary>
        /// 상태이상 부여. 속성별로 중첩 규칙이 다르다.
        /// - 발화: 부여할 때마다 별도 인스턴스로 추가 (각자 지속시간이 따로 흐름)
        /// - 빙결: 스택 합산 + 지속시간 갱신
        /// - 응축/예언: 스택 합산 + 영구 지속(-1)
        /// - 그 외: 스택 합산 + 더 긴 지속시간 유지
        /// </summary>
        public void AddStatus(StatusType type, int stacks, int duration = 1) {
            if (!IsServer) return;

            if (type == StatusType.Ignite) {
                ActiveStatuses.Add(new StatusData { Type = type, Stacks = stacks, Duration = duration });
                Debug.Log($"[Player {OwnerClientId}] {type} 별도 부여됨! ({duration}턴)");
                return;
            }

            // 이미 같은 상태이상이 있으면 하나로 합산
            for (int i = 0; i < ActiveStatuses.Count; i++) {
                if (ActiveStatuses[i].Type == type) {
                    var status = ActiveStatuses[i];
                    status.Stacks += stacks;

                    if (type == StatusType.Freeze) {
                        status.Duration = duration;
                    }
                    else if (type == StatusType.ArcaneStack || type == StatusType.Prophecy) {
                        status.Duration = -1;
                    }
                    else {
                        status.Duration = Mathf.Max(status.Duration, duration);
                    }

                    ActiveStatuses[i] = status;
                    Debug.Log($"[Player {OwnerClientId}] {type} 중첩 및 갱신! 총 {status.Stacks}스택");
                    return;
                }
            }

            // 처음 부여되는 경우 새로 추가
            if (type == StatusType.ArcaneStack || type == StatusType.Prophecy) duration = -1;
            ActiveStatuses.Add(new StatusData { Type = type, Stacks = stacks, Duration = duration });
            Debug.Log($"[Player {OwnerClientId}] {type} {stacks}스택 새로 부여됨!");
        }

        public int GetStatusStack(StatusType type) {
            int totalStacks = 0;
            // 발화처럼 여러 인스턴스로 나뉜 경우도 있으므로 전체 합산
            foreach (var status in ActiveStatuses) {
                if (status.Type == type) totalStacks += status.Stacks;
            }

            return totalStacks;
        }
        
        // 같은 타입의 모든 상태이상 지속시간 연장 (영구 지속(-1)은 제외)
        public void ExtendStatusDuration(StatusType type, int amountToExtend) {
            if (!IsServer) return;

            for (int i = 0; i < ActiveStatuses.Count; i++) {
                if (ActiveStatuses[i].Type == type) {
                    var status = ActiveStatuses[i];

                    if (status.Duration > 0) {
                        status.Duration += amountToExtend;
                        ActiveStatuses[i] = status;
                        Debug.Log($"[Player {OwnerClientId}] {type}의 지속 턴 수가 {amountToExtend}만큼 증가했습니다.");
                    }
                }
            }
        }
        
        // 스택을 소모하지 않고 효과만 1회 발동 (발화 피해 1회, 빙결 폭발 등)
        public void TriggerStatusEffect(StatusType type) {
            if (!IsServer) return;

            int totalStacks = GetStatusStack(type);
            if (totalStacks <= 0) return;

            switch (type) {
                case StatusType.Ignite:
                    TakeDamage(totalStacks, DamageType.Ignite);
                    Debug.Log($"{type} 강제 1회 발동! 데미지: {totalStacks}");
                    break;
                case StatusType.Freeze:
                    // 빙결 폭발: 스택 × 2 피해
                    int freezeDamage = totalStacks * 2; 
                    TakeDamage(freezeDamage, DamageType.Direct);
                    Debug.Log($"❄️ {type} 강제 폭발! 데미지: {freezeDamage}");
                    break;
            }
        }
        
        // 현재 적용 중인 해당 상태이상의 스택을 배수로 증폭
        public void MultiplyStatusStack(StatusType type, int multiplier) {
            if (!IsServer) return;

            for (int i = 0; i < ActiveStatuses.Count; i++) {
                if (ActiveStatuses[i].Type == type) {
                    var status = ActiveStatuses[i];
                    status.Stacks *= multiplier;
                    ActiveStatuses[i] = status;
                    Debug.Log($"[Player {OwnerClientId}] {type}의 스택이 {multiplier}배로 증가했습니다! (현재: {status.Stacks})");
                }
            }
        }

        // ==========================================
        // [서버 전용] 카드 이동 (CardMovementCommand에서 호출)
        // ==========================================
        public void ProcessCardMovement(EffectType moveType, int count, string specificCardId = "") {
            if (!IsServer) return;

            switch (moveType) {
                case EffectType.DrawCard:
                    // 카드 내용은 HandModel이 주인에게만 전송
                    for (int i = 0; i < count; i++) {
                        Deck.DrawCard();
                    }

                    // 상대에게는 장수만 알려 카드 정보 노출 없이 뒷면 연출만 재생
                    ulong enemyId = GetEnemyClientId();
                    PlayEnemyDrawAnimationClientRpc(count, RpcTarget.Single(enemyId, RpcTargetUse.Temp));
                    break;

                case EffectType.ShuffleSpecificCardToDeck:
                    // 카드 데이터의 ID는 문자열로 들어오므로 int로 변환
                    if (int.TryParse(specificCardId, out int parsedCardId)) {
                        // 매번 섞지 않고 모두 넣은 뒤 한 번만 셔플
                        for (int i = 0; i < count; i++) {
                            Deck.InsertCard(parsedCardId, shuffleAfter: false);
                        }

                        Deck.Shuffle();
                    }

                    break;

                case EffectType.DiscardRandom:
                    for (int i = 0; i < count; i++) {
                        int discardedId = Hand.DiscardRandomCardFromServer();
                        if (discardedId != -1) {
                            // TODO: Graveyard.AddCardToGraveyard(discardedId) 연동

                            // 버려진 카드는 공개 정보이므로 양측에 알림
                            NotifyCardDiscardedClientRpc(discardedId);
                        }
                    }

                    break;
            }
        }

        // ==========================================
        // [서버 전용] 페이즈 진입 시 상태 처리 (PhaseManager가 페이즈 변경마다 호출)
        // ==========================================
        public void HandlePhaseEffects(GamePhase phase, bool isMyTurn) {
            if (!IsServer) return;

            // 내 턴 종료(End): 발화 피해 → 상태이상 지속시간 감소
            if (phase == GamePhase.End && isMyTurn) {
                if (!IsServer) return;

                int totalIgniteStacks = GetStatusStack(StatusType.Ignite);
                if (totalIgniteStacks > 0) {
                    BroadcastPhaseVFX(EffectCommands.VFXType.AddStatus, StatusType.Ignite);
                    // Ignite 타입으로 전달해야 발화 증폭(IgniteDamageMultiplier)이 적용됨
                    TakeDamage(totalIgniteStacks, DamageType.Ignite);
                    Debug.Log($"발화 효과 발동! 기본 데미지: {totalIgniteStacks}");
                }

                // 지속시간 1턴 감소, 만료 시 제거 (삭제가 있으므로 역순 순회)
                for (int i = ActiveStatuses.Count - 1; i >= 0; i--) {
                    var status = ActiveStatuses[i];

                    // 횟수제는 피격 시에만 차감
                    if (status.Type == StatusType.DamageReduction_Hit) continue;

                    // 영구 지속(-1)은 감소 대상 아님
                    if (status.Duration > 0) {
                        status.Duration--;
                        if (status.Duration == 0) {
                            BroadcastPhaseVFX(EffectCommands.VFXType.DetonateStatus, status.Type);
                            ActiveStatuses.RemoveAt(i);
                        }
                        else {
                            ActiveStatuses[i] = status;
                        }
                    }
                }
            }

            // 내 턴 시작(Draw): 마나 전량 회복
            if (phase == GamePhase.Draw && isMyTurn) {
                CurrentMana.Value = MaxMana.Value;
                Debug.Log($"[Player {OwnerClientId}] 턴 시작, 마나가 가득 찼습니다.");
            }
        }


        // ==========================================
        // [서버 → 클라이언트] 연출용 RPC
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

        // 커맨드 외부(턴 종료 처리 등)에서 발생한 VFX를 호스트·게스트 양쪽에 재생
        private void BroadcastPhaseVFX(EffectCommands.VFXType vfxType, StatusType statusType = StatusType.None)
        {
            // 게스트: RPC (SendTo.NotServer)
            SpellController.Instance.PlayVisualEffectClientRpc(vfxType, statusType, this.NetworkObjectId);

            // 호스트: 로컬에서 직접 재생 (턴 처리를 막지 않도록 대기하지 않음)
            if (Managers.VFX.BattleVFXManager.Instance != null) {
                StartCoroutine(Managers.VFX.BattleVFXManager.Instance.PlayVFXRoutine(vfxType, statusType, this));
            }
        }
        
        
        #region DEV

        [ContextMenu("TEST: 데미지 5 받기")]
        public void TestTakeDamage()
        {
            if (!Application.isPlaying) { Debug.LogWarning("플레이 모드에서 실행해주세요."); return; }
            if (!IsServer) { Debug.LogWarning("서버(호스트) 권한이 필요합니다."); return; }
    
            TakeDamage(5); 
            Debug.Log("[Test] 데미지 5 적용 완료. 체력 UI가 깎였는지 확인하세요.");
        }

        [ContextMenu("TEST: 체력 10 회복")]
        public void TestHeal()
        {
            if (!Application.isPlaying || !IsServer) return;
    
            Heal(10);
        }

        [ContextMenu("TEST: 마나 2 사용")]
        public void TestUseMana()
        {
            if (!Application.isPlaying || !IsServer) return;

            if(TryUseMana(2))
                Debug.Log("[Test] 마나 2 사용 완료. 마나 UI 색상이 변했는지 확인하세요.");
            else
                Debug.LogWarning("[Test] 마나가 부족하여 사용할 수 없습니다.");
        }

        [ContextMenu("TEST: 마나 전체 회복")]
        public void TestRestoreMana()
        {
            if (!Application.isPlaying || !IsServer) return;
    
            ManaHeal(MaxMana.Value); // MaxMana만큼 회복
        }

        [ContextMenu("TEST: 발화 2스택 (3턴) 추가")]
        public void TestAddIgnite()
        {
            if (!Application.isPlaying || !IsServer) return;
    
            AddStatus(StatusType.Ignite, 2, 3);
            Debug.Log("[Test] 발화 상태이상 부여 완료. UI에 아이콘이 생성되었는지 확인하세요.");
        }

        [ContextMenu("TEST: 빙결 1스택 (2턴) 추가")]
        public void TestAddFreeze()
        {
            if (!Application.isPlaying || !IsServer) return;
    
            AddStatus(StatusType.Freeze, 1, 2);
        }

        [ContextMenu("TEST: 보호막 5 획득")]
        public void TestAddShield()
        {
            if (!Application.isPlaying || !IsServer) return;
    
            AddShield(5);
        }

        #endregion
    }
}
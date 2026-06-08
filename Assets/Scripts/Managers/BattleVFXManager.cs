using UnityEngine;
using Models.PlayerModels; // StatusType 등 사용을 위해 포함

namespace Managers.VFX
{
    public class BattleVFXManager : MonoBehaviour
    {
        public static BattleVFXManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // ==========================================
        // ⚔️ 1. 전투 액션 (Damage & Heal)
        // ==========================================
        [Header("Combat Actions (Damage & Heal)")]
        [Tooltip("기본 데미지 (DamageCommand)")]
        [SerializeField] private GameObject defaultDamageVFX;
        
        [Tooltip("체력 회복 (HealCommand)")]
        [SerializeField] private GameObject healVFX;
        
        [Tooltip("보호막 생성 (ShieldCommand)")]
        [SerializeField] private GameObject shieldVFX;


        // ==========================================
        // 🌀 2. 상태 이상 제어 (Status Effects)
        // ==========================================
        

        [System.Serializable]
        public struct StatusVFXMapping {
            public StatusType statusType;
            public GameObject vfxPrefab;
        }
         [Tooltip("상태이상 부여")]
        [SerializeField] private StatusVFXMapping[] specificStatusVFXs;
        


        // ==========================================
        // 🃏 3. 유틸리티 및 카드 이동 (Utility & Movement)
        // ==========================================
        [Header("Utility & Card Movement")]
        [Tooltip("카드 이동 (CardMovementCommand) - 드로우, 버리기 등 카드가 날아가는 궤적")]
        [SerializeField] private GameObject cardMovementTrailVFX;
        
        [Tooltip("핸드 속 카드의 코스트 업")]
        [SerializeField] private GameObject costUpVFX;
        [Tooltip("핸드 속 카드의 코스트 다운")]
        [SerializeField] private GameObject costDownVFX;


        // ==========================================
        // 🔵 4. 마나 및 시스템 제어 (Mana & System)
        // ==========================================
        [Header("Mana & System")]
        [Tooltip("마나 회복 (ManaCommand - 양수)")]
        [SerializeField] private GameObject manaGainVFX;
        
        [Tooltip("마나 감소/소모 (ManaCommand - 음수)")]
        [SerializeField] private GameObject manaLossVFX;
        
        [Tooltip("시스템 특수 제어 (SystemControlCommand) - 턴 강제 종료, 대격변 등 화면 전체 연출")]
        [SerializeField] private GameObject systemControlVFX;
        
        
        // ==========================================
        // 🎥 5. 카메라 쉐이크 및 글로벌 연출 (Camera & Global)
        // ==========================================
        [Header("Camera & Global Feedback")]
        [Tooltip("강한 타격 시 화면 흔들림 효과를 줄 파티클이나 후처리 객체")]
        [SerializeField] private GameObject heavyImpactScreenVFX;
    }
}
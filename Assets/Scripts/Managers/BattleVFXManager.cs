using UnityEngine;
using Models.PlayerModels;
using System.Collections;
using System.Runtime.CompilerServices;
using Cards.EffectInfos; // StatusType 등 사용을 위해 포함

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

        private GameObject GetSpecificStatusVFX(StatusType targetStatus)
        {
            if (specificStatusVFXs == null) return defaultDamageVFX; // 방어 코드

            foreach (var mapping in specificStatusVFXs)
            {
                if (mapping.statusType == targetStatus && mapping.vfxPrefab != null)
                {
                    return mapping.vfxPrefab; // 매핑된 전용 이펙트 반환!
                }
            }
            // 매핑을 못 찾았다면(아직 프리팹 안 넣은 경우 등) 기본 상태이상 이펙트를 띄웁니다.
            return defaultDamageVFX; 
        }
        //todo 카드 무브먼트 따라 ui상 움직임 보여줘야 할 듯
        private GameObject GetSpecificCardMovementVFX(EffectType movement)
        {
            return null;
        }

        public IEnumerator PlayVFXRoutine(Models.EffectCommands.VFXType vfxType, StatusType statusType, PlayerModel target, EffectType cardMovement = EffectType.None)
        {
            // 명찰이 None이거나 타겟이 없으면 그냥 넘어갑니다.
            if (vfxType == Models.EffectCommands.VFXType.None || target == null) yield break;

            GameObject prefabToPlay = null;

            // 🌟 넘어온 명찰(enum)에 맞는 프리팹을 인스펙터 필드에서 꺼냅니다.
            switch (vfxType)
            {
                case Models.EffectCommands.VFXType.Damage: prefabToPlay = defaultDamageVFX; break;
                case Models.EffectCommands.VFXType.Heal: prefabToPlay = healVFX; break;
                case Models.EffectCommands.VFXType.Shield: prefabToPlay = shieldVFX; break;
                case Models.EffectCommands.VFXType.AddStatus: 
                    prefabToPlay = GetSpecificStatusVFX(statusType);
                    break;
                case Models.EffectCommands.VFXType.ManaGain: prefabToPlay = manaGainVFX; break;
                case Models.EffectCommands.VFXType.ManaLoss: prefabToPlay = manaLossVFX; break;
                case Models.EffectCommands.VFXType.SystemControl: prefabToPlay = systemControlVFX; break;
                case Models.EffectCommands.VFXType.CostUp: prefabToPlay = costUpVFX; break;
                case Models.EffectCommands.VFXType.CostDown: prefabToPlay = costDownVFX; break;
                default: prefabToPlay = defaultDamageVFX; break;
            }

            if (prefabToPlay == null) yield break;

            // 카메라 줌인
            Cameras.BattleCameraController.Instance.FocusOnTarget(target.transform);
            yield return new WaitForSeconds(0.5f);

            Transform mountPoint = target.transform;

            GameObject vfxInstance = Instantiate(prefabToPlay, mountPoint.position, mountPoint.rotation);
            vfxInstance.transform.SetParent(mountPoint, false);
            vfxInstance.transform.localPosition = new Vector3(0f, 0f, 0f);
            vfxInstance.transform.localRotation = Quaternion.identity;
            // 1.5초 대기 후 파괴
            yield return new WaitForSeconds(1.5f);
            if (vfxInstance != null) Destroy(vfxInstance);
            // 카메라 원위치
            Cameras.BattleCameraController.Instance.ResetCamera();
            yield return new WaitForSeconds(0.2f);
        }
    }
}
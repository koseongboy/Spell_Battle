using System;
using Models.PlayerModels;
using UnityEngine;

namespace DefaultNamespace.Utilities
{
    
    // 인스펙터에서 상태이상 종류별로 이미지를 매핑하기 위한 구조체
    [Serializable]
    public struct StatusIconMapping {
        public StatusType Type;
        public Sprite IconSprite;
    }
    
    public static class StatusDataUtility
    {
        
        // #################### LEGACY ####################
        
        // public static string GetStatusName(StatusType statusType) {
        //     switch (statusType) {
        //         case StatusType.Ignite: return "발화";
        //         case StatusType.Freeze: return "빙결";
        //         case StatusType.Prophecy: return "예언";
        //         case StatusType.ArcaneStack: return "응축";
        //         case StatusType.Shield: return "보호막";
        //         
        //         case StatusType.IgniteDamageMultiplier: return "발화 데미지 증가";
        //         case StatusType.DamageReduction: return "데미지 감소";
        //         case StatusType.DamageReduction_Turn: return "턴 단위 데미지 감소";
        //         case StatusType.DamageReduction_Hit: return "공격 단위 데미지 감소";
        //         case StatusType.ShieldGainBoost: return "보호막 획득량 증가";
        //         case StatusType.StatusApplyMultiplier: return "상태이상 부여량 증가";
        //         case StatusType.DamageReflect: return "반사";
        //         
        //         default: return "(알수없음)";
        //     }
        // } 
        //
        // public static Sprite GetStatusIcon(StatusType statusType) {
        //     // TODO : 상태이상 이미지 뱉어주기
        //     return null;
        // }
        //
        // public static string GetStatusDesc(StatusType statusType) {
        //     return String.Empty;
        // }
    }
}

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
    
    public class StatusDataUtility : MonoBehaviour
    {
        // TODO : 상태이상 이미지 관리
    }
}

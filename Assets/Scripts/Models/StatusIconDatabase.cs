using UnityEngine;
using System.Collections.Generic;
using Models.PlayerModels; // StatusType이 있는 네임스페이스

namespace DefaultNamespace
{
    [CreateAssetMenu(fileName = "StatusIconDatabase", menuName = "TCG/Status Icon Database")]
    public class StatusIconDatabase : ScriptableObject
    {
        [System.Serializable]
        public struct StatusIconMapping
        {
            public StatusType Type;
            public Sprite IconSprite;
        }

        [Header("상태이상 아이콘 매핑")]
        public List<StatusIconMapping> Mappings;

        // 캐싱용 Dictionary
        private Dictionary<StatusType, Sprite> _iconDict;

        // UI에서 아이콘을 요청할 때 사용하는 함수
        public Sprite GetIcon(StatusType type)
        {
            // Dictionary가 비어있다면 최초 1회 세팅
            if (_iconDict == null)
            {
                _iconDict = new Dictionary<StatusType, Sprite>();
                foreach (var mapping in Mappings)
                {
                    if (!_iconDict.ContainsKey(mapping.Type))
                    {
                        _iconDict.Add(mapping.Type, mapping.IconSprite);
                    }
                }
            }

            return _iconDict.ContainsKey(type) ? _iconDict[type] : null;
        }
    }
}
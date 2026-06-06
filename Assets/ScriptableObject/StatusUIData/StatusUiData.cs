using Models.PlayerModels;
using UnityEngine;

namespace DefaultNamespace
{
    
    [CreateAssetMenu(fileName = "StatusUiData", menuName = "StatusData")]
    public class StatusUiData : ScriptableObject {
        public string Name;
        public StatusType Type;
        public Sprite Icon;
        public string Desc;
    }
}

using TMPro;
using UnityEngine;

namespace DefaultNamespace
{
    public class RedAlert : MonoBehaviour, UI_IDataReceiver<string>, UI_ILayerInfo
    {
        public EUILayer TargetLayer => EUILayer.Top;
        
        [SerializeField] TextMeshProUGUI message;

        public void ReceiveData(string data) {
            message.text = data;
        }
    }
}

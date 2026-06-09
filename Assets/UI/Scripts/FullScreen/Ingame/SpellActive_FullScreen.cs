using Cards.CardUIDatas;
using Models.CardDatabases;
using UnityEngine;

namespace DefaultNamespace
{
    public class SpellActive_FullScreen : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<(string, Property)>
    {
        public EUILayer TargetLayer => EUILayer.FullScreen;

        public void ReceiveData((string, Property) data) {
            var elementColor = CardDatabase.Instance.GetElementData(data.Item2).Color;
            
        }
    }
}

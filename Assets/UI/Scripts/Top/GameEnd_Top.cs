using UnityEngine;

namespace DefaultNamespace
{
    public class GameEnd_Top : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<bool>
    {
        public EUILayer TargetLayer => EUILayer.Top;

        public GameObject winUI;
        public GameObject defeatUI;
        
        public void ReceiveData(bool isWin) {
            if (isWin) {
                winUI.SetActive(true);
                defeatUI.SetActive(false);
            }
            else {
                winUI.SetActive(false);
                defeatUI.SetActive(true);
            }
        }

        public void OnClick_Next() {
            // TODO : 게임 다 끝내고 메인 로비로 돌아가는 로직
        }
    }
}

using UnityEngine;
using UnityEngine.Serialization;

namespace DefaultNamespace
{
    public enum GameEndType {
        Win,
        Lose,
        Draw
    }
    
    
    public class GameEnd_Top : MonoBehaviour, UI_ILayerInfo, UI_IDataReceiver<GameEndType>
    {
        public EUILayer TargetLayer => EUILayer.Top;

        public GameObject winUI;
        public GameObject loseUI;
        public GameObject drawUI;
        
        public void ReceiveData(GameEndType gameEndType) {
            winUI.SetActive(false);
            loseUI.SetActive(false);
            drawUI.SetActive(false);
            
            if (gameEndType == GameEndType.Win) {
                winUI.SetActive(true);
            }else if (gameEndType == GameEndType.Lose) {
                loseUI.SetActive(true);
            }else if (gameEndType == GameEndType.Draw) {
                drawUI.SetActive(true);
            }
        }

        public void OnClick_Next() {
            Debug.Log($"여기서 메인 로비로 돌아가는 로직");
            // TODO : 게임 다 끝내고 메인 로비로 돌아가는 로직
        }
    }
}

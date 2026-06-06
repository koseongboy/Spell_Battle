using DefaultNamespace.Utilities;
using Models.PlayerModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefaultNamespace
{

    public class UI_StatusPiece : MonoBehaviour {

        [Header("UI element")] 
        public Image img_Element;
        public TextMeshProUGUI txt_Name;
        public TextMeshProUGUI txt_Turn;
        public TextMeshProUGUI txt_Stack;
        public TextMeshProUGUI txt_Desc;
        
        public void UpdateUI(StatusData data) {
            var uiData = StatusUIDataManager.Instance.GetStatusData(data.Type);

            img_Element.sprite = uiData.Icon;
            txt_Name.text = uiData.Name;
            txt_Turn.text = data.Duration + "턴";
            txt_Stack.text = data.Stacks + "스택";
            txt_Desc.text = uiData.Desc;
        }
    }
}

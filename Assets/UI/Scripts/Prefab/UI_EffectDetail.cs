using TMPro;
using UnityEngine;

namespace DefaultNamespace
{
    public class UI_EffectDetail : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI txt_Title;
        [SerializeField] private TextMeshProUGUI txt_Desc;

        public void Init(string title, string desc) {
            if (txt_Title != null) {
                txt_Title.text = title;
            }
            if (txt_Desc != null) {
                txt_Desc.text = desc;
            }
        }
    }
}

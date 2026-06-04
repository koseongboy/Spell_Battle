using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Cards.EffectInfos;
using Cards.PlayableCards;
using TMPro;

namespace DefaultNamespace
{
    public class UI_Card_Mulligan : MonoBehaviour
    {
        [Header("UI 연결")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private Image image;
        // 필요한 경우 cost, desc 텍스트 등 추가

        [Header("선택 시각화")]
        public GameObject checkMark;   // 선택 시 띄울 V표시 아이콘

        private int myIndex;
        private bool isSelected = false;
        private System.Action<int> onClickCallback;

        public void Init(PlayableCard data, int index, System.Action<int> onClick)
        {
            myIndex = index;
            onClickCallback = onClick;
            nameText.text = data.uiData.wordName;
            costText.text = data.uiData.cost.ToString();
            descText.text = data.uiData.desc;
            // TODO : image

            // 초기화 시 선택 해제 상태로 세팅
            isSelected = false;
            UpdateVisuals();
        }

        public void OnMulliganClicked()
        {
            Debug.Log("OnMulliganClicked");
            
            // 1. 시각적 상태 토글 (선택 <-> 해제)
            isSelected = !isSelected;
            UpdateVisuals();

            // 2. MulliganUI 매니저에게 클릭 사실 전달
            onClickCallback?.Invoke(myIndex);
        }

        private void UpdateVisuals()
        {
            checkMark.SetActive(isSelected);
        }
    }
}

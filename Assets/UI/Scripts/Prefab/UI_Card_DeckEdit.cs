using System;
using Cards.EffectInfos;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DefaultNamespace {
    public class UI_Card_DeckEdit : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler {
        
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private CardInDeckPiece dragPiece;

        private GenericCard cardData;
        private Action<GenericCard> onClick;
        private Action<GenericCard> onDrop;
        private RectTransform dropZone;
        
        private CardInDeckPiece dragProxy;

        public void Init(GenericCard data, Action<GenericCard> clickAction, Action<GenericCard> dropAction, RectTransform dropZoneRect) {
            cardData = data;
            onClick = clickAction;
            onDrop = dropAction;
            dropZone = dropZoneRect;
            
            nameText.text = cardData.uiData.wordName;
            costText.text = cardData.uiData.cost.ToString();
            descText.text = cardData.uiData.desc;
        }

        // ==========================================
        // 🌟 단순 클릭: 팝업 띄우기
        // ==========================================
        public void OnPointerClick(PointerEventData eventData) {
            // 드래그 중이 아닐 때만 클릭으로 인정
            if (!eventData.dragging) {
                onClick?.Invoke(cardData);
            }
        }

        // ==========================================
        // 드래그 앤 드롭 구현
        // ==========================================
        public void OnBeginDrag(PointerEventData eventData) {
            // 1. 드래그 시작 시, 자기 자신을 복제하여 캔버스 최상단에 생성
            dragProxy = Instantiate(dragPiece, GetComponentInParent<Canvas>().transform);
            dragProxy.Init( cardData, 1, (data) => { });
            
            CanvasGroup group = dragProxy.GetComponent<CanvasGroup>();

            group.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData) {
            // 마우스 커서 위치를 따라다님
            if (dragProxy != null) {
                dragProxy.transform.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData) {
            // 1. 드래그가 끝나면 따라다니던 조각 삭제
            Destroy(dragProxy.gameObject);

            // 2. 마우스를 놓은 위치가 우측 덱 리스트(dropZone) 내부인지 판별
            if (dropZone != null &&
                RectTransformUtility.RectangleContainsScreenPoint(dropZone, eventData.position,
                    eventData.pressEventCamera)) {
                onDrop?.Invoke(cardData);
            }
        }
    }
}
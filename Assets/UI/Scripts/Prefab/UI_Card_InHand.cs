using System;
using Cards.EffectInfos;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class UI_Card_InHand : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("UI 연결")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private Image image;
        
        [Header("비주얼 오브젝트 (자식 객체 연결)")]
        [SerializeField] private Transform visualTransform;
        
        [Header("호버링 세팅")]
        public float hoverOffsetY = 30f; // 위로 올라가는 높이
        public float hoverScale = 1.2f;  // 커지는 배율
        public float longHoverTime = 1.0f; // 디테일 창이 뜰 때까지 필요한 시간(초)
        
        private GenericCard cardData;
        private int handIndex; // PlayerController에 넘겨줄 내 손패 번호
        private Action<int> onClickAction; 
        
        private Vector3 baseLayoutPos;
        private Quaternion baseLayoutRot;

        // 상태 변수들
        private Vector3 originalScale;
        private bool isHovering = false;
        private float hoverTimer = 0f;
        
        private void Awake()
        {
            if (visualTransform == null) visualTransform = this.transform;
            originalScale = visualTransform.localScale;
        }

        // Init: 클릭 시 넘겨줄 index와 Action<int>로 수정됨
        public void Init(GenericCard data, int index, Action<int> clickAction) 
        {
            cardData = data;
            handIndex = index;
            onClickAction = clickAction;
            
            nameText.text = cardData.uiData.wordName;
            costText.text = cardData.uiData.cost.ToString();
            descText.text = cardData.uiData.desc;
            //TODO : Image
        }
        
        public void SetLayout(Vector3 pos, Quaternion rot)
        {
            baseLayoutPos = pos;
            baseLayoutRot = rot;

            // 마우스를 올리고 있는 중이 아닐 때만 적용 (올리고 있는데 덮어씌우면 안 되니까)
            if (!isHovering)
            {
                visualTransform.localPosition = baseLayoutPos;
                visualTransform.localRotation = baseLayoutRot;
            }
        }

        private void Update()
        {
            // 2. 롱 호버 (디테일 창) 체크 로직
            if (isHovering)
            {
                hoverTimer += Time.deltaTime;
                if (hoverTimer >= longHoverTime)
                {
                    ShowDetailWindow();
                    hoverTimer = -9999f; // 한 번 띄운 후에는 계속 실행되지 않도록 막음
                }
            }
        }

        // ==========================================
        // 🖱️ 마우스 이벤트 처리 로직
        // ==========================================

        // 1. 마우스가 카드 위로 올라왔을 때 (위로 올라오고 커짐)
        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovering = true;
            hoverTimer = 0f; // 타이머 시작

            // 시각적 요소 조작
            visualTransform.localPosition = baseLayoutPos + new Vector3(0, hoverOffsetY, 0);
            visualTransform.localRotation = Quaternion.identity; 
            visualTransform.localScale = originalScale * hoverScale;
            
            // UI 계층(Hierarchy) 맨 아래로 보내서 다른 카드에 가려지지 않고 최상단에 보이게 함
            transform.SetAsLastSibling(); 
        }

        // 1. 마우스가 카드 밖으로 나갔을 때 (원상복구)
        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            hoverTimer = 0f;

            // 시각적 요소 원상 복구
            visualTransform.localPosition = baseLayoutPos;
            visualTransform.localRotation = baseLayoutRot;
            visualTransform.localScale = originalScale;
        }

        // 3. 카드를 클릭했을 때
        public void OnPointerClick(PointerEventData eventData)
        {
            // 좌클릭일 때만 작동하도록 방어 (우클릭 등 방지)
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // PlayerController의 ToggleSpellIndex와 연결된 Action 실행 (내 인덱스 전달)
                onClickAction?.Invoke(handIndex);
                Debug.Log($"[Card UI] {cardData.uiData.wordName} 카드 클릭됨! (Index: {handIndex})");
            }
        }

        // ==========================================
        // 🔍 추후 개발 기능
        // ==========================================
        private void ShowDetailWindow()
        {
            Debug.Log($"[Card UI] 1초 경과: {cardData.uiData.wordName} 카드의 상세 정보 창 띄우기!");
            // TODO: 디테일 창 프리팹을 활성화하고 cardData.uiData 정보를 주입하는 로직
        }
    }
}

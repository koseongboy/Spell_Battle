using System;
using System.Collections;
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
        
        private Coroutine hoverCoroutine;
        
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
            transform.localPosition = pos;
            transform.localRotation = rot;
        }

        // ==========================================
        // 🖱️ 마우스 이벤트 처리 로직
        // ==========================================

        // 1. 마우스가 카드 위로 올라왔을 때 (위로 올라오고 커짐)
        public void OnPointerEnter(PointerEventData eventData)
        {
            // 1. 비주얼 튀어 오르기
            visualTransform.localPosition = new Vector3(0, hoverOffsetY, 0);
            visualTransform.localRotation = Quaternion.identity; 
            visualTransform.localScale = originalScale * hoverScale;
            transform.SetAsLastSibling(); 

            // 새 롱 호버 코루틴 시작
            if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
            hoverCoroutine = StartCoroutine(LongHoverRoutine());
        }

        // 1. 마우스가 카드 밖으로 나갔을 때 (원상복구)
        public void OnPointerExit(PointerEventData eventData)
        {
            // 1. 비주얼 원상 복구
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity; 
            visualTransform.localScale = originalScale;

            // 2. 🌟 마우스가 카드에서 나갔으므로 롱 호버 타이머(코루틴) 취소
            if (hoverCoroutine != null)
            {
                StopCoroutine(hoverCoroutine);
                hoverCoroutine = null;
            }
            
            // TODO: 만약 디테일 창이 떠 있는 상태라면 여기서 닫아주는 로직 필요
        }

        // 3. 카드를 클릭했을 때
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                // 🌟 클릭했을 때도 롱 호버 타이머를 꺼버림 (클릭했는데 창이 뜨면 안 되니까)
                if (hoverCoroutine != null)
                {
                    StopCoroutine(hoverCoroutine);
                    hoverCoroutine = null;
                }

                onClickAction?.Invoke(handIndex);
            }
        }
        
        // ==========================================
        // ⏳ 코루틴 로직
        // ==========================================
        private IEnumerator LongHoverRoutine()
        {
            // 설정한 시간(1초)만큼 조용히 대기 (Update에서 deltaTime 더하는 것과 동일한 효과)
            yield return new WaitForSeconds(longHoverTime);
            
            // 1초 동안 StopCoroutine이 불리지 않고 살아남았다면 창 띄우기!
            ShowDetailWindow();
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

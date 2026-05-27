using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class LeftUpper_Common : MonoBehaviour, UI_ILayerInfo {
        public EUILayer TargetLayer => EUILayer.Popup;
        
        [SerializeField] private Button btn_Option;
        [SerializeField] private Button btn_Friend;
        [SerializeField] private Button btn_Back;
        
        // Controller가 구독할 클릭 이벤트
        public event Action OnClicked_Option;
        public event Action OnClicked_Friend;
        public event Action OnClicked_Back;
        
        private void Start() {
            LeftUpperController.Instance.RegisterView(this);
            
            // UI 버튼 클릭 시 이벤트 발생
            btn_Option.onClick.AddListener(() => OnClicked_Option?.Invoke());
            btn_Friend.onClick.AddListener(() => OnClicked_Friend?.Invoke());
            btn_Back.onClick.AddListener(() => OnClicked_Back?.Invoke());
        }
        
        // 로비 최상단 등 뒤로 갈 곳이 없을 때 버튼을 숨기는 기능
        public void SetBackButtonActive(bool isActive)
        {
            btn_Back.gameObject.SetActive(isActive);
        }
    }
}

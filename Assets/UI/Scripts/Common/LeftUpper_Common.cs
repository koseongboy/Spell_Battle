using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class LeftUpper_Common : MonoBehaviour, UI_ILayerInfo {
        [SerializeField] private Button btn_Option;
        [SerializeField] private Button btn_Friend;
        [SerializeField] private Button btn_Back;
        
        public EUILayer TargetLayer => EUILayer.Popup;
        
        private void Start() {
            LeftUpperController.Instance.RegisterView(this);
            BindEvents();
        }

        public void BindEvents() {
            var cont = LeftUpperController.Instance;
            
            // 옵션 버튼
            btn_Option.onClick.RemoveAllListeners();
            btn_Option.onClick.AddListener( ()=>cont.OpenOptionUI() );
            
            // 친구 버튼
            btn_Friend.onClick.RemoveAllListeners();
            btn_Friend.onClick.AddListener( ()=>cont.OpenFriendUI() );
            
            // 뒤로 버튼
            var action = cont.GetAction_GoBack();
            if (action == null) {
                btn_Back.onClick.RemoveAllListeners();
                btn_Back.gameObject.SetActive(false);
            }
            else {
                btn_Back.gameObject.SetActive(true);
                btn_Back.onClick.RemoveAllListeners();
                btn_Back.onClick.AddListener( action );
            }
        }
    }
}

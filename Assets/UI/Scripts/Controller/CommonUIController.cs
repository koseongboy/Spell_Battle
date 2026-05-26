using UnityEngine;

namespace DefaultNamespace
{
    public class CommonUIController : MonoBehaviour
    {
        #region Singleton & initialization
        public static CommonUIController Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        #endregion
        
        // 뒤로가기 버튼을 위한
        [SerializeField] private string lastFullScreenUI = string.Empty;
        [SerializeField] private string currentFullScreenUI = string.Empty;
        
        [ContextMenu("Show Red Alert")]
        public void ShowRedAlert( string text ) {
            UILoader.Instance.ShowUI<string>("RedAlert_Common", text);
        }
        
        [ContextMenu("Show Black Alert")]
        public void ShowBlackAlert( string text ) {
            UILoader.Instance.ShowUI<string>("BlackAlert_Common", text);
        }

        [ContextMenu("Show Loading")]
        public void ShowLoading() {
            UILoader.Instance.ShowUI("Loading_Common");
        }

        [ContextMenu("Done Loading")]
        public void DoneLoading() {
            UILoader.Instance.HideUI("Loading_Common");
        }
        
        public bool IsGoBackAllowed() {
            if ( lastFullScreenUI == string.Empty || currentFullScreenUI == "Lobby_FullScreen" )
                return false;
            
            return true;
        }
        
        public void ChangeFullScreen(string target) {
            if (currentFullScreenUI != string.Empty) {
                UILoader.Instance.HideUI(currentFullScreenUI);
            }

            // TODO : 화면 전환 연출
            
            UILoader.Instance.ShowUI( target );
            
            lastFullScreenUI = currentFullScreenUI;
            currentFullScreenUI = target;
            
            // TODO : 하씨 이게 맞냐
            LeftUpperController.Instance.RefreshUI();
        }
        
        public void GoBack_FullScreen() {
            ChangeFullScreen( lastFullScreenUI );
        }
    }
}

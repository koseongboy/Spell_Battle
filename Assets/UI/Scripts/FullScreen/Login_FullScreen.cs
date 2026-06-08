using System;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using TMPro;
using Managers.LocalDataManagers;

namespace DefaultNamespace
{
    public class Login_FullScreen : MonoBehaviour
    {
        [Header("UI 요소 연결")]
        public TMP_InputField idInputField;
        public TMP_InputField pwInputField;
        
        [Header("회원가입 팝업")] 
        public GameObject registerPanel;
        public TMP_InputField regIdInput;
        public TMP_InputField regPwInput;
        public RectTransform registerRect;
        public CanvasGroup registerCanvasGroup;
        
        [Header("DOTween Settings")]
        public float animDuration = 0.4f; // 애니메이션 재생 시간
        public Vector2 startOffset = new Vector2(0, -500f); // 아래에서 올라올 시작 위치 (화면 해상도에 맞춰 조절)
        
        private Vector2 registerOriginalPosition;
        private bool isRegisterOn = false;

        private void Awake() {
            registerOriginalPosition = registerRect.anchoredPosition;
            
            // 팩트: TMP_InputField의 onSubmit 이벤트는 유저가 해당 입력창에서 '엔터'를 눌렀을 때만 발동합니다.
            idInputField.onSubmit.AddListener(OnSubmitPressed);
            pwInputField.onSubmit.AddListener(OnSubmitPressed);
        }
        
        private void Start()
        {
            // TODO : AutoLogin. 서버쪽 준비되면 주석 해제. 
            // TryAutoLogin();
        }

        private void Update()
        {
            // Tab 키를 눌렀을 때
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // 1. 회원가입 창이 켜져있을 때의 탭 이동
                if (isRegisterOn)
                {
                    if (regIdInput.isFocused) regPwInput.ActivateInputField();
                    else regIdInput.ActivateInputField(); // Shift+Tab 대신 편의상 루프
                }
                // 2. 기본 로그인 창일 때의 탭 이동
                else
                {
                    if (idInputField.isFocused) pwInputField.ActivateInputField();
                    else idInputField.ActivateInputField(); // 다시 아이디로 루프
                }
            }
        }

        private void OnEnable() {
            isRegisterOn = false;
            registerPanel.SetActive(false);
            idInputField.ActivateInputField();
        }
        
        private void OnDestroy()
        {
            if (idInputField != null) idInputField.onSubmit.RemoveListener(OnSubmitPressed);
            if (pwInputField != null) pwInputField.onSubmit.RemoveListener(OnSubmitPressed);
        }

        private async void TryAutoLogin() {
            var ldm = LocalDataManager.Instance;
            ldm.LoadData();
            if (ldm.userToken != string.Empty)
            {
                string savedToken = ldm.userToken;
                
                CommonUIController.Instance.ShowLoading();

                // 2. AuthManager에게 자동 로그인 통신 처리를 위임합니다.
                bool isAutoSuccess = await AuthManager.Instance.RequestAutoLoginAsync(savedToken);

                if (isAutoSuccess)
                {
                    await Controllers.LobbyController.LobbyController.Instance.InitializeNetworkAsync();

                    UILoader.Instance.HideUI("Login_FullScreen");
                    CommonUIController.Instance.ChangeFullScreen("Lobby_FullScreen");
                    UILoader.Instance.ShowUI("LeftUpper_Common");
                }
            }
        }
        
        /// <param name="text">입력창에 적혀있던 최종 텍스트</param>
        private void OnSubmitPressed(string text)
        {
            // ID나 PW 입력창 중 하나라도 포커스가 가 있는 상태에서 엔터를 치면 즉시 로그인 프로세스 가동
            if (idInputField.isFocused || pwInputField.isFocused)
            {
                OnLoginButtonClick();
            }
        }

        public async void OnLoginButtonClick()
        {
            string id = idInputField.text;
            string pw = pwInputField.text;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
            {
                CommonUIController.Instance.ShowRedAlert("아이디와 비밀번호를 모두 입력하세요.");
                return;
            }

            CommonUIController.Instance.ShowLoading();
            
            bool isSuccess = await AuthManager.Instance.RequestLoginAsync(id, pw);

            if (isSuccess)
            {
                await Controllers.LobbyController.LobbyController.Instance.InitializeNetworkAsync();
                
                UILoader.Instance.HideUI("Login_FullScreen");
                CommonUIController.Instance.ChangeFullScreen("Lobby_FullScreen");
                UILoader.Instance.ShowUI("LeftUpper_Common");
            }
            else
            {
                CommonUIController.Instance.DoneLoading();
                CommonUIController.Instance.ShowRedAlert("로그인에 실패했습니다. 계정 정보를 확인하세요.");
            }
        }

        public void TogglePopup_Register() {
            isRegisterOn = !isRegisterOn;
            
            registerRect.DOKill();
            registerCanvasGroup.DOKill();

            if (isRegisterOn)
            {
                registerPanel.SetActive(true);
                registerRect.anchoredPosition = registerOriginalPosition + startOffset;
                registerRect.localScale = Vector3.one * 0.5f;
                registerCanvasGroup.alpha = 0f;

                registerRect.DOAnchorPos(registerOriginalPosition, animDuration).SetEase(Ease.OutQuint);
                registerRect.DOScale(Vector3.one, animDuration).SetEase(Ease.OutQuint);
                registerCanvasGroup.DOFade(1f, animDuration).SetEase(Ease.OutQuint);
                regIdInput.ActivateInputField();
            }
            else
            {
                registerRect.DOAnchorPos(registerOriginalPosition + startOffset, animDuration).SetEase(Ease.InQuint);
                registerRect.DOScale(Vector3.one * 0.5f, animDuration).SetEase(Ease.InQuint);
                registerCanvasGroup.DOFade(0f, animDuration).SetEase(Ease.InQuint)
                    .OnComplete(() => 
                    {
                        registerPanel.SetActive(false);
                    });
                idInputField.ActivateInputField();
            }
        }
        
        // --- 회원가입 버튼 클릭 이벤트 ---
        public async void OnRegisterButtonClick()
        {
            string id = regIdInput.text;
            string pw = regPwInput.text;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
            {
                CommonUIController.Instance.ShowRedAlert("아이디와 비밀번호를 모두 입력하세요.");
                return;
            }

            CommonUIController.Instance.ShowLoading();

            // 로직(Controller) 호출
            bool isSuccess = await AuthManager.Instance.RequestRegisterAsync(id, pw);

            if (isSuccess)
            {
                CommonUIController.Instance.DoneLoading();
                CommonUIController.Instance.ShowBlackAlert("가입 완료! 로그인해주세요.");
            }
            else
            {
                CommonUIController.Instance.DoneLoading();
                CommonUIController.Instance.ShowRedAlert("회원가입 실패. 이미 존재하는 아이디일 수 있습니다.");

            }
        }
    }
}

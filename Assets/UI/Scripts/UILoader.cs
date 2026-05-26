using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DefaultNamespace
{
    public class UILoader : MonoBehaviour
    {
        #region Fields & Properties
        [SerializeField] private Transform canvas_FullScreen;    
        [SerializeField] private Transform canvas_Popup;    
        [SerializeField] private Transform canvas_Top;    

        [SerializeField] 
        private GameObject devGrid;

        [SerializeField]
        private List<string> awakeUIList = new List<string>();

        // 비동기 로드 핸들을 관리하는 딕셔너리
        private Dictionary<string, AsyncOperationHandle<GameObject>> loadUIHandles = new();
        // 생성된 UI 인스턴스를 관리하는 딕셔너리
        private Dictionary<string, GameObject> loadedUIs = new();


        // 뒤로가기 버튼을 위한
        [SerializeField] private string lastFullScreenUI = string.Empty;
        [SerializeField] private string currentFullScreenUI = string.Empty;

        #endregion

        #region Singleton & initialization
        public static UILoader Instance { get; private set; }
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #endregion

        #region public methods

        /// <summary>
        /// 데이터를 전달하지 않고 UI를 활성화합니다.
        /// </summary>
        public void ShowUI(string uiName)
        {
            if (loadedUIs.TryGetValue(uiName, out GameObject uiInstance) && uiInstance != null)
            {
                uiInstance.SetActive(true);
            }
            else
            {
                // 인스턴스가 없거나 null이면 새로 로드 (에러 방어 포함)
                if (uiInstance == null) loadedUIs.Remove(uiName);
                
                LoadUI(uiName, (instance) =>
                {
                    if (instance != null) instance.SetActive(true);
                });
            }
        }

        /// <summary>
        /// 단일 데이터 또는 ValueTuple로 묶인 다중 데이터를 전달하며 UI를 활성화합니다.
        /// </summary>
        /// <typeparam name="T">전달할 데이터의 타입 (단일 객체, struct, 또는 ValueTuple)</typeparam>
        public void ShowUI<T>(string uiName, T data)
        {
            if (loadedUIs.TryGetValue(uiName, out GameObject uiInstance) && uiInstance != null)
            {
                uiInstance.SetActive(true);
                SendDataToUI(uiInstance, data);
            }
            else
            {
                if (uiInstance == null) loadedUIs.Remove(uiName);

                LoadUI(uiName, (instance) =>
                {
                    if (instance != null)
                    {
                        instance.SetActive(true);
                        SendDataToUI(instance, data);
                    }
                });
            }
        }

        public void HideUI(string uiName)
        {
            if (loadedUIs.TryGetValue(uiName, out GameObject uiInstance))
            {
                if (uiInstance != null)
                {
                    uiInstance.SetActive(false);
                }
                else
                {
                    Debug.LogWarning($"UI {uiName} 인스턴스가 null 입니다. 언로드 시도합니다.");
                    loadedUIs.Remove(uiName);
                    UnloadUI(uiName);
                }
            }
            else
            {
                Debug.LogWarning($"UI {uiName} 는 로드된 상태가 아닙니다.");
            }
        }

        /// <summary>
        /// Addressables를 이용해 UI 프리팹을 비동기 로드하고 인스턴스화합니다.
        /// </summary>
        public void LoadUI(string uiName, Action<GameObject> onInstanceCreated = null)
        {
            // 1. 이미 로드 요청이 완료되어 인스턴스가 존재하는 경우
            if (loadedUIs.TryGetValue(uiName, out GameObject existingInstance) && existingInstance != null)
            {
                onInstanceCreated?.Invoke(existingInstance);
                return;
            }

            // 2. 이미 로드 요청이 진행 중인 경우 (Race Condition 방지)
            if (loadUIHandles.TryGetValue(uiName, out var existingHandle))
            {
                // 로딩이 완료되었을 때 실행될 콜백 체인에 추가 등록
                existingHandle.Completed += (handle) =>
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        // 먼저 완료된 요청에 의해 생성되었을 인스턴스를 찾아 전달
                        if (loadedUIs.TryGetValue(uiName, out GameObject inst))
                        {
                            onInstanceCreated?.Invoke(inst);
                        }
                    }
                    else
                    {
                        onInstanceCreated?.Invoke(null);
                    }
                };
                return;
            }

            // 3. 완전히 새로 로드하는 경우
            AsyncOperationHandle<GameObject> loadHandle = Addressables.LoadAssetAsync<GameObject>(uiName);
            loadUIHandles.Add(uiName, loadHandle);

            loadHandle.Completed += (handle) =>
            {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    GameObject uiPrefab = handle.Result;
                    
                    Transform targetParent = canvas_FullScreen; // Default
                    if (uiPrefab.TryGetComponent<UI_ILayerInfo>(out var layerInfo)) {
                        targetParent = layerInfo.TargetLayer switch {
                            EUILayer.FullScreen => canvas_FullScreen,
                            EUILayer.Popup => canvas_Popup,
                            EUILayer.Top => canvas_Top,
                            _ => canvas_FullScreen
                        };
                    }
                    GameObject uiInstance = Instantiate(uiPrefab, targetParent);
                    
                    // 딕셔너리에 인스턴스 등록 시 중복 체크 방어
                    if (!loadedUIs.ContainsKey(uiName))
                    {
                        loadedUIs.Add(uiName, uiInstance);
                    }
                    
                    onInstanceCreated?.Invoke(uiInstance);
                }
                else
                {
                    Debug.LogError($"UI 로드 실패: {uiName}");
                    loadUIHandles.Remove(uiName);
                    Addressables.Release(handle);   
                    
                    onInstanceCreated?.Invoke(null); 
                }
            };
        }

        public void UnloadUI(string uiName)
        {
            if (loadedUIs.TryGetValue(uiName, out GameObject uiInstance))
            {
                if (uiInstance != null)
                {
                    Destroy(uiInstance);
                }
                loadedUIs.Remove(uiName);
            }

            if (loadUIHandles.TryGetValue(uiName, out var loadHandle))
            {
                Addressables.Release(loadHandle);
                loadUIHandles.Remove(uiName);
                Debug.Log($"UI 언로드 완료: {uiName}");
            }
            else
            {
                Debug.LogWarning($"UI {uiName} 는 로드 핸들이 존재하지 않습니다.");
            }
        }

        /// <summary>
        /// 기존 하드코딩되었던 ShowAlert 방식을 범용 제네릭 ShowUI로 통합 처리합니다.
        /// </summary>
        public void ShowAlert(string text)
        {
            // 이제 독립적인 하드코딩 메서드 대신 범용 ShowUI<T> 구조를 통해 호출 가능합니다.
            ShowUI<string>("UI_Alert", text);
        }
        
        public void ShowRedAlert( string text ) {
            ShowUI<string>("RedAlert_Common", text);
        }
        
        public void ShowBlackAlert( string text ) {
            ShowUI<string>("BlackAlert_Common", text);
        }

        public void ShowLoading() {
            Debug.Log("TODO : 여기서 로딩창 나와야함.");
        }
        
        public bool IsGoBackAllowed() {
            if ( lastFullScreenUI == string.Empty || currentFullScreenUI == "Lobby_FullScreen" )
                return false;
            
            return true;
        }
        
        public void ChangeFullScreen(string target) {
            if (currentFullScreenUI != string.Empty) {
                HideUI(currentFullScreenUI);
            }

            // TODO : 화면 전환 연출
            
            ShowUI( target );
            
            lastFullScreenUI = currentFullScreenUI;
            currentFullScreenUI = target;
            
            // TODO : 하씨 이게 맞냐
            LeftUpperController.Instance.RefreshUI();
        }
        
        public void GoBack_FullScreen() {
            ChangeFullScreen( lastFullScreenUI );
        }

        #endregion

        #region private methods

        private void SendDataToUI<T>(GameObject uiInstance, T data)
        {
            if (data == null) return;

            // 해당 인터페이스를 구현한 컴포넌트가 있는지 TryGetComponent(제네릭)로 탐색
            if (uiInstance.TryGetComponent<UI_IDataReceiver<T>>(out var receiver))
            {
                receiver.ReceiveData(data);
            }
        }

        /// <summary>
        /// 씬이 시작될 때 Show되어야할 UI들을 Show
        /// </summary>
        private void ShowUIOnSceneStart()
        {
            foreach (var uiName in awakeUIList)
            {
                ShowUI(uiName);
            }
        }

        #endregion

        #region Unity event methods

        private void Start()
        {
            ShowUIOnSceneStart();
        }

        #endregion


        #region DEV
        

        #endregion



    }
}
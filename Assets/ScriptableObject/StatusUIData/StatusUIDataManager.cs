using System.Collections.Generic;
using System.Threading.Tasks;
using Models.PlayerModels;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DefaultNamespace
{
    public class StatusUIDataManager : MonoBehaviour
    {
        public static StatusUIDataManager Instance { get; private set; }

        [Header("어드레서블 로드 라벨 설정")]
        [SerializeField] private AssetLabelReference targetLabel;

        private Dictionary<StatusType, StatusUiData> _statusDataCache;
        private AsyncOperationHandle<IList<StatusUiData>> _loadHandle; // 메모리 해제를 위한 핸들 보관

        // 비동기 로딩이 완료되었는지 확인하는 플래그
        public bool IsReady { get; private set; } = false;

        private void Awake()
        {
            // 팩트: 씬이 전환되어도 매니저가 파괴되지 않도록 유지하고, 중복 생성을 방지합니다.
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                // 비동기 데이터 로드 시작
                _ = InitializeManagerAsync();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 지정된 라벨(targetLabel)이 붙은 모든 SO 에셋을 Addressable로 비동기 로드하여 캐시를 구축합니다.
        /// </summary>
        private async Task InitializeManagerAsync()
        {
            _statusDataCache = new Dictionary<StatusType, StatusUiData>();

            // 팩트: 단일 주소(Address)가 아닌, Label 기반으로 여러 에셋을 동시에 요청합니다.
            _loadHandle = Addressables.LoadAssetsAsync<StatusUiData>(targetLabel, null);

            // 데이터 로드가 끝날 때까지 대기
            await _loadHandle.Task;

            if (_loadHandle.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (StatusUiData data in _loadHandle.Result)
                {
                    if (data == null) continue;

                    if (!_statusDataCache.ContainsKey(data.Type))
                    {
                        _statusDataCache.Add(data.Type, data);
                    }
                    else
                    {
                        Debug.LogWarning($"[StatusDataManager] 중복된 StatusType 발견: {data.Type}. 첫 번째 에셋만 등록됩니다.");
                    }
                }

                IsReady = true;
                Debug.Log($"[StatusDataManager] Addressables로 총 {_statusDataCache.Count}개의 상태이상 UI 데이터 로드 완료.");
            }
            else
            {
                Debug.LogError("[StatusDataManager] Addressables 로드 실패! 라벨 이름이나 에셋 등록 상태를 확인하세요.");
            }
        }

        /// <summary>
        /// 상태이상 데이터 조회 함수
        /// </summary>
        public StatusUiData GetStatusData(StatusType type)
        {
            if (!IsReady)
            {
                Debug.LogWarning("[StatusDataManager] 아직 Addressables 로딩이 끝나지 않았습니다!");
                return null;
            }

            // 딕셔너리에서 안전하게 키값을 검사하여 리턴
            if (_statusDataCache != null && _statusDataCache.TryGetValue(type, out StatusUiData data))
            {
                return data;
            }

            Debug.LogWarning($"[StatusDataManager] {type}에 해당하는 데이터를 찾을 수 없습니다.");
            return null;
        }

        private void OnDestroy()
        {
            // 팩트: 매니저가 파괴될 때 사용했던 리소스를 시스템 메모리(RAM)에서 완전히 해제합니다.
            if (_loadHandle.IsValid())
            {
                Addressables.Release(_loadHandle);
            }
        }
    }
}

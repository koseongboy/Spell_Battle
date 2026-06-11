using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // 씬 전환 이벤트를 위해 필수
using Managers.VoiceManagers;

public class GlobalButtonSoundAssigner : MonoBehaviour
{
    public static GlobalButtonSoundAssigner Instance { get; private set; }

    private void Awake()
    {
        // 🌟 1. 싱글톤 구조 및 DontDestroyOnLoad 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 씬이 로드될 때마다 자동으로 실행되도록 이벤트 구독 등록!
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            // 중복 생성 방지
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // 🌟 2. 오브젝트가 파괴될 때는 메모리 누수 방지를 위해 이벤트 구독 해제
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 🌟 3. 새로운 씬이 완전히 로드되면 유니티가 자동으로 이 함수를 호출합니다.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AssignSoundToAllButtons(scene.name);
    }

    /// <summary>
    /// 현재 씬의 모든 버튼을 찾아 클릭 사운드 이벤트를 할당합니다.
    /// </summary>
    private void AssignSoundToAllButtons(string sceneName)
    {
        // 4. 현재 활성화되어 있는 '모든' Button 컴포넌트를 싹 다 긁어모읍니다.
        // (비활성화 상태인 버튼도 포함하고 싶다면 인자에 true를 넘겨줍니다)
        Button[] allButtons = FindObjectsOfType<Button>(true);
        int assignedCount = 0;

        foreach (Button btn in allButtons)
        {
            // 5. 중복 등록 방지 (매우 중요!)
            // 버튼 이벤트는 리스너를 계속 누적해서 등록하기 때문에, 
            // 씬이 재로드되거나 기존 버튼이 유지될 때 소리가 여러 번 겹쳐 나는 것을 막기 위해 
            // 한 번 빼고 다시 등록하는 방식을 쓰거나 안전하게 등록합니다.
            // 여기서는 람다식 중복을 피하기 위해 리스너를 한 번 싹 비우는 것이 아니라, 
            // 깔끔하게 한 번만 등록되도록 익명 함수 형태로 추가합니다.
            
            btn.onClick.AddListener(() =>
            {
                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayDefaultButtonSFX();
                }
            });

            assignedCount++;
        }

        

        if (assignedCount > 0)
        {
            Debug.Log($"[SoundManager] 🎬 씬 '{sceneName}' 로드 완료: 총 {assignedCount}개의 버튼에 클릭 사운드 자동 연동!");
        }
    }

    /// <summary>
    /// 동적으로 생성된 특정 UI 오브젝트 하위의 모든 버튼에 사운드를 입힙니다.
    /// </summary>
    public void AssignSoundToTargetUI(GameObject targetUI)
    {
        if (targetUI == null) return;

        Button[] buttons = targetUI.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            btn.onClick.RemoveListener(PlayDefaultSFXInternal); 
            btn.onClick.AddListener(PlayDefaultSFXInternal);
        }
    }

    private void PlayDefaultSFXInternal()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayDefaultButtonSFX();
        }
    }
}
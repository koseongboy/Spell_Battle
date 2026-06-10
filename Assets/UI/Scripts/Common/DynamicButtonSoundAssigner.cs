using UnityEngine;
using UnityEngine.UI;
using Managers.VoiceManagers;

public class DynamicButtonSoundAssigner : MonoBehaviour
{
    // OnEnable은 SetActive(true)가 되거나 화면에 등장할 때마다 '무조건' 실행됩니다.
    private void OnEnable()
    {
        // true를 넣어주면 현재 비활성화되어 있는 자식 버튼들까지 싹 다 찾아냅니다! (애니메이션 대기용 버튼 구출)
        Button[] buttons = GetComponentsInChildren<Button>(true);
        
        foreach (Button btn in buttons)
        {
            // 중복 등록 방지 (기존에 걸려있던 사운드 리스너가 있다면 제거)
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
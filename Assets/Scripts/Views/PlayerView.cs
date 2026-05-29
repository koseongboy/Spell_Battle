using UnityEngine;
using Controllers.PlayerController;
using Models.PlayerModels;
using Unity.Netcode;
// using TMPro;

namespace Views.PlayerView // 기존에 쓰시던 네임스페이스 그대로 사용!
{
    public class PlayerView : MonoBehaviour
    {
        // 씬에서 컨트롤러가 이 View를 쉽게 찾을 수 있도록 싱글톤 처리
        public static PlayerView Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // ==========================================
        // 🔌 플레이어가 태어나면 연결(Bind)하는 함수
        // ==========================================
        public void Bind(PlayerController myPlayer)
        {
            myPlayer.OnHpChanged += UpdateHealth;
            myPlayer.OnManaChanged += UpdateMana;

            // 초기값 세팅
            UpdateHealth(myPlayer.CurrentHp);
            UpdateMana(myPlayer.CurrentMana);
            
            Debug.Log("✅ [PlayerView] 내 캐릭터와 화면이 성공적으로 연결되었습니다.");
        }

        // ==========================================
        // 🎨 실제 화면 갱신 로직 (기존에 짜두셨던 코드 활용)
        // ==========================================
        public  void UpdateHealth(int currentHp)
        {
            Debug.Log("채력 설정 완료: " + currentHp);
            // hpText.text = currentHp.ToString();
        }

        public void UpdateMana(int currentMana)
        {
            Debug.Log("마나 설정 완료: " + currentMana);
        }
        public  void UpdateStatuses(NetworkList<StatusData> statuses)
        {
            Debug.Log("상태이상이 변경됐습니다.");
        }
    }
}
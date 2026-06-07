using System.Collections.Generic;
using Cards.CardUIDatas;
using UnityEngine;
using Cards.EffectInfos; 

namespace Models.CardDatabases 
{
    [CreateAssetMenu(fileName = "NewPresetDeck", menuName = "CardData/PresetDeck")]
    public class PresetDeckData : ScriptableObject 
    {
        [Header("프리셋 기본 정보")]
        public string presetId; 
        public string deckName; 
        [TextArea] public string description;
        public Property representativeProperty; 

        [Header("덱 구성 (카드 ID 목록)")]
        public List<int> cardIds = new List<int>();

        // ==========================================
        // 💡 꼼수 공간: 여기에 제가 준 배열 텍스트를 통째로 복붙하세요.
        // ==========================================
        [Header("빠른 입력 (텍스트 붙여넣고 우클릭 -> Parse 적용)")]
        [TextArea(3, 10)]
        public string rawIdText;

        // 인스펙터 스크립트 이름에서 우클릭하면 실행할 수 있는 버튼을 만들어줍니다.
        [ContextMenu("붙여넣은 텍스트로 ID 자동 채우기")]
        public void ParseIdsFromText()
        {
            if (string.IsNullOrWhiteSpace(rawIdText)) return;

            cardIds.Clear();

            // 1. 대괄호, 줄바꿈, 띄어쓰기 등 불필요한 문자를 전부 날려버립니다.
            string cleanText = rawIdText.Replace("[", "")
                .Replace("]", "")
                .Replace("\n", "")
                .Replace("\r", "")
                .Replace(" ", "");

            // 2. 쉼표(,)를 기준으로 쪼갭니다.
            string[] splitText = cleanText.Split(',');

            // 3. 숫자로 변환해서 리스트에 쑤셔 넣습니다.
            foreach (string s in splitText)
            {
                if (int.TryParse(s, out int parsedId))
                {
                    cardIds.Add(parsedId);
                }
            }

            Debug.Log($"[PresetDeckData] 파싱 완료! 총 {cardIds.Count}장의 카드가 입력되었습니다.");
            
            // 파싱이 끝난 후 보기 깔끔하게 텍스트 공간은 비워줍니다 (선택 사항)
            rawIdText = ""; 
        }
    }
}
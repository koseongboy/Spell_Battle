using System.Collections.Generic;
using UnityEngine;


namespace Models.Voices
{
    public class SpellValidator
    {
        /// <summary>
        /// STT로 인식된 텍스트에 필수 단어들이 모두 포함되어 있는지 검증합니다.
        /// </summary>
        /// <param name="sttResult">플레이어가 실제로 발음한 문장 (STT 결과)</param>
        /// <param name="requiredWords">반드시 들어가야 할 단어 리스트 (접두어, 카드명 등)</param>
        /// <returns>모든 단어가 포함되었는지 여부</returns>
        public static bool ValidateIncantation(string sttResult, List<string> requiredWords)
        {
            // 1. STT 결과를 띄어쓰기(어절) 단위로 분리하여 배열로 만듭니다.
            string[] spokenWords = sttResult.Split(new char[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

            bool isAllIncluded = true;

            foreach (string targetWord in requiredWords)
            {
                bool wordFound = false;

                // 2. 각 어절을 순회하며 해당 타겟 단어가 포함되어 있는지 확인합니다.
                foreach (string spoken in spokenWords)
                {
                    // '파이어볼을', '파이어볼이' 처럼 조사가 붙을 수 있으므로 Contains 사용
                    if (spoken.Contains(targetWord))
                    {
                        wordFound = true;
                        break;
                    }
                }

                if (!wordFound)
                {
                    Debug.LogWarning($"[SpellValidator] 실패: '{targetWord}' 단어를 발음하지 않았거나 인식되지 않았습니다.");
                    isAllIncluded = false;
                }
            }

            return isAllIncluded;
        }
    }
}

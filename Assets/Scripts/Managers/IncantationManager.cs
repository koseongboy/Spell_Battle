using UnityEngine;

namespace Managers
{
    public class IncantationManager
    {
        private static IncantationManager instance;
        public static IncantationManager Instance => instance ??= new IncantationManager();

        private Models.Databases.ConceptData[] concepts;
        private Models.Databases.PrefixData[] prefixes;

        private IncantationManager()
        {
            // 🌟 꿀팁: Resources/Incantations/ 폴더 안의 SO들을 싹 다 긁어옵니다.
            concepts = Resources.LoadAll<Models.Databases.ConceptData>("Incantations/Concepts");
            prefixes = Resources.LoadAll<Models.Databases.PrefixData>("Incantations/Prefixes");
            
            Debug.Log($"[IncantationManager] 컨셉 {concepts.Length}개, 접두어 {prefixes.Length}개 로드 완료!");
        }

        // 튜플(Tuple)을 사용하여 믹스 앤 매치 결과 반환
        public (string concept, string prefix) GetRandomIncantation()
        {
            string randomConcept = "평범하게";
            string randomPrefix = "빛이여";

            if (concepts != null && concepts.Length > 0)
            {
                randomConcept = concepts[Random.Range(0, concepts.Length)].text;
            }

            if (prefixes != null && prefixes.Length > 0)
            {
                randomPrefix = prefixes[Random.Range(0, prefixes.Length)].text;
            } else Debug.LogWarning("선제문이 로드가 안 되고 있다");

            return (randomConcept, randomPrefix);
        }
    }
}
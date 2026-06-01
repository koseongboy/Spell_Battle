#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cards.EffectInfos;
using Models.PlayerModels;
using UnityEditor;
using UnityEngine;

namespace DefaultNamespace.Editor {
    public class CSVToCardGenerator : EditorWindow {
        // 유니티 상단 메뉴에 'Tools > 카드 자동 생성기' 메뉴를 만듭니다.
        [MenuItem("Tools/카드 자동 생성기")]
        public static void ShowWindow() {
            GetWindow<CSVToCardGenerator>("카드 자동 생성기");
        }

        private TextAsset csvFile;
        private string savePath = "Assets/Resources/Cards/PlayableCard"; // 에셋이 저장될 폴더 경로

        private void OnGUI() {
            GUILayout.Label("CSV 파일로 카드 SO 자동 생성", EditorStyles.boldLabel);

            csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV 파일", csvFile, typeof(TextAsset), false);
            savePath = EditorGUILayout.TextField("저장 경로", savePath);

            if (GUILayout.Button("생성 시작")) {
                if (csvFile == null) {
                    Debug.LogError("CSV 파일을 등록해주세요!");
                    return;
                }

                GenerateCards();
            }
        }

    private void GenerateCards()
        {
            // 저장 폴더가 없으면 에러가 나므로 미리 생성
            if (!AssetDatabase.IsValidFolder(savePath))
            {
                Directory.CreateDirectory(savePath);
            }

            // CSV 텍스트를 줄바꿈 단위로 분리 (첫 줄은 헤더이므로 스킵)
            string[] lines = csvFile.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // 안전한 파싱을 위한 로컬 헬퍼 함수
            int ParseInt(string s) => int.TryParse(s, out int result) ? result : 0;
            bool ParseBool(string s) => bool.TryParse(s, out bool result) ? result : false;

            for (int i = 1; i < lines.Length; i++)
            {
                // 쉼표(,) 기준으로 데이터 분리
                string[] row = lines[i].Split(',');

                // 최소 첫 번째 효과 세팅(16열)까지 데이터가 없으면 건너뜀
                if (row.Length < 16)
                {
                    Debug.LogWarning($"[CSV Parser] 데이터가 부족한 행을 건너뜁니다: {lines[i]}");
                    continue;
                }

                // 1. ScriptableObject 인스턴스 메모리 상에 생성
                GenericCard newCard = ScriptableObject.CreateInstance<GenericCard>();
                newCard.uiData = new Cards.CardUIDatas.CardUIData();
                
                // 2. 기본 정보 파싱 (0열 ~ 4열)
                newCard.uiData.id = ParseInt(row[0]);
                newCard.uiData.wordName = row[1];
                newCard.uiData.cost = ParseInt(row[2]);
                Enum.TryParse(row[3], out Cards.CardUIDatas.Property prop);
                newCard.uiData.property = prop;
                newCard.uiData.desc = row[4];

                // Effects 리스트 초기화
                newCard.uiData.Effects = new List<EffectInfo>();

                // 3. 첫 번째 효과 파싱 (5열 ~ 15열)
                EffectInfo effect1 = new EffectInfo();
                
                Enum.TryParse(row[5], out EffectType eType1);
                Enum.TryParse(row[6], out TargetType tType1);
                Enum.TryParse(row[9], out StatusType sType1);
                Enum.TryParse(row[10], out ConditionType cType1);
                Enum.TryParse(row[11], out Cards.CardUIDatas.Property cProp1);

                effect1.effectType = eType1;
                effect1.targetType = tType1;
                effect1.value1 = ParseInt(row[7]);
                effect1.value2 = ParseInt(row[8]);
                effect1.statusType = sType1;

                // 조건 설정 맵핑
                effect1.condition = cType1;
                effect1.conditionProperty = cProp1;
                effect1.useConditionalValues = ParseBool(row[12]);
                effect1.conditionalValue1 = ParseInt(row[13]);
                effect1.conditionalValue2 = ParseInt(row[14]);
                effect1.specificCardId = row[15];

                newCard.uiData.Effects.Add(effect1);

                // 4. 두 번째 효과 파싱 (16열 ~ 20열) - 대격변, 허공처럼 효과가 2개인 경우
                // row[16]에 "None"이 아니거나 비어있지 않은 실제 EffectType 값이 들어있을 때만 추가
                if (row.Length >= 21 && !string.IsNullOrWhiteSpace(row[16]) && row[16].Trim() != "None")
                {
                    EffectInfo effect2 = new EffectInfo();
                    
                    Enum.TryParse(row[16], out EffectType eType2);
                    Enum.TryParse(row[17], out TargetType tType2);
                    Enum.TryParse(row[20], out StatusType sType2);

                    effect2.effectType = eType2;
                    effect2.targetType = tType2;
                    effect2.value1 = ParseInt(row[18]);
                    effect2.value2 = ParseInt(row[19]);
                    effect2.statusType = sType2;

                    newCard.uiData.Effects.Add(effect2);
                }
                
                // ==========================================
                // 5. 키워드(Keywords) 파싱 (21번째 열 이후) 추가
                // ==========================================
                newCard.uiData.Keywords = new List<CardKeyword>();
                
                // CSV에 21번째 열(인덱스 21)이 존재하고 데이터가 비어있지 않은지 확인
                if (row.Length >= 22 && !string.IsNullOrWhiteSpace(row[21]) && row[21].Trim() != "None") {
                    string rawKeywordString = row[21];
                    string[] splitKeywords = rawKeywordString.Split('|');

                    foreach (string kwText in splitKeywords) {
                        // 대소문자 무시(true)하여 Enum으로 파싱
                        if (Enum.TryParse(kwText.Trim(), true, out CardKeyword parsedKeyword)) {
                            newCard.uiData.Keywords.Add(parsedKeyword);
                        } else {
                            Debug.LogWarning($"[CSV Parser] ID {newCard.uiData.id}의 키워드 파싱 실패: '{kwText}'는 올바른 CardKeyword가 아닙니다.");
                        }
                    }
                }
                
                // 6. 속성(Property)별 폴더 경로 지정 및 에셋 파일(.asset) 저장
                string propertyName = newCard.uiData.property.ToString(); // 예: "Fire", "Water"
                string targetFolderPath = $"{savePath}/{propertyName}";

                // 해당 속성의 폴더가 존재하는지 확인하고, 없다면 새로 생성
                if (!AssetDatabase.IsValidFolder(targetFolderPath))
                {
                    // System.IO.Directory를 사용하면 하위 폴더까지 한 번에 생성이 가능합니다.
                    Directory.CreateDirectory(targetFolderPath);
                }

                // 파일 이름을 Card_{id}.asset 형태로 지정
                string assetPath = $"{targetFolderPath}/Card_{newCard.uiData.id}.asset";

                // 덮어쓰기 로직 (프리팹 참조 끊김 방지)
                GenericCard existingCard = AssetDatabase.LoadAssetAtPath<GenericCard>(assetPath);
            
                if (existingCard != null)
                {
                    // 이미 에셋이 존재하면, 새로 만든 데이터(newCard)를 기존 에셋(existingCard)에 복사해서 덮어씌움
                    EditorUtility.CopySerialized(newCard, existingCard);
                    EditorUtility.SetDirty(existingCard); // 변경 사항이 있음을 유니티 엔진에 알림
                }
                else
                {
                    // 에셋이 존재하지 않으면 새로 생성
                    AssetDatabase.CreateAsset(newCard, assetPath);
                }
            }

            // 변경된 에셋 데이터베이스 저장 및 에디터 새로고침
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"총 {lines.Length - 1}개의 카드 생성이 완료되었습니다!");
        }
    }
}
#endif
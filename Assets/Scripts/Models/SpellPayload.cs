using System;
using System.Collections.Generic;
using System.IO;
using Models.EffectCommands;
using Models.EvaluationRequests;
using Models.PlayerModels;
using Cards.CardUIDatas;
using Cards.EffectInfos;
using Cards.PlayableCards;
using Newtonsoft.Json;

namespace Models.SpellPayloads
{
    /// <summary>
    /// 주문 1회 분량의 데이터 묶음.
    /// - EvalData : AI 평가 서버에 보낼 컨셉/선주문/단어
    /// - Commands : 카드 효과로부터 생성된 EffectCommand 목록 (우선순위 정렬 후 실행)
    /// 메인 속성은 사용한 카드 속성의 최빈값으로 결정되며, 카드 효과의 발동 조건으로 쓰인다.
    /// </summary>
    public class SpellPayload
    {
        public EvaluationRequest EvalData = new EvaluationRequest();
        public List<EffectCommand> Commands = new List<EffectCommand>();
        public List<int> UsedCardIds = new List<int>();

        public List<Property> PropertyHistory = new List<Property>(); // 영창 순서대로 누적된 속성

        public Property MainProperty = Property.None;
        private Property? forcedMainProperty = null; // 설정 시 최빈값 계산보다 우선

        // 효과 해석 전 대기 중인 카드 (CompileSpell에서 일괄 처리)
        private List<PlayableCard> pendingCards = new List<PlayableCard>();
        public void EnqueuePendingCard(PlayableCard card)
        {
            pendingCards.Add(card);
            UsedCardIds.Add(card.Id);
            
            if (card is PlayableCard playableCard)
            {
                AddProperty(playableCard.uiData.property, 1);
            }
        }
        
        /// <summary>
        /// [서버] 주문 확정 시 호출. 메인 속성을 먼저 확정해야 "메인 속성이 X일 때" 같은
        /// 카드 조건을 모든 카드가 동일한 기준으로 판정할 수 있다.
        /// </summary>
        public void CompileSpell(PlayerModel caster, PlayerModel enemy)
        {
            CalculateMainProperty();

            foreach (var card in pendingCards)
            {
                card.ApplyCardEffects(this, caster, enemy);
            }

            // 카드 순서와 무관하게 CommandPriority 순으로 실행되도록 정렬
            Commands.Sort();
        }


        // 카드 효과 해석 중 호출되는 함수들
        public void AddWord(string word) => EvalData.Words.Add(word);
        public void AddCommand(EffectCommand cmd) => Commands.Add(cmd);

        // 속성을 count만큼 누적 (최빈값 계산 시 가중치로 작용)
        public void AddProperty(Property property, int count = 1)
        {
            if (property == Property.None) return;
            
            for (int i = 0; i < count; i++)
            {
                PropertyHistory.Add(property);
            }
        }
        
        // '신비' 카드: 최빈값 계산을 무시하고 메인 속성을 강제 지정
        public void ForceMainProperty(Property property)
        {
            forcedMainProperty = property;
        }

        public void SetPrefix(string prefix)
        {
            EvalData.RequiredPrefix = prefix;
            AddWord(prefix);
        }
        
        public void SetConcept(string concept) => EvalData.Concept = concept;
        
        
        // 영창 UI 표시용 조회
        public string GetConcept() => EvalData.Concept;
        public string GetPrefix() => EvalData.RequiredPrefix;
        public List<string> GetWords() => EvalData.Words;
        public List<PlayableCard> GetCards() => pendingCards;

        // 메인 속성 결정: 강제 지정 > 최빈값 (동률이면 먼저 영창한 속성)
        public void CalculateMainProperty()
        {
            if (forcedMainProperty.HasValue)
            {
                MainProperty = forcedMainProperty.Value;
                return;
            }

            if (PropertyHistory.Count == 0)
            {
                MainProperty = Property.None;
                return;
            }

            Dictionary<Property, int> counts = new Dictionary<Property, int>();
            foreach (var prop in PropertyHistory)
            {
                if (counts.ContainsKey(prop)) counts[prop]++;
                else counts[prop] = 1;
            }
 
            Property mostFrequent = Property.None;
            int maxCount = 0;

            // 영창 순서대로 순회하며 '초과'일 때만 갱신하므로 동률이면 먼저 등장한 속성이 유지됨
            foreach (var prop in PropertyHistory)
            {
                if (counts[prop] > maxCount)
                {
                    mostFrequent = prop;
                    maxCount = counts[prop];
                }
            }

            MainProperty = mostFrequent;
        }

        public string ToJson(string audioFilePath = null)
        {
            if (!string.IsNullOrEmpty(audioFilePath) && File.Exists(audioFilePath))
            {
                byte[] audioBytes = File.ReadAllBytes(audioFilePath);
                EvalData.AudioBase64 = Convert.ToBase64String(audioBytes);
            }

            return EvalData.ToJson();
        }
    }
}
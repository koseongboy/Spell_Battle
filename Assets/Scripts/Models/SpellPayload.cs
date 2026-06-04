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
    public class SpellPayload
    {
        public EvaluationRequest EvalData = new EvaluationRequest();
        public List<EffectCommand> Commands = new List<EffectCommand>();
        public List<int> UsedCardIds = new List<int>();

        public List<Property> PropertyHistory = new List<Property>();
        
        public Property MainProperty = Property.None;
        private Property? forcedMainProperty = null; // '신비' 카드와 같은 강제 속성 변경용

        // 영창 중인 카드들을 보관할 대기열
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
        
        // 영창 종료 시 턴 매니저가 호출할 핵심 메서드
        public void CompileSpell(PlayerModel caster, PlayerModel enemy)
        {
            // 1. 대기열에 쌓인 속성들을 바탕으로 현재 주문의 메인 속성 최종 확정
            CalculateMainProperty();

            // 2. 확정된 메인 속성(MainProperty)을 기준으로 각 카드의 조건 및 효과 해석
            foreach (var card in pendingCards)
            {
                card.ApplyCardEffects(this, caster, enemy);
            }
            
            // 우선순위 기반 일괄 정렬
            Commands.Sort();
        }
        
        
        // 카드가 호출할 함수들
        public void AddWord(string word) => EvalData.Words.Add(word);
        public void AddCommand(EffectCommand cmd) => Commands.Add(cmd);

        // 한 번에 여러 개의 속성을 스택에 넣을 수 있도록 count 매개변수 추가
        public void AddProperty(Property property, int count = 1)
        {
            if (property == Property.None) return;
            
            for (int i = 0; i < count; i++)
            {
                PropertyHistory.Add(property);
            }
        }
        
        // '신비' 카드 대응: 최빈값 계산을 무시하고 메인 속성을 강제 설정
        public void ForceMainProperty(Property property)
        {
            forcedMainProperty = property;
        }
                
        // 턴 매니저가 호출할 코드
        public void SetPrefix(string prefix)
        {
            EvalData.RequiredPrefix = prefix;
            AddWord(prefix);
        }
        
        public void SetConcept(string concept) => EvalData.Concept = concept;
        
        
        // 김명준이 추가
        public string GetConcept() => EvalData.Concept;
        public string GetPrefix() => EvalData.RequiredPrefix;
        public List<string> GetWords() => EvalData.Words;
        public List<PlayableCard> GetCards() => pendingCards;

        // 영창 종료 후
        public void CalculateMainProperty()
        {
            // 1. 강제 설정된 속성이 있다면 우선 적용
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

            // 2. 속성별 등장 횟수를 카운트
            Dictionary<Property, int> counts = new Dictionary<Property, int>();
            foreach (var prop in PropertyHistory)
            {
                if (counts.ContainsKey(prop)) counts[prop]++;
                else counts[prop] = 1;
            }
 
            // 3. 가장 많이 등장한(최빈값) 속성 찾기
            Property mostFrequent = Property.None;
            int maxCount = 0;

            foreach (var prop in PropertyHistory) 
            {
                // PropertyHistory 순서대로 순회하므로, 동률일 경우 '먼저 영창한 카드 속성'이 유지됨
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

            // EvaluationRequest 쪽에 ToJson()이 구현되어 있다고 가정
            return EvalData.ToJson();
        }
    }
}
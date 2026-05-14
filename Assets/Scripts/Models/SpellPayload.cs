using System.Collections.Generic;
using System.ComponentModel.Design;
using Models.PlayerModels;
using Models.EvaluationRequests;
using Models.EffectCommands;
using Cards.CardUIDatas;
using UnityEngine;
using System.IO;
using System;
using Newtonsoft.Json;

namespace Models.SpellPayloads
{
    public class SpellPayload
    {
        public EvaluationRequest EvalData = new EvaluationRequest();
        public List<EffectCommand> Commands = new List<EffectCommand>();

        public List<Property> PropertyHistory = new List<Property>();
        public Property MainProperty = Property.None;

        // 카드가 호출할 함수들
        public void AddWord(string word) => EvalData.Words.Add(word);
        public void AddCommand(EffectCommand cmd) => Commands.Add(cmd);
        public void AddProperty(Property property)
        {
            if (property != Property.None)
            {
                PropertyHistory.Add(property);
            }
        }
                
        //턴 매니져가 호출할 코드
        //영창 생성 파트
        public void SetPrefix(string prefix)
        {
            EvalData.RequiredPrefix = prefix;
            AddWord(prefix);
        }
        public void SetConcept(string concept) => EvalData.Concept = concept;

        //영창 종료 후
        public void CalculateMainProperty()
        {
            if (PropertyHistory.Count == 0)
            {
                MainProperty = Property.None;
                return;
            }

            // 1. 속성별 등장 횟수를 카운트
            Dictionary<Property, int> counts = new Dictionary<Property, int>();
            foreach (var prop in PropertyHistory)
            {
                if (counts.ContainsKey(prop)) counts[prop]++;
                else counts[prop] = 1;
            }
 
            // 2. 가장 많이 등장한(최빈값) 속성 찾기 (todo) 같을 경우 기획적으로 어떻게 처리할 지
            Property mostFrequent = Property.None;
            int maxCount = 0;

            foreach (var kvp in counts)
            {
                if (kvp.Value > maxCount)
                {
                    mostFrequent = kvp.Key;
                    maxCount = kvp.Value;
                }
            }

            MainProperty = mostFrequent;
        }

        public string ToJson(string audioFilePath = null)
        {
            // 1. 음성 파일이 있다면 Base64로 변환하여 필드에 채움
            if (!string.IsNullOrEmpty(audioFilePath) && File.Exists(audioFilePath))
            {
                byte[] audioBytes = File.ReadAllBytes(audioFilePath);
                EvalData.AudioBase64 = Convert.ToBase64String(audioBytes);
            }

            // 2. 전체 객체를 JSON으로 직렬화
            //Formatting.Indented를 넣으면 사람이 보기 좋게 줄바꿈이 됩니다.
            return EvalData.ToJson();
        }
    }
}

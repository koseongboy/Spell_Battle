using System.Collections.Generic;
using System.ComponentModel.Design;
using Models.PlayerModel;
using Cards.CardUIDatas;
using UnityEngine;
using System.IO;
using System;
using Newtonsoft.Json;

namespace Models.SpellPayloads
{
    [System.Serializable]
    public class DamageInfo
    {
        public int TotalDamage = 0;
        public int TotalHeal = 0;
        public int TotalShield = 0;

        public List<StatusData> StatusEffectsToApply = new List<StatusData>();

        public void AddDamage(int amount) => TotalDamage += amount;
        public void AddHeal(int amount) => TotalHeal += amount;
        public void AddShield(int amount) => TotalShield += amount;

        public void AddStatus(StatusType type, int stacks, int duration = 1)
        {
            if (type == StatusType.Ignite)
            {
                StatusEffectsToApply.Add(new StatusData { Type = type, Stacks = stacks, Duration = duration });
                return;
            }

            for (int i = 0; i < StatusEffectsToApply.Count; i++)
            {
                if (StatusEffectsToApply[i].Type == type)
                {
                    var status = StatusEffectsToApply[i];
                    status.Stacks += stacks;
                    if (type == StatusType.Freeze) status.Duration = duration; // 빙결은 지속시간 초기화
                    else if (type == StatusType.ArcaneStack || type == StatusType.Prophecy) status.Duration = -1; // 지속 무한인 애들
                    else status.Duration = Mathf.Max(status.Duration, duration);
                    StatusEffectsToApply[i] = status;
                    return;
                }
            }

            if (type == StatusType.ArcaneStack || type == StatusType.Prophecy) duration = -1;
            StatusEffectsToApply.Add(new StatusData { Type = type, Stacks = stacks, Duration = duration });
        }

    }
    [System.Serializable]
    public class SpellPayload
    {
        public string RequiredPrefix = "";
        public string RolePlayConcept = "";
        public string AudioBase64 = "";

        public List<string> IncantationWords = new List<string>();
        public DamageInfo CasterPayload = new DamageInfo();
        public DamageInfo TargetPayload = new DamageInfo();
        public List<Property> PropertyHistory = new List<Property>();

        public void SetPrefix(string prefix)
        {
            RequiredPrefix = prefix;
            AddIncantation(prefix, Property.None);
        }
        public void SetConcept(string concept) => RolePlayConcept = concept;

        public void AddIncantation(string word, Property property)
        {
            IncantationWords.Add(word);
            PropertyHistory.Add(property);
        }

        public void AddDamage(int amount, bool toOpponent = true)
        {
            if (toOpponent) TargetPayload.AddDamage(amount);
            else CasterPayload.AddDamage(amount);
        }

        public void AddHeal(int amount, bool toOpponent = false)
        {
            if(toOpponent) TargetPayload.AddHeal(amount);
            else CasterPayload.AddHeal(amount);
        }
        public void AddShield(int amount, bool toOpponent = false)
        {
            if(toOpponent) TargetPayload.AddShield(amount);
            else CasterPayload.AddShield(amount);
        }

        public void AddStatus(StatusType type, int stacks,  bool toOpponent , int duration = 1)
        {
            if(toOpponent) TargetPayload.AddStatus(type, stacks, duration);
            else CasterPayload.AddStatus(type, stacks, duration);
        }


        public string ToJson(string audioFilePath = null)
        {
            // 1. 음성 파일이 있다면 Base64로 변환하여 필드에 채움
            if (!string.IsNullOrEmpty(audioFilePath) && File.Exists(audioFilePath))
            {
                byte[] audioBytes = File.ReadAllBytes(audioFilePath);
                AudioBase64 = Convert.ToBase64String(audioBytes);
            }

            // 2. 전체 객체를 JSON으로 직렬화
            //Formatting.Indented를 넣으면 사람이 보기 좋게 줄바꿈이 됩니다.
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
    }
}

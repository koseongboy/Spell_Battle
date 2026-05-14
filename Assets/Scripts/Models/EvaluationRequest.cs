using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Models.EvaluationRequests
{
    public class EvaluationRequest
    {
        public string Concept;
        public string RequiredPrefix;
        public List<string> Words;
        public string AudioBase64;
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}

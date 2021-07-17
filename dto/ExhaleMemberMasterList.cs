using System;
using Newtonsoft.Json;

namespace ExhaleCreativity
{
    public class ExhaleMemberMasterList
    {

        [JsonProperty("values")]
        public object[] Values { get; set; }
    }
}
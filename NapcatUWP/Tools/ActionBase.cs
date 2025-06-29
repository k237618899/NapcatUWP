using Windows.Data.Json;
using Newtonsoft.Json;

namespace NapcatUWP.Tools
{
    internal class ActionBase
    {
        [JsonProperty(PropertyName = "action")]
        public string Action { get; set; }
        [JsonProperty(PropertyName = "params")]
        public JsonObject Params { get; set; }
        [JsonProperty(PropertyName = "echo")]
        public string Echo { get; set; }
    }
}
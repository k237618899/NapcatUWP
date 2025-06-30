using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NapcatUWP.Tools
{
    internal class ResponseEntity
    {
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }

        [JsonProperty(PropertyName = "retcode")]
        public int ReturnCode { get; set; }

        [JsonProperty(PropertyName = "data")] public JToken Data { get; set; } // 修改：從 JObject 改為 JToken

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

        [JsonProperty(PropertyName = "wording")]
        public string Wording { get; set; }

        [JsonProperty(PropertyName = "echo")] public string Echo { get; set; }
    }
}
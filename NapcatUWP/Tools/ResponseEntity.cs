using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NapcatUWP.Tools
{
    class ResponseEntity
    {
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }
        [JsonProperty(PropertyName = "retcode")]
        public int ReturnCode { get; set; }
        [JsonProperty(PropertyName = "data")]
        public JObject Data { get; set; }
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
        [JsonProperty(PropertyName = "wording")]
        public string Wording { get; set; }
        [JsonProperty(PropertyName = "echo")]   
        public string Echo { get; set; }
    }
}

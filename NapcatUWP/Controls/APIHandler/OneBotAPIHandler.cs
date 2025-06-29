using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Data.Json;
using NapcatUWP.Pages;
using NapcatUWP.Tools;
using Newtonsoft.Json;

namespace NapcatUWP.Controls.APIHandler
{
    internal class OneBotAPIHandler
    {
        

        public void IncomingTask(string messages)
        {
           JsonObject json = JsonObject.Parse(messages);
           string echo = json.GetNamedString("echo", "null");
           if ("null" != echo)
           {
               ActionResponseHandler(messages,echo);
           }
           else
           {
               Debug.WriteLine(json.ToString());
           }
        }

        private void ActionResponseHandler(string json,string echo)
        {
            ResponseEntity response= JsonConvert.DeserializeObject<ResponseEntity>(json);
            switch (echo)
            {
                case "login_info":
                    LoginInfoHandler(response);
                    break;
            }
        }

        private void LoginInfoHandler(ResponseEntity response)
        {
            double user_id = response.Data.Value<double>("user_id");
            string nickName = response.Data.Value<string>("nickname");
            
            Debug.WriteLine("response status:"+response.Status+"\r\nLogin ID:"+user_id+"\r\nNickName:"+nickName);
        }
    }
}
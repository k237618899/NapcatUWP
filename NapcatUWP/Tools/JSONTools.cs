using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Newtonsoft.Json;

namespace NapcatUWP.Tools
{
    class JSONTools
    {
        public static string Test()
        {
            ActionBase actionBase = new ActionBase();
            actionBase.Action = "get_login_info";
            actionBase.Params = new JsonObject();
            actionBase.Echo = "123";
            string output = JsonConvert.SerializeObject(actionBase);
            Debug.WriteLine(output);
            return output;
        }

        public static string ActionToJSON(string actionName,JsonObject actionParams,string echo)
        {
            ActionBase actionBase = new ActionBase();
            actionBase.Action = actionName;
            actionBase.Params = actionParams;
            actionBase.Echo = echo;
            string output = JsonConvert.SerializeObject(actionBase);
            Debug.WriteLine(output);
            return output;
        }

    }
}

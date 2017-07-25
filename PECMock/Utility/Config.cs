using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Web.Hosting;
using Newtonsoft.Json.Linq;

namespace PECMock.Utility
{
    public class Config
    {
        public static JToken Read(string configname)
        {
            string path = HostingEnvironment.ApplicationPhysicalPath;
            // remove trailing slash if any
            if (path[path.Length - 1] == Path.DirectorySeparatorChar) path = path.Substring(0, path.Length - 1);
            int lastSeparatorLocation = path.LastIndexOf(Path.DirectorySeparatorChar);
            path = path.Substring(0, lastSeparatorLocation) + Path.DirectorySeparatorChar + "Configs" +
                   path.Substring(lastSeparatorLocation);
            string content = File.ReadAllText(path + Path.DirectorySeparatorChar + configname + ".json");
            var config = JToken.Parse(content);
            return config;
        }
    }
}

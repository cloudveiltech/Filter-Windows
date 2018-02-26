using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CitadelService.Util
{
    public static class ResourceStreams
    {
        public static byte[] Get(string resourceName)
        {
            try
            {
                //var blockedPagePackURI = "CitadelService.Resources.BlockedPage.html";
                using (var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (resourceStream != null && resourceStream.CanRead)
                    {
                        using (TextReader tsr = new StreamReader(resourceStream))
                        {
                            return Encoding.UTF8.GetBytes(tsr.ReadToEnd());
                        }
                    }
                    else
                    {
                        //m_logger.Error("Cannot read from packed block page file.");
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}

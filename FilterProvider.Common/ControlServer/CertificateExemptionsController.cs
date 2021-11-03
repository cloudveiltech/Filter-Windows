using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using FilterProvider.Common.Util;
using System;
using System.Collections.Generic;

namespace FilterProvider.Common.ControlServer
{
    public class CertificateExemptionsController : WebApiController
    {
        private CertificateExemptions exemptions;

        public CertificateExemptionsController(CertificateExemptions exemptions)
        {
          
            this.exemptions = exemptions;
        }

        [Route(HttpVerbs.Get, "/api/exempt/{thumbprint}")]
        public Dictionary<string, int> Exempt(string thumbprint)
        {
            string host = Request.QueryString["host"];
            if (host != null)
            {
                exemptions.TrustCertificate(host, thumbprint);
            }


            var resultDict = new Dictionary<String, int>();
            resultDict.Add("success", 1);
            return resultDict;
        }

        [Route(HttpVerbs.Get, "/api/testme")]
        public string TestMe()
        {
            return "<html><head><title>Test</title></head><body>This is my body</body></html>";
        }
    }
}

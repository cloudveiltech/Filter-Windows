using FilterProvider.Common.Services;
using FilterProvider.Common.Util;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace FilterProvider.Common.ControlServer
{
    public class CertificateExemptionsController : WebApiController
    {
        private CertificateExemptions exemptions;

        public CertificateExemptionsController(CertificateExemptions exemptions, IHttpContext context)
            : base(context)
        {
            this.exemptions = exemptions;
        }

        [WebApiHandler(HttpVerbs.Get, "/api/exempt/{thumbprint}")]
        public Task<bool> Exempt(string thumbprint)
        {
            string host = this.QueryString("host");

            if(host != null)
            {
                exemptions.TrustCertificate(host, thumbprint);
            }

            return this.JsonResponseAsync("{ \"success\": 1 }");
        }

        [WebApiHandler(HttpVerbs.Get, "/api/testme")]
        public Task<bool> TestMe()
        {
            return this.HtmlResponseAsync("<html><head><title>Test</title></head><body>This is my body</body></html>");
        }
    }
}

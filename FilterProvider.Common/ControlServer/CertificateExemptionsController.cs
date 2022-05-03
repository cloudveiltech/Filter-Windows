using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using FilterProvider.Common.Util;
using System.Collections.Specialized;
using System.Threading.Tasks;
namespace FilterProvider.Common.ControlServer
{
    public class CertificateExemptionsController : WebApiController
    {
        private CertificateExemptions exemptions;

        public CertificateExemptionsController(CertificateExemptions exemptions)
        {          
            this.exemptions = exemptions;
        }

        public struct SuccessResponse
        {
            public int success;
        }

        [Route(HttpVerbs.Get, "/api/exempt/{thumbprint}")]
        public SuccessResponse Exempt(string thumbprint, [QueryData] NameValueCollection parameters)
        {
            string host = parameters["host"];
            if(host != null)
            {
                exemptions.TrustCertificate(host, thumbprint);
            }

            return new SuccessResponse { success = 1 };
        }

        [Route(HttpVerbs.Get, "/api/testme")]
        public string TestMe()
        {
            return "<html><head><title>Test</title></head><body>This is my body</body></html>";
        }
    }
}

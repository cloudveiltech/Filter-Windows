using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace CitadelService.Util
{
    public interface ICertificateExemptions
    {
        void TrustCertificate(string host, string certHash);

        void AddExemptionRequest(HttpWebRequest request, X509Certificate certificate);

        bool IsExempted(HttpWebRequest request, X509Certificate certificate);
    }
}

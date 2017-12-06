using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Te.Citadel.Testing
{
    public enum FilterTest {
        GoogleSafeSearchTest,
        BingSafeSearchTest,
        YoutubeSafeSearchTest,
        AllTestsCompleted,
        ExceptionOccurred
    }

    public delegate void FilterTestResultHandler(FilterTest test, bool passed);

    class FilterTesting
    {
        /// <summary>
        /// Used by filter test to determine whether the filter is working or not.
        /// This string is embedded in the BlockPage.
        /// </summary>
        public const string filterMagicString = "filtering:ok-J1ynoE8POR";
        public const string GoogleSafeSearchIp = "216.239.38.120";

        public event FilterTestResultHandler OnFilterTestResult;

        public void TestFilter()
        {
            // Load 5 known bad sites and see if they contain the magic string.
            // redtube.com - porn
            // 777.com - gambling
            // bestgore.com
        }

        private string getIpFromRequest(string url)
        {
            IPEndPoint endPoint = null;

            var webRequest = WebRequest.CreateHttp(url);
            webRequest.KeepAlive = false;

            webRequest.ServicePoint.BindIPEndPointDelegate = delegate (ServicePoint servicePoint, IPEndPoint remoteEndPoint, int retryCount)
            {
                endPoint = remoteEndPoint;
                return null;
            };

            HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse();
            response.Close();

            return endPoint.Address.ToString();

        }

        public void TestDNS()
        {
            int testsPassed = 0;
            int testsFailed = 0;

            try
            {
                IPHostEntry entry = Dns.GetHostEntry("www.google.com");

                WebClient client = new WebClient();

                string ip = this.getIpFromRequest("https://www.google.com");
                if (ip == GoogleSafeSearchIp)
                {
                    OnFilterTestResult?.Invoke(FilterTest.GoogleSafeSearchTest, true);
                    testsPassed++;
                }
                else
                {
                    OnFilterTestResult?.Invoke(FilterTest.GoogleSafeSearchTest, false);
                    testsFailed++;
                }

                ip = this.getIpFromRequest("https://www.bing.com");
                string strictIp = this.getIpFromRequest("https://strict.bing.com");

                if(ip == strictIp)
                {
                    OnFilterTestResult?.Invoke(FilterTest.BingSafeSearchTest, true);
                    testsPassed++;
                }
                else
                {
                    OnFilterTestResult?.Invoke(FilterTest.BingSafeSearchTest, false);
                    testsFailed++;
                }

                ip = this.getIpFromRequest("https://www.youtube.com");
                strictIp = this.getIpFromRequest("https://restrict.youtube.com");

                if(ip == strictIp)
                {
                    OnFilterTestResult?.Invoke(FilterTest.YoutubeSafeSearchTest, true);
                    testsPassed++;
                }
                else
                {
                    OnFilterTestResult?.Invoke(FilterTest.YoutubeSafeSearchTest, false);
                    testsFailed++;
                }

                OnFilterTestResult?.Invoke(FilterTest.AllTestsCompleted, true);
            }
            catch(Exception ex)
            {
                OnFilterTestResult?.Invoke(FilterTest.ExceptionOccurred, false);
            }
        }

        public IPEndPoint BindIPEndPoint1(ServicePoint servicePoint, IPEndPoint remoteEndPoint, int retryCount)
        {
            string ip = remoteEndPoint.ToString();
            return remoteEndPoint;
        }
    }
}

using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Gui.CloudVeil.Testing
{
    public enum FilterTest {
        BlockingTest = 1,
        GoogleSafeSearchTest,
        BingSafeSearchTest,
        YoutubeSafeSearchTest,
        AllTestsCompleted,
        ExceptionOccurred,
        PixabaySafeSearchTest,
        DnsFilterTest
    }

    public class DiagnosticsEntry
    {
        public DiagnosticsEntry()
        {

        }

        public DiagnosticsEntry(FilterTest test, bool passed, string details)
        {
            Test = test;
            Passed = passed;
            Details = details;
        }

        public FilterTest Test { get; set; }
        public bool Passed { get; set; }
        public string Details { get; set; }

        public Exception Exception { get; set; }
    }

    public delegate void FilterTestResultHandler(DiagnosticsEntry entry);

    /// <summary>
    /// FilterServiceProvider.exe may not be running when we want to test the filter, so we put the filter test code into the GUI.
    /// 
    /// This class tests the filter and the computer's DNS settings for safe-search.
    /// </summary>
    public class FilterTesting
    {
        /// <summary>
        /// Used by filter test to determine whether the filter is working or not.
        /// This string is embedded in the BlockPage.
        /// </summary>
        public const string filterMagicString = "filtering:ok-J1ynoE8POR";
        public const string GoogleSafeSearchIp = "216.239.38.120";
        public const string GoogleSafeSearchDomain = "forcesafesearch.google.com";

        public event FilterTestResultHandler OnFilterTestResult;

        /*public void TestInternet()
        {
            try
            {
                IPHostEntry ipEntry = Dns.GetHostEntry("connectivitycheck.cloudveil.org");
            }
            catch(Exception ex)
            {

            }

            try
            {
                var webRequest = WebRequest.CreateHttp("http://connectivitycheck.cloudveil.org/ncsi.txt");]

                HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse();

                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {

                }
            }
        }*/

        public void TestFilter()
        {
            try
            {
                var webRequest = WebRequest.CreateHttp("http://test.cloudveil.org");

                HttpWebResponse response = (HttpWebResponse)webRequest.GetResponse();

                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string ret = reader.ReadToEnd();
                    if (ret.Contains(filterMagicString))
                    {
                        OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.BlockingTest, true, "Your filter is up and running!"));
                    }
                    else
                    {
                        OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.BlockingTest, false, "Your filter did not block the test site. Please reboot to see if that fixes the problem."));
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Response == null)
                {
                    OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.BlockingTest, false, "No response detected from test site. Check your internet connection and try again.")
                    {
                        Exception = ex
                    });
                }
                else
                {
                    OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.BlockingTest, false, "Unexpected error occurred. " + ex.Message + ". Check your internet connection and try again later.")
                    {
                        Exception = ex
                    });
                }
            }
            catch (Exception ex)
            {
                // FIXME: Any exception needs to get logged for transmission to the cloudveil server.
                OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.BlockingTest, false, "Unexpected error occurred. " + ex.Message + ". Please contact support.")
                {
                    Exception = ex
                });
            }
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

        private bool doUrlIpsMatch(string url1, string url2, out string ip1, out string ip2)
        {
            string ip = this.getIpFromRequest(url1);
            string strictIp = null;

            Uri uri = new Uri(url2);
            url2 = uri.Authority;

            IPHostEntry strictIpEntry = Dns.GetHostEntry(url2);

            if (strictIpEntry != null)
            {
                strictIp = strictIpEntry.AddressList[0].ToString();
            }
            else
            {
                ip1 = ip;
                ip2 = null;
                return false;
            }

            ip1 = ip;
            ip2 = strictIp;

            return ip == strictIp;
        }

        public void TestDNS()
        {
            try
            {
                string ip1, ip2, details;
                bool result;

                result = doUrlIpsMatch("http://testdns.cloudveil.org", "http://block.cloudveil.org", out ip1, out ip2);
                details = result ? "DNS filtering is currently active on your computer." : "DNS filtering is not currently active on your computer.";

                OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.DnsFilterTest, result, details));
            }
            catch (Exception ex)
            {
                OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.ExceptionOccurred, false, ex.ToString()) { Exception = ex });
            }
        }

        public void TestDNSSafeSearch()
        {           
            int testsPassed = 0;
            int testsFailed = 0;

            try
            {
                string ip1, ip2, ip3, details;
                bool result;

                ip3 = "";

                result = doUrlIpsMatch("https://www.google.com", "https://forcesafesearch.google.com", out ip1, out ip2);
                details = string.Format("IP {0} {1} IP {2}", ip1, result ? "matches" : "does not match expected", ip2);

                OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.GoogleSafeSearchTest, result, details));

                result = doUrlIpsMatch("https://www.bing.com", "https://strict.bing.com", out ip1, out ip2);
                details = string.Format("IP {0} {1} IP {2}", ip1, result ? "matches" : "does not match expected", ip2);

                OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.BingSafeSearchTest, result, details));

                result = doUrlIpsMatch("https://www.youtube.com", "https://restrict.youtube.com", out ip1, out ip2);
                if(result)
                {
                    details = string.Format("IP {0} {1} IP {2}, type: restrict", ip1, "matches", ip2);
                }
                else
                {
                    result = doUrlIpsMatch("https://www.youtube.com", "https://restrictmoderate.youtube.com", out ip1, out ip3);
                    details = string.Format("IP {0} {1} IP {2}, type: restrict/moderate", ip1, "matches", ip3);
                } 
                if(!result)
                {
                    details = string.Format("IP {0} {1} IP {2} or {3}", ip1, "does not match expected", ip2, ip3);
                }
                
                OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.YoutubeSafeSearchTest, result, details));
                
                OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.AllTestsCompleted, true, ""));
            }
            catch(Exception ex)
            {
                OnFilterTestResult?.Invoke(new DiagnosticsEntry(FilterTest.ExceptionOccurred, false, ex.ToString()) { Exception = ex });
            }
        }

        public IPEndPoint BindIPEndPoint1(ServicePoint servicePoint, IPEndPoint remoteEndPoint, int retryCount)
        {
            string ip = remoteEndPoint.ToString();
            return remoteEndPoint;
        }
    }
}

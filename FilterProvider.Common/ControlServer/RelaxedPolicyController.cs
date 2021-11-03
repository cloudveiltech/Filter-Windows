using FilterProvider.Common.Util;
using System;
using System.Threading.Tasks;
using Filter.Platform.Common.Util;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using EmbedIO;

namespace FilterProvider.Common.ControlServer
{
    class RelaxedPolicyPostBody
    {
        public string passcode { get; set; }
    }

    public class RelaxedPolicyPostResponse
    {
        public string message { get; set; }
    }

    public class RelaxedPolicyController : WebApiController
    {
        private RelaxedPolicy relaxedPolicy;

        public RelaxedPolicyController(RelaxedPolicy relaxedPolicy)
        {
            this.relaxedPolicy = relaxedPolicy;
        }

        [Route(HttpVerbs.Get, "/api/relaxedpolicy")]
        public BypassInformation GetRelaxedPolicyInformation()
        {
            BypassInformation info = relaxedPolicy.GetInfo();
            return info;
        }

        /// <summary>
        /// Requests the relaxed policy.
        /// 
        /// 'passcode' is the only property which must be passed. passcode may be null, or a string.
        /// </summary>
        /// <returns></returns>
        [Route(HttpVerbs.Post, "/api/relaxedpolicy")]
        public async Task<RelaxedPolicyPostResponse> RequestRelaxedPolicy()
        {
            try
            {
                var data = await HttpContext.GetRequestDataAsync<RelaxedPolicyPostBody>();

                string bypassNotification = null;
                bool ret = relaxedPolicy.RequestRelaxedPolicy(data.passcode, out bypassNotification);

                if (ret)
                {
                    Response.StatusCode = 200;
                }
                else
                {
                    Response.StatusCode = 401;
                }

                return new RelaxedPolicyPostResponse() { message = bypassNotification };
            }
            catch(Exception ex)
            {
                LoggerUtil.GetAppWideLogger().Error($"Exception occurred while request relaxed policy: {ex}");
                return new RelaxedPolicyPostResponse() { message = "Error occurred while requesting relaxed policy. Try again later." };
            }
        }
    }
}

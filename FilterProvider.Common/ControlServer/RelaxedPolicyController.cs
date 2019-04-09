using FilterProvider.Common.Configuration;
using FilterProvider.Common.Services;
using FilterProvider.Common.Util;
using Citadel.IPC.Messages;
using Citadel.IPC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;
using Unosquare.Labs.EmbedIO.Constants;
using Unosquare.Labs.EmbedIO.Modules;

namespace FilterProvider.Common.ControlServer
{
    class RelaxedPolicyPostBody
    {
        public string passcode { get; set; }
    }

    class RelaxedPolicyPostResponse
    {
        public string message { get; set; }
    }

    public class RelaxedPolicyController : WebApiController
    {
        private RelaxedPolicy relaxedPolicy;

        public RelaxedPolicyController(RelaxedPolicy relaxedPolicy, IHttpContext context) : base(context)
        {
            this.relaxedPolicy = relaxedPolicy;
        }

        [WebApiHandler(HttpVerbs.Get, "/api/relaxedpolicy")]
        public Task<bool> GetRelaxedPolicyInformation()
        {
            BypassInformation info = relaxedPolicy.GetInfo();
            return this.JsonResponseAsync(info);
        }

        /// <summary>
        /// Requests the relaxed policy.
        /// 
        /// 'passcode' is the only property which must be passed. passcode may be null, or a string.
        /// </summary>
        /// <returns></returns>
        [WebApiHandler(HttpVerbs.Post, "/api/relaxedpolicy")]
        public async Task<bool> RequestRelaxedPolicy()
        {
            var data = await this.ParseJsonAsync<RelaxedPolicyPostBody>();

            string bypassNotification = null;
            bool ret = relaxedPolicy.RequestRelaxedPolicy(data.passcode, out bypassNotification);

            if(ret)
            {
                Response.StatusCode = 200;
            }
            else
            {
                Response.StatusCode = 401;
            }

            return await this.JsonResponseAsync(new RelaxedPolicyPostResponse() { message = bypassNotification });
        }
    }
}

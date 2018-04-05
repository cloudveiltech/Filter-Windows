using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Citadel.IPC.Messages;
using Citadel.Core.Windows.Util;
using CitadelService.Data.Filtering;
using Newtonsoft.Json;
using System.Net;

namespace CitadelService.Util
{
    public class Accountability
    {
        public Accountability()
        {
            ReportList = new List<BlockInfo>();
        }

        public List<BlockInfo> ReportList { get; set; }

        public void AddBlockAction(BlockType cause, Uri requestUri, string categoryNameString, string matchingRule)
        {
            LoggerUtil.GetAppWideLogger().Info($"Sending block action to server: cause={cause}, requestUri={requestUri}, categoryNameString={categoryNameString}, matchingRule={matchingRule}");
            BlockInfo blockInfo = new BlockInfo(cause, requestUri, categoryNameString, matchingRule);

            //ReportList.Add(blockInfo);

            string json = JsonConvert.SerializeObject(blockInfo);
            byte[] formData = Encoding.UTF8.GetBytes(json);

            HttpStatusCode statusCode;

            WebServiceUtil.Default.SendResource(ServiceResource.AccountabilityNotify, formData, out statusCode);
        }
    }
}

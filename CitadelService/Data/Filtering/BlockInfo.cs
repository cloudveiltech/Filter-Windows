using Citadel.IPC.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitadelService.Data.Filtering
{
    public class BlockInfo
    {
        public BlockInfo(BlockType cause, Uri requestUri, string categoryNameString, string matchingRule)
        {
            Cause = cause;
            RequestUri = requestUri.ToString();
            CategoryNameString = categoryNameString;
            MatchingRule = matchingRule;
        }


        public BlockType Cause { get; set; }
        public string RequestUri { get; set; }
        public string CategoryNameString { get; set; }
        public string MatchingRule { get; set; }
    }
}

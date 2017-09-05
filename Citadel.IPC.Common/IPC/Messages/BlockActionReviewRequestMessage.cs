using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Citadel.IPC.Messages
{
    [Serializable]
    public class BlockActionReviewRequestMessage : ClientOnlyMessage
    {
        public string CategoryName
        {
            get;
            private set;
        }

        public string FullRequestUrl
        {
            get;
            private set;
        }

        public BlockActionReviewRequestMessage(string categoryName, string fullRequestUrl)
        {
            CategoryName = categoryName;
            FullRequestUrl = fullRequestUrl;
        }
    }
}

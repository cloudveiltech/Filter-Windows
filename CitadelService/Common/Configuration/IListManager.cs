using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CitadelService.Common.Configuration
{
    public interface IListManager
    {
        void LoadListFromServer(string listName);
        void LoadListFromDisk(string listName);

        bool VerifyListContentsAgainstServer(string listName);
    }
}

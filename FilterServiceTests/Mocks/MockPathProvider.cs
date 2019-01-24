using Filter.Platform.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FilterServiceTests.Mocks
{
    public class MockPathProvider : IPathProvider
    {
        public string ApplicationDataFolder => "." + Path.DirectorySeparatorChar + "unit-tests";

        public string GetPath(params string[] pathParts)
        {
            return Path.Combine(ApplicationDataFolder, Path.Combine(pathParts));
        }
    }
}

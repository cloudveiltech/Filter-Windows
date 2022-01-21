using FilterProvider.Common.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CloudVeilService.Services
{
    public class VersionProvider : IVersionProvider
    {
        public Version GetApplicationVersion()
        {
            Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Version version = AssemblyName.GetAssemblyName(assembly.Location).Version;
            return version;
        }
    }
}

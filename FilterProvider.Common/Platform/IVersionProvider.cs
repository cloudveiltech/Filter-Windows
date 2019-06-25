using System;
using System.Collections.Generic;
using System.Text;

namespace FilterProvider.Common.Platform
{
    public interface IVersionProvider
    {
        Version GetApplicationVersion();
    }
}

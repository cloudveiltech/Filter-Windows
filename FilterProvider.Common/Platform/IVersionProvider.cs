using System;

namespace FilterProvider.Common.Platform
{
    public interface IVersionProvider
    {
        Version GetApplicationVersion();
    }
}

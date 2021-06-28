namespace InstallerCheckPackageCache
{
    class Program
    {
        static void Main(string[] args)
        {
            var checker = new InstallerCacheChecker();
            checker.CheckAndRestoreCache();
        }

    }
}

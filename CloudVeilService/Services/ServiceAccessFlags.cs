
namespace CloudVeilService.Services
{
    public enum ServiceAccessFlags : uint
    {
        QueryConfig = 1,
        ChangeConfig = 2,
        QueryStatus = 4,
        EnumerateDependents = 8,
        Start = 16,
        Stop = 32,
        PauseContinue = 64,
        Interrogate = 128,
        UserDefinedControl = 256,
        Delete = 65536,
        ReadControl = 131072,
        WriteDac = 262144,
        WriteOwner = 524288,
        Synchronize = 1048576,
        AccessSystemSecurity = 16777216,
        GenericAll = 268435456,
        GenericExecute = 536870912,
        GenericWrite = 1073741824,
        GenericRead = 2147483648
    }
}

using EntropyTag.Application;

namespace EntropyTag.Infrastructure
{
    public static class BuildMetadata
    {
        public static string Describe(StartupState startupState)
        {
            return $"{startupState.CompanyName}/{startupState.ProductName} {UnityEngine.Application.version}";
        }
    }
}

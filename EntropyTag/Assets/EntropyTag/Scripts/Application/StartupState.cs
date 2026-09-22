using EntropyTag.Domain;

namespace EntropyTag.Application
{
    public sealed class StartupState
    {
        public StartupState(string companyName, string productName)
        {
            CompanyName = companyName;
            ProductName = productName;
        }

        public string CompanyName { get; }

        public string ProductName { get; }

        public static StartupState CreateDefault()
        {
            return new StartupState(GameIdentity.CompanyName, GameIdentity.ProductName);
        }
    }
}

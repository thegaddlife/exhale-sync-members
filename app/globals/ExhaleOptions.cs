
namespace ExhaleCreativity
{
    public class ExhaleOptions
    {
        public const string SecureSettings = "SecureSettings";

        public string GoogleApiKey { get; set; }

        public string FormSheetId { get; set; }

        public string StripeApiKey { get; set; }

        public string AzureWebJobsStorage { get; set; }

        public string AlgoliaAppId { get; set; }

        public string AlgoliaApiKey { get; set; }
    }
}

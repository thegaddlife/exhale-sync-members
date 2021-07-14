using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Algolia.Search.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExhaleCreativity
{
    public class ExhaleIndexService : IExhaleIndexService
    {
        private readonly ILogger<ExhaleIndexService> _logger;
        private readonly SearchClient _client;

        public ExhaleIndexService(ILogger<ExhaleIndexService> logger, IOptions<ExhaleOptions> options)
        {
            _logger = logger;

            _client = new SearchClient(options.Value.AlgoliaAppId, options.Value.AlgoliaApiKey);
        }

        public async Task UpdateSearchIndexAsync(string indexName, IEnumerable<ExhaleMember> members)
        {
            _logger.LogInformation($"Updating algolia with {members.Count()} members");

            // TODO: we may want to create a new attribute so we can limit what we store in the index

            SearchIndex index = _client.InitIndex(indexName);
            var response = await index.ReplaceAllObjectsAsync(members);

            //TODO: do something with response here
        }
    }
}
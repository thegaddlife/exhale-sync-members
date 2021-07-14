using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExhaleCreativity
{
    internal class ExhaleSheetsService : IExhaleSheetsService
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<ExhaleSheetsService> _logger;
        private readonly string _apiKey;

        public ExhaleSheetsService(IHttpClientFactory clientFactory, ILogger<ExhaleSheetsService> logger, IOptions<ExhaleOptions> options)
        {
            _clientFactory = clientFactory;
            _logger = logger;
            _apiKey = options.Value.GoogleApiKey;
        }

        public async Task<T> GetSheetDataAsync<T>(string formId, string worksheet = Constants.MainSheet)
        {
            try
            {
                var httpClient = _clientFactory.CreateClient();
                var response = await httpClient.GetAsync($"https://sheets.googleapis.com/v4/spreadsheets/{formId}/values/{worksheet}?key={_apiKey}");
                if (response.IsSuccessStatusCode == false)
                {
                    throw new Exception($"Unable to read form {formId} data; Status {response.StatusCode}");
                }
                return await response.Content.ReadAsAsync<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to get Sheets data", ex);
                throw;
            }
        }
    }
}
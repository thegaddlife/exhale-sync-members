using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ExhaleCreativity
{
    public class ExhaleBlobService : IExhaleBlobService
    {
        readonly BlobContainerClient _containerClient;
        private readonly ILogger<ExhaleBlobService> _logger;

        public ExhaleBlobService(ILogger<ExhaleBlobService> logger, IOptions<ExhaleOptions> options)
        {
            BlobServiceClient blobServiceClient = new(options.Value.AzureStorageConnectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(Constants.MembersContainerName);
            _logger = logger;
        }

        public async Task<List<T>> GetBlobAsListAsync<T>(string blobName)
        {
            BlobClient blobClient = _containerClient.GetBlobClient(blobName);
            _logger.LogInformation($"Getting blob {blobName}");

            using var stream = new MemoryStream();
            var blobGrouped = await blobClient.DownloadToAsync(stream);
            var serialised = default(string);
            stream.Position = 0;
            using var sr = new StreamReader(stream);
            serialised = await sr.ReadToEndAsync();
            var groups = JsonConvert.DeserializeObject<List<T>>(serialised);

            return groups;
        }

        public async Task UploadAsync<T>(string blobName, List<T> data)
        {
            BlobClient blobClient = _containerClient.GetBlobClient(blobName);

            var json = JsonConvert.SerializeObject(data);
            using Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            await blobClient.UploadAsync(stream, overwrite: true);
        }
    }
}
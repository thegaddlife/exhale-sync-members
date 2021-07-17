using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExhaleCreativity
{
    public interface IExhaleBlobService
    {
        Task<List<T>> GetBlobAsListAsync<T>(string blobName);
        Task UploadAsync<T>(string blobName, List<T> data);

    }
}
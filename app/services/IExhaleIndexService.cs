using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExhaleCreativity
{
    public interface IExhaleIndexService
    {
        Task UpdateSearchIndexAsync(string indexName, IEnumerable<ExhaleMember> members);
    }
}
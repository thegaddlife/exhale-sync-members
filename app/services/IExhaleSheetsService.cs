using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExhaleCreativity
{
    public interface IExhaleSheetsService
    {
        Task<T> GetSheetDataAsync<T>(string formId, string worksheet = Constants.MainSheet);
    }
}
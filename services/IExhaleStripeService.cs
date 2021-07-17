using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExhaleCreativity
{

    public interface IExhaleStripeService
    {
        Task<List<ExhaleMember>> GetExhaleMembersAsync();
    }
}
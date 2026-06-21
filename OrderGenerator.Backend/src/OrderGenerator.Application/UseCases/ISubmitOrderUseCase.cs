using System.Threading.Tasks;
using OrderGenerator.Application.DTOs;
using OrderGenerator.Application.Services;

namespace OrderGenerator.Application.UseCases
{
    public interface ISubmitOrderUseCase
    {
        Task<OrderSubmissionResult> SubmitOrderAsync(SubmitOrder dto);
    }
}

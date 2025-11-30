using APLabApp.BLL.DeleteRequests;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace APLabApp.Bll.Services
{
    public interface IDeleteRequestService
    {
        Task<int> CreateAsync(CreateDeleteRequestDto dto, CancellationToken ct);
        Task<List<DeleteRequestListItemDto>> GetAllAsync(CancellationToken ct);
        Task ApproveAsync(int deleteRequestId, CancellationToken ct);
        Task RejectAsync(int deleteRequestId, CancellationToken ct);
    }
}

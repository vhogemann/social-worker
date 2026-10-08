using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Accounts.Tokens;

public interface ITokenRefresher
{
    string Platform { get; }
    Task<bool> RefreshAccountTokenAsync(Account account, CancellationToken ct = default);
}

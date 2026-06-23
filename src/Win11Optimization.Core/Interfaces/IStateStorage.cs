using System.Threading;
using System.Threading.Tasks;

namespace Win11Optimization.Core.Interfaces;
public interface IStateStorage
{
    Task SetStateAsync<T>(string optimizationId, string key, T value, CancellationToken ct = default);
    Task<T?> GetStateAsync<T>(string optimizationId, string key, CancellationToken ct = default);
    Task ClearStateAsync(string optimizationId, string key, CancellationToken ct = default);
}

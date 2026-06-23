using Win11Optimization.Core.Models;

namespace Win11Optimization.Core.Interfaces;
public interface IOptimization
{
    OptimizationInfo Info { get; }
    Task<bool> IsAppliedAsync(CancellationToken ct = default);
    Task<OptimizationResult> ApplyAsync(CancellationToken ct = default);
    Task<OptimizationResult> RollbackAsync(CancellationToken ct = default);
}

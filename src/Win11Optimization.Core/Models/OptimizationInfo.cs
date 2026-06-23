namespace Win11Optimization.Core.Models;
public sealed record OptimizationInfo(
    string Id,
    string Name,
    string Description,
    OptimizationCategory Category,
    RiskLevel RiskLevel
);

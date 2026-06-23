namespace Win11Optimization.Core.Models;
public sealed class OptimizationResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public IReadOnlyList<string> Warnings { get; }

    private OptimizationResult(bool isSuccess, string? errorMessage, IReadOnlyList<string>? warnings)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Warnings = warnings ?? [];
    }
    public static OptimizationResult Success()
        => new(true, null, null);
    public static OptimizationResult Success(IReadOnlyList<string> warnings)
        => new(true, null, warnings);
    public static OptimizationResult Failure(string errorMessage)
        => new(false, errorMessage, null);
    public static OptimizationResult Failure(string errorMessage, IReadOnlyList<string> warnings)
        => new(false, errorMessage, warnings);

    public override string ToString()
        => IsSuccess
            ? $"Success (warnings: {Warnings.Count})"
            : $"Failure: {ErrorMessage}";
}

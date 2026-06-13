namespace Win11Optimization.Core.Models;

/// <summary>
/// Результат выполнения оптимизации — паттерн Result вместо исключений.
/// Поддерживает частичный успех через предупреждения (Warnings).
/// </summary>
public sealed class OptimizationResult
{
    /// <summary>Операция завершилась успешно (возможно, с предупреждениями).</summary>
    public bool IsSuccess { get; }

    /// <summary>Сообщение об ошибке при неуспешном выполнении.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Предупреждения — несущественные проблемы, не блокирующие операцию.</summary>
    public IReadOnlyList<string> Warnings { get; }

    private OptimizationResult(bool isSuccess, string? errorMessage, IReadOnlyList<string>? warnings)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Warnings = warnings ?? [];
    }

    /// <summary>Создаёт успешный результат без предупреждений.</summary>
    public static OptimizationResult Success()
        => new(true, null, null);

    /// <summary>Создаёт успешный результат с предупреждениями.</summary>
    public static OptimizationResult Success(IReadOnlyList<string> warnings)
        => new(true, null, warnings);

    /// <summary>Создаёт результат с ошибкой.</summary>
    public static OptimizationResult Failure(string errorMessage)
        => new(false, errorMessage, null);

    /// <summary>Создаёт результат с ошибкой и дополнительными предупреждениями.</summary>
    public static OptimizationResult Failure(string errorMessage, IReadOnlyList<string> warnings)
        => new(false, errorMessage, warnings);

    public override string ToString()
        => IsSuccess
            ? $"Success (warnings: {Warnings.Count})"
            : $"Failure: {ErrorMessage}";
}

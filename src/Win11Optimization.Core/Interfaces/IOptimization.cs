using Win11Optimization.Core.Models;

namespace Win11Optimization.Core.Interfaces;

/// <summary>
/// Единый контракт для всех оптимизаций системы.
/// 
/// Каждая оптимизация реализует этот интерфейс и регистрируется
/// в DI-контейнере как IEnumerable&lt;IOptimization&gt;.
/// 
/// Жизненный цикл: IsAppliedAsync → ApplyAsync → RollbackAsync.
/// Бэкап создаётся внутри реализации через инъекцию IBackupManager.
/// </summary>
public interface IOptimization
{
    /// <summary>
    /// Метаданные оптимизации: имя, описание, категория, риск.
    /// Должно возвращать одни и те же данные при каждом вызове.
    /// </summary>
    OptimizationInfo Info { get; }

    /// <summary>
    /// Проверяет, применена ли оптимизация в текущей системе.
    /// Не вносит изменений — только чтение.
    /// </summary>
    Task<bool> IsAppliedAsync(CancellationToken ct = default);

    /// <summary>
    /// Применяет оптимизацию. Реализация ОБЯЗАНА создать бэкап
    /// через IBackupManager до внесения изменений.
    /// </summary>
    /// <returns>Результат с информацией об успехе/ошибке.</returns>
    Task<OptimizationResult> ApplyAsync(CancellationToken ct = default);

    /// <summary>
    /// Откатывает оптимизацию к исходному состоянию через бэкап.
    /// </summary>
    /// <returns>Результат с информацией об успехе/ошибке.</returns>
    Task<OptimizationResult> RollbackAsync(CancellationToken ct = default);
}

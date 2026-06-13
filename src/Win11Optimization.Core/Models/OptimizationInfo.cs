namespace Win11Optimization.Core.Models;

/// <summary>
/// Метаданные оптимизации — идентификация, описание, категория и уровень риска.
/// Используется как декларативное описание без бизнес-логики.
/// </summary>
/// <param name="Id">Уникальный идентификатор (например, "net.nagle_disable").</param>
/// <param name="Name">Человекочитаемое имя (например, "Отключение Nagle's Algorithm").</param>
/// <param name="Description">Подробное описание: что делает оптимизация и зачем.</param>
/// <param name="Category">Категория оптимизации для группировки в UI.</param>
/// <param name="RiskLevel">Уровень риска для предупреждения пользователя.</param>
public sealed record OptimizationInfo(
    string Id,
    string Name,
    string Description,
    OptimizationCategory Category,
    RiskLevel RiskLevel
);

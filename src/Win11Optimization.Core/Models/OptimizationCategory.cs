namespace Win11Optimization.Core.Models;

/// <summary>
/// Категория оптимизации — группирует оптимизации в логические разделы.
/// </summary>
public enum OptimizationCategory
{
    /// <summary>Сетевой стек: TCP/IP, Nagle, Throttling.</summary>
    Network,

    /// <summary>Приватность: телеметрия, трекинг, Cortana.</summary>
    Privacy,

    /// <summary>Производительность: планировщик, память, диск.</summary>
    Performance,

    /// <summary>Службы: отключение ненужных фоновых сервисов.</summary>
    Services,

    /// <summary>Визуальные эффекты: анимации, прозрачность, тени.</summary>
    Visual,

    /// <summary>Энергопотребление: схемы питания, CPU parking.</summary>
    Power,

    /// <summary>Игры: Game Mode, GPU scheduling, input lag.</summary>
    Gaming,

    /// <summary>Система: базовые оптимизации, службы, питание, UI.</summary>
    System
}

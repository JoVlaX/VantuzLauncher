namespace Vantuz.Core;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// ARM007: QuantizedNode - базовый класс для квантованного выполнения плагинов.
/// Заменяет free-form async Task методы на строго контролируемые кванты выполнения.
/// Согласно .traerules:96-98 и .traerules:169-174.
/// </summary>
public abstract class QuantizedNode : IAsyncDisposable
{
    /// <summary>
    /// Уникальное имя узла для роутинга
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Выполняет один квант работы.
    /// Метод ДОЛЖЕН завершиться в течение выделенного кванта времени.
    /// Если работа не завершена - возвращает Yield для продолжения.
    /// </summary>
    /// <param name="context">Контекст выполнения с оставшимся временем кванта</param>
    /// <param name="stepConfig">Конфигурация шага pipeline</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>Результат кванта: Complete, Yield или Error</returns>
    public abstract Task<QuantumResult> ExecuteQuantumAsync(
        IQuantumContext context,
        JsonElement stepConfig,
        CancellationToken ct);

    /// <summary>
    /// Инициализация узла перед первым квантом.
    /// Вызывается один раз перед началом выполнения.
    /// </summary>
    public virtual ValueTask InitializeAsync(JsonElement stepConfig, CancellationToken ct)
    {
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Очистка ресурсов при завершении работы узла.
    /// </summary>
    public virtual ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Контекст выполнения кванта с контролем времени и backpressure
/// </summary>
public interface IQuantumContext
{
    /// <summary>
    /// Оставшееся время текущего кванта
    /// </summary>
    TimeSpan RemainingQuantum { get; }

    /// <summary>
    /// Полный размер кванта (для расчёта процентов)
    /// </summary>
    TimeSpan TotalQuantum { get; }

    /// <summary>
    /// CancellationToken для отмены операции
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Репортёр для информирования о прогрессе
    /// </summary>
    IStatusReporter Reporter { get; }

    /// <summary>
    /// Payload данные только для чтения (Query)
    /// </summary>
    IReadOnlyPayload Payload { get; }

    /// <summary>
    /// Мутации для записи результатов (Command)
    /// </summary>
    ICommandMutations Mutations { get; }

    /// <summary>
    /// Принудительно уступить оставшийся квант и вернуться в планировщик.
    /// Используется для кооперативной многозадачности.
    /// </summary>
    Task YieldQuantumAsync();

    /// <summary>
    /// Перейти к следующему шагу pipeline.
    /// Может быть вызван только при Complete результате.
    /// </summary>
    Task StepAsync();
}

/// <summary>
/// Результат выполнения кванта
/// </summary>
public readonly record struct QuantumResult
{
    public QuantumStatus Status { get; init; }
    public object? State { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Квант завершён успешно, можно переходить к следующему шагу
    /// </summary>
    public static QuantumResult Complete() => new() { Status = QuantumStatus.Complete };

    /// <summary>
    /// Требуется дополнительный квант для продолжения работы.
    /// State сохраняется и передаётся в следующий квант.
    /// </summary>
    public static QuantumResult Yield(object? state = null) => new()
    {
        Status = QuantumStatus.Yield,
        State = state
    };

    /// <summary>
    /// Ошибка выполнения кванта
    /// </summary>
    public static QuantumResult Error(string message) => new()
    {
        Status = QuantumStatus.Error,
        ErrorMessage = message
    };

    /// <summary>
    /// Прервать выполнение pipeline
    /// </summary>
    public static QuantumResult Abort(string reason) => new()
    {
        Status = QuantumStatus.Abort,
        ErrorMessage = reason
    };
}

public enum QuantumStatus
{
    /// <summary>Квант успешно завершён</summary>
    Complete,
    /// <summary>Требуется ещё один квант</summary>
    Yield,
    /// <summary>Ошибка, но можно продолжить pipeline</summary>
    Error,
    /// <summary>Критическая ошибка, прервать pipeline</summary>
    Abort
}

/// <summary>
/// Интерфейс для чтения payload (Query side CQRS)
/// </summary>
public interface IReadOnlyPayload
{
    T? Get<T>(string key);
    bool Contains(string key);
}

/// <summary>
/// Интерфейс для мутаций (Command side CQRS)
/// </summary>
public interface ICommandMutations
{
    void Set<T>(string key, T value) where T : notnull;
}

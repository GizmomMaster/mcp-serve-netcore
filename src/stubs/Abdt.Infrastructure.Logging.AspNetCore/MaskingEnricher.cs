using Serilog.Core;
using Serilog.Events;

namespace Abdt.Infrastructure.Logging.AspNetCore;

/// <summary>
/// Enricher, заменяющий значения чувствительных свойств на маску (FR-52, NFR-38).
/// Работает по умолчанию — маскирование не нужно включать вручную.
/// </summary>
internal sealed class MaskingEnricher : ILogEventEnricher
{
    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        List<string>? toMask = null;

        foreach (var property in logEvent.Properties)
        {
            if (LogMasking.IsPrivate(property.Key))
            {
                (toMask ??= []).Add(property.Key);
            }
        }

        if (toMask is null)
        {
            return;
        }

        foreach (var key in toMask)
        {
            logEvent.AddOrUpdateProperty(
                propertyFactory.CreateProperty(key, LogMasking.Mask));
        }
    }
}

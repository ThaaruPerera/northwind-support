using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Presentation.Extensions;

public static class SlaSettingsExtensions
{
    /// <summary>
    /// Binds the <c>Sla</c> section once and validates it, so a missing or silly value stops the
    /// API at startup instead of surfacing on the first ticket raised.
    /// </summary>
    public static IServiceCollection AddSlaSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Sla").Get<SlaSettings>()
            ?? throw new InvalidOperationException("The Sla configuration section is missing.");

        settings.Validate();

        return services.AddSingleton(settings);
    }
}

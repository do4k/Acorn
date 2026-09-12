using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Logging;

public static class LoggingBuilderExtensions
{
    /// <summary>
    ///     Adds a rotating-less file log sink. Logs are appended to <paramref name="filePath"/>
    ///     at or above <paramref name="minLevel"/>.
    /// </summary>
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string filePath,
        LogLevel minLevel = LogLevel.Information)
    {
        builder.AddProvider(new FileLoggerProvider(filePath, minLevel));
        return builder;
    }
}

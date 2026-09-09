using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure.Logging;

/// <summary>
///     Writes log entries to a file so game-server logs are always tailable even
///     when the Aspire dashboard's resource-log view is unavailable or flaky.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly object _lock = new();
    private readonly StreamWriter _writer;
    private readonly LogLevel _minLevel;

    public FileLoggerProvider(string filePath, LogLevel minLevel)
    {
        _minLevel = minLevel;

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true
        };
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public bool IsEnabled(LogLevel level) => level >= _minLevel;

    public void Write<TState>(
        string category,
        LogLevel level,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        var message = formatter(state, exception);
        if (exception is not null)
        {
            message = $"{message}  {exception}";
        }

        lock (_lock)
        {
            _writer.WriteLine($"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level,-11}] {category}: {message}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer.Dispose();
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _provider.Write(_category, logLevel, eventId, state, exception, formatter);
        }
    }
}

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

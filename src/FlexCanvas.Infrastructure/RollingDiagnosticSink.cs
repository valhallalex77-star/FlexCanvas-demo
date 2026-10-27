using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using FlexCanvas.Application;
using FlexCanvas.Domain;

namespace FlexCanvas.Infrastructure;

public sealed class RollingDiagnosticSink : IDiagnosticSink, IAsyncDisposable
{
    private readonly Channel<DiagnosticEntry> _queue = Channel.CreateBounded<DiagnosticEntry>(
        new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly string _directory;
    private readonly long _maxBytes;
    private readonly int _maxFiles;
    private readonly Task _writer;
    private int _unhealthy;
    private long _sequence;
    private string? _current;
    private DateOnly _currentDay;

    public RollingDiagnosticSink(string directory, long maxBytes = 1024 * 1024, int maxFiles = 8)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1024);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 1);
        _directory = Path.GetFullPath(directory);
        _maxBytes = maxBytes;
        _maxFiles = maxFiles;
        _writer = Task.Run(WriteAsync);
    }

    public bool IsHealthy => Volatile.Read(ref _unhealthy) == 0;
    public void Record(DiagnosticEntry entry)
    {
        if (!_queue.Writer.TryWrite(entry)) Interlocked.Exchange(ref _unhealthy, 1);
    }

    private async Task WriteAsync()
    {
        await foreach (var entry in _queue.Reader.ReadAllAsync())
        {
            try
            {
                Directory.CreateDirectory(_directory);
                // Only fixed fields are serializable; caller messages and window contents never enter the sink.
                string line = JsonSerializer.Serialize(new
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Level = entry.Level.ToString(), EventName = entry.Event.ToString(),
                    entry.CorrelationId, Code = entry.Code.ToString(), Win32Api = entry.Api.ToString(),
                    Win32ErrorCode = entry.NativeError, entry.ProcessId,
                    Hwnd = entry.Hwnd?.ToInt64(), entry.ExceptionHResult
                }) + "\n";
                int length = Encoding.UTF8.GetByteCount(line);
                DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (_current is null || _currentDay != today || !File.Exists(_current) || new FileInfo(_current).Length + length > _maxBytes)
                {
                    _currentDay = today;
                    _current = Path.Combine(_directory, $"app-{DateTime.UtcNow:yyyyMMddHHmmssfffffff}-{Environment.ProcessId}-{++_sequence}.jsonl");
                    await File.WriteAllTextAsync(_current, "", new UTF8Encoding(false));
                    Prune();
                }
                await File.AppendAllTextAsync(_current, line, new UTF8Encoding(false));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Interlocked.Exchange(ref _unhealthy, 1);
            }
        }
    }

    private void Prune()
    {
        // Restrict cleanup to this provider's files; never delete POC logs or unrelated data.
        var files = new DirectoryInfo(_directory).GetFiles("app-*.jsonl")
            .OrderByDescending(f => f.LastWriteTimeUtc).ThenByDescending(f => f.Name, StringComparer.Ordinal).ToArray();
        foreach (var file in files.Where((f, i) => f.FullName != _current
            && (i >= _maxFiles || f.LastWriteTimeUtc < DateTime.UtcNow.AddDays(-7))))
            file.Delete();
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _writer.ConfigureAwait(false);
    }
}

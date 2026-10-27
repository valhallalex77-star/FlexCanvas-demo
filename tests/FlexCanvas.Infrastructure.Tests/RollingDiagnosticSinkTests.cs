using System.IO;
using System.Text.Json;
using FlexCanvas.Domain;
using Xunit;

namespace FlexCanvas.Infrastructure.Tests;

public sealed class RollingDiagnosticSinkTests
{
    [Fact]
    public async Task RollingLimitsAndFixedFieldsPreserveCorrelationAndOtherFiles()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"FlexCanvas-log-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            string unrelated = Path.Combine(folder, "poc-preserved.log");
            await File.WriteAllTextAsync(unrelated, "do not delete");
            string expired = Path.Combine(folder, "app-expired.jsonl");
            await File.WriteAllTextAsync(expired, "old");
            File.SetLastWriteTimeUtc(expired, DateTime.UtcNow.AddDays(-8));
            Guid id = Guid.NewGuid();
            var sink = new RollingDiagnosticSink(folder, 1024, 3);
            for (int i = 0; i < 24; i++)
                sink.Record(new(DiagnosticEvent.NativeRead, DiagnosticLevel.Warning, id,
                    DiagnosticCode.AccessDenied, NativeApi.OpenProcess, 5, 42, (nint)123));
            await sink.DisposeAsync();
            Assert.True(sink.IsHealthy);
            var files = Directory.GetFiles(folder, "app-*.jsonl");
            Assert.InRange(files.Length, 1, 3);
            Assert.False(File.Exists(expired));
            Assert.True(File.Exists(unrelated));
            foreach (string file in files)
            {
                Assert.InRange(new FileInfo(file).Length, 1, 1024);
                foreach (string line in await File.ReadAllLinesAsync(file))
                {
                    using var json = JsonDocument.Parse(line);
                    Assert.Equal(id, json.RootElement.GetProperty("CorrelationId").GetGuid());
                    Assert.Equal(5, json.RootElement.GetProperty("Win32ErrorCode").GetInt32());
                    Assert.False(json.RootElement.TryGetProperty("Title", out _));
                    Assert.False(json.RootElement.TryGetProperty("Message", out _));
                    Assert.False(json.RootElement.TryGetProperty("Path", out _));
                }
            }
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task UnwritableDestinationIsVisibleAndShutdownStillCompletes()
    {
        string path = Path.Combine(Path.GetTempPath(), $"FlexCanvas-log-blocker-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(path, "owned test file");
        try
        {
            var sink = new RollingDiagnosticSink(path);
            sink.Record(new(DiagnosticEvent.Startup, DiagnosticLevel.Information, Guid.NewGuid()));
            await sink.DisposeAsync();
            Assert.False(sink.IsHealthy);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task CompletedQueueReportsDroppedWrites()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"FlexCanvas-log-closed-{Guid.NewGuid():N}");
        var sink = new RollingDiagnosticSink(folder);
        await sink.DisposeAsync();
        sink.Record(new(DiagnosticEvent.Shutdown, DiagnosticLevel.Information, Guid.NewGuid()));
        Assert.False(sink.IsHealthy);
        Assert.False(Directory.Exists(folder));
    }
}

using AUTORESTART1.Backup;
using AUTORESTART1.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AUTORESTART1.Tests.Backup;

public sealed class CimShadowCopyServiceTests
{
    private const string ShadowId = "{11111111-2222-3333-4444-555555555555}";
    private const string DeviceObject = @"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy42";

    [Fact]
    public async Task CreateAsync_returns_the_device_for_the_exact_created_shadow()
    {
        RecordingCommandRunner runner = new(
            Success($"ReturnValue = 0;\r\nShadowID = \"{ShadowId}\";"),
            Success($"DeviceObject={DeviceObject}"));
        CimShadowCopyService service = CreateService(runner);

        ShadowCopy shadow = await service.CreateAsync(@"C:\data\att2000.mdb", CancellationToken.None);

        Assert.Equal(ShadowId, shadow.Id);
        Assert.Equal(DeviceObject, shadow.DeviceObject);
        Assert.All(runner.Calls, call => Assert.Equal("powershell.exe", call.FileName));
        Assert.Contains(runner.Calls[1].Arguments, argument => argument.Contains(ShadowId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateAsync_rejects_a_nonzero_cim_return_value()
    {
        RecordingCommandRunner runner = new(Success("ReturnValue = 8;"));
        CimShadowCopyService service = CreateService(runner);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(@"C:\data\att2000.mdb", CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_deletes_the_exact_shadow_when_device_lookup_is_malformed()
    {
        RecordingCommandRunner runner = new(
            Success($"ReturnValue = 0;\r\nShadowID = \"{ShadowId}\";"),
            Success("unexpected output"),
            Success("Delete successful."));
        CimShadowCopyService service = CreateService(runner);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(@"C:\data\att2000.mdb", CancellationToken.None));

        Assert.Contains(runner.Calls[2].Arguments, argument => argument.Contains(ShadowId, StringComparison.Ordinal));
        Assert.DoesNotContain(runner.Calls, call =>
            string.Join(' ', call.Arguments).Equals("shadowcopy delete /nointeractive", StringComparison.OrdinalIgnoreCase));
    }

    private static CimShadowCopyService CreateService(ICommandRunner runner) => new(
        runner,
        new BackupOptions { CommandTimeoutSeconds = 5 },
        NullLogger<CimShadowCopyService>.Instance);

    private static CommandResult Success(string output) => new(0, output, string.Empty);

    private sealed class RecordingCommandRunner(params CommandResult[] results) : ICommandRunner
    {
        private readonly Queue<CommandResult> _results = new(results);

        public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public Task<CommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Calls.Add((fileName, arguments.ToArray()));
            return Task.FromResult(_results.Dequeue());
        }
    }
}

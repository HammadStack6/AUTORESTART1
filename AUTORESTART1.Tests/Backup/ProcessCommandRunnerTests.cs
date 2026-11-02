using AUTORESTART1.Backup;

namespace AUTORESTART1.Tests.Backup;

public sealed class ProcessCommandRunnerTests
{
    [Fact]
    public async Task RunAsync_rejects_a_nonzero_exit_code()
    {
        ProcessCommandRunner runner = new();

        CommandExecutionException exception = await Assert.ThrowsAsync<CommandExecutionException>(() =>
            runner.RunAsync("cmd.exe", ["/c", "exit 7"], TimeSpan.FromSeconds(5), CancellationToken.None));

        Assert.Equal(7, exception.ExitCode);
    }

    [Fact]
    public async Task RunAsync_terminates_a_command_that_exceeds_its_timeout()
    {
        ProcessCommandRunner runner = new();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            runner.RunAsync(
                "cmd.exe",
                ["/c", "ping 127.0.0.1 -n 6 > nul"],
                TimeSpan.FromMilliseconds(100),
                CancellationToken.None));
    }
}

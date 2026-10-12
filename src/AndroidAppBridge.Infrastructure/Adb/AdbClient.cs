namespace AndroidAppBridge.Infrastructure;

public interface IAdbClient
{
    Task<string> RunAsync(string? serial, CancellationToken ct, params string[] arguments);
}

/// <summary>Adapter from ADB commands to a shell-free process request.</summary>
public sealed class AdbClient(string executable, IProcessRunner runner) : IAdbClient
{
    public async Task<string> RunAsync(string? serial, CancellationToken ct, params string[] arguments)
    {
        var args = new List<string>();
        if (serial != null)
        {
            args.Add("-s");
            args.Add(serial);
        }
        args.AddRange(arguments);
        var result = await runner.RunAsync(new(executable, args, TimeSpan.FromMinutes(15)), ct);
        var output = (result.Output + "\n" + result.Error).Trim();
        if (result.ExitCode != 0)
            throw new IOException(output);
        return output;
    }
}

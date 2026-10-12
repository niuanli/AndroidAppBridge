using System.ComponentModel;
using System.Diagnostics;

namespace AndroidAppBridge.Infrastructure;

public record ProcessRequest(string Executable, IReadOnlyList<string> Arguments, TimeSpan Timeout);
public record ProcessResult(int ExitCode, string Output, string Error);
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken ct);
}

/// <summary>Owns one child process and drains both streams while awaiting exit.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(request.Executable))
            throw new FileNotFoundException("Нет platform-tools/adb.exe рядом с программой.");
        var start = new ProcessStartInfo(request.Executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in request.Arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Не удалось запустить ADB.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(request.Timeout);
        void Kill()
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(true);
            }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
        }
        using var registration = timeout.Token.Register(Kill);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Kill();
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException) { }
            if (ct.IsCancellationRequested)
                throw new OperationCanceledException(ct);
            throw new TimeoutException("ADB не завершил операцию за отведённое время.");
        }
        return new(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }
}

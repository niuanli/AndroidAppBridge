namespace AndroidAppBridge.Application;

/// <summary>Observer adapter; the presentation layer is responsible for UI-thread dispatch.</summary>
public sealed class DelegateProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}

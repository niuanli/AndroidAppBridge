namespace AndroidAppBridge.Tests.Infrastructure;

[TestFixture]
public class AdbGatewayTests
{
    sealed class Runner : IProcessRunner
    {
        public ProcessRequest? Request;
        public ProcessResult Result = new(0, "Success", "");
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Request = request;
            return Task.FromResult(Result);
        }
    }
    sealed class Client : IAdbClient
    {
        public List<string[]> Calls = [];
        public Queue<string> Responses = new();
        public Task<string> RunAsync(string? serial, CancellationToken ct, params string[] arguments)
        {
            ct.ThrowIfCancellationRequested();
            Calls.Add(arguments);
            return Task.FromResult(Responses.Count > 0 ? Responses.Dequeue() : "Success");
        }
    }
    [Test]
    public async Task KeepsPathsWithSpacesAndShellCharactersAsSingleArguments()
    {
        var runner = new Runner();
        var client = new AdbClient("adb.exe", runner);
        var path = "C:\\Apps\\[APK] file & copy.apk";
        await client.RunAsync("serial", CancellationToken.None, "install", path);
        Assert.That(runner.Request!.Arguments, Is.EqualTo(new[] { "-s", "serial", "install", path }));
        Assert.That(runner.Request.Timeout, Is.EqualTo(TimeSpan.FromMinutes(15)));
    }
    [Test]
    public void NonZeroExitReportsBothOutputStreams()
    {
        var runner = new Runner { Result = new(1, "Failure", "INSTALL_FAILED_USER_RESTRICTED") };
        var client = new AdbClient("adb.exe", runner);
        var error = Assert.ThrowsAsync<IOException>(() => client.RunAsync(null, CancellationToken.None, "install", "game.apk"));
        Assert.That(error!.Message, Does.Contain("Failure").And.Contain("INSTALL_FAILED_USER_RESTRICTED"));
    }
    [Test]
    public void PropagatesCallerCancellation()
    {
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        var runner = new Runner();
        Assert.ThrowsAsync<OperationCanceledException>(() => new AdbClient("adb", runner).RunAsync(null, ct.Token, "devices"));
        Assert.That(runner.Request, Is.Null);
    }
    [Test]
    public async Task ParsesDeviceStateAndReadableModel()
    {
        var client = new Client();
        client.Responses.Enqueue("List of devices attached\nserial1 device model:Test_Phone transport_id:1\nserial2 unauthorized\nserial3 offline\n");
        var devices = await new AdbDeviceGateway(client).GetDevicesAsync(CancellationToken.None);
        Assert.That(devices.Select(d => d.State), Is.EqualTo(new[] { "device", "unauthorized", "offline" }));
        Assert.That(devices[0].Model, Is.EqualTo("Test Phone"));
    }
    [TestCase(true)]
    [TestCase(false)]
    public async Task UsesReplacementFlagOnlyForAnExistingApp(bool replace)
    {
        var client = new Client();
        await new AdbDeviceGateway(client).InstallAsync("serial", ["base.apk", "split.apk"], replace, CancellationToken.None);
        Assert.That(client.Calls.Single(), Is.EqualTo(replace ? new[] { "install-multiple", "-r", "base.apk", "split.apk" } : new[] { "install-multiple", "base.apk", "split.apk" }));
    }
    [Test]
    public async Task ChecksExactPackageRatherThanSubstringMatch()
    {
        var client = new Client();
        client.Responses.Enqueue("package:org.example.game.extra\n");
        Assert.That(await new AdbDeviceGateway(client).IsInstalledAsync("serial", "org.example.game", CancellationToken.None), Is.False);
    }
    [Test]
    public async Task CreatesTheObbDirectoryBeforePushing()
    {
        var client = new Client();
        await new AdbDeviceGateway(client).PushObbAsync("serial", "main.obb", "/sdcard/Android/obb/org.example.game/main.obb", CancellationToken.None);
        Assert.That(client.Calls[0], Is.EqualTo(new[] { "shell", "mkdir", "-p", "/sdcard/Android/obb/org.example.game" }));
        Assert.That(client.Calls[1][0], Is.EqualTo("push"));
    }
    [Test]
    public void AReportedLaunchErrorIsNotSuccess()
    {
        var client = new Client();
        client.Responses.Enqueue("org.example.game/.MainActivity");
        client.Responses.Enqueue("Error: Activity not found");
        Assert.ThrowsAsync<IOException>(() => new AdbDeviceGateway(client).LaunchAsync("serial", "org.example.game", CancellationToken.None));
    }
}

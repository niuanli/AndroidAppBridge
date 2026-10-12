namespace AndroidAppBridge.Presentation;

static class ReleaseInfo
{
    public static string Version => typeof(ReleaseInfo).Assembly.GetName().Version!.ToString(3);
}

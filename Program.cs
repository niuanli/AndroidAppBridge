using System.IO.Compression;
using System.Text.Json;

namespace AppBridge;

/// <summary>Windows entry point and explicit offline diagnostic commands.</summary>
static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            SelfTest();
            return;
        }
        ApplicationConfiguration.Initialize();
        if (args.Contains("--ui-check"))
        {
            var results = new List<string>();
            foreach (var factor in new[] { 1f, 1.25f, 1.5f })
            {
                using var form = new BridgeForm(previewOnly: true);
                form.Opacity = 0;
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                if (factor != 1)
                    form.SimulateScale(factor);
                form.PreviewBackup(longName: factor > 1);
                Application.DoEvents();
                form.CheckLayout();
                using var picture = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(picture, new Rectangle(Point.Empty, form.Size));
                picture.Save(Path.Combine(AppContext.BaseDirectory, $"ui-{factor * 100:F0}.png"));
                results.Add($"PASS: simulated layout + font scale {factor * 100:F0}%");
            }
            using (var wide = new BridgeForm(previewOnly: true))
            {
                wide.Opacity = 0;
                wide.ShowInTaskbar = false;
                wide.Show();
                wide.Size = new Size(1900, 1000);
                Application.DoEvents();
                var check = wide.CheckIdle();
                while (!check.IsCompleted)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }
                check.GetAwaiter().GetResult();
                wide.PreviewBackup();
                Application.DoEvents();
                wide.CheckLayout();
                wide.CheckSearchAndLanguages();
                results.Add("PASS: search matching, empty results, reset, RU/EN switching, About notices and author");
                using var picture = new Bitmap(wide.Width, wide.Height);
                wide.DrawToBitmap(picture, new Rectangle(Point.Empty, wide.Size));
                picture.Save(Path.Combine(AppContext.BaseDirectory, "ui-wide-idle.png"));
                results.Add("PASS: wide layout, idle at 0%, action prerequisites");
            }
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "ui-check-result.txt"), results);
            return;
        }
        if (args.Contains("--preview"))
        {
            using var form = new BridgeForm(previewOnly: true);
            form.Opacity = 0;
            form.ShowInTaskbar = false;
            form.Show();
            Application.DoEvents();
            form.PerformLayout();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "preview.png"));
            form.PreviewProgress();
            Application.DoEvents();
            using var active = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(active, new Rectangle(Point.Empty, form.Size));
            active.Save(Path.Combine(AppContext.BaseDirectory, "preview-progress-demo.png"));
            return;
        }
        Application.Run(new BridgeForm());
    }
    // Synthetic APKs and manifests keep integrity tests independent of installed applications.
    static void SelfTest()
    {
        var root = Path.Combine(Path.GetTempPath(), "AppBridge-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "apk"));
        try
        {
            void Assert(bool ok, string name)
            {
                if (!ok)
                    throw new Exception(name);
            }
            var apk = Path.Combine(root, "apk", "base.apk");
            using (var zip = ZipFile.Open(apk, ZipArchiveMode.Create))
            {
                zip.CreateEntry("lib/arm64-v8a/libtest.so");
            }
            var b = new Backup("test.example", "1", 22, 35, Storage.Abis([apk]), [new("apk/base.apk", Storage.Hash(apk), new FileInfo(apk).Length)], "test");
            File.WriteAllText(Path.Combine(root, "backup.json"), JsonSerializer.Serialize(b));
            Assert(Storage.Load(root).Package == "test.example", "backup roundtrip");
            Assert(Storage.Compatibility(b, 36, ["arm64-v8a"]) == null, "compatible");
            Assert(Storage.Compatibility(b, 36, ["armeabi-v7a"]) != null, "ABI mismatch");
            Assert(Storage.Compatibility(b, 21, ["arm64-v8a"]) != null, "minSdk");
            foreach (var path in new[] { "../outside.apk", "C:\\outside.apk", "apk/../../outside.apk" })
            {
                var denied = false;
                try
                {
                    Storage.Resolve(root, path);
                }
                catch
                {
                    denied = true;
                }
                Assert(denied, "path traversal");
            }
            File.AppendAllText(apk, "tamper");
            var failed = false;
            try
            {
                Storage.Load(root);
            }
            catch
            {
                failed = true;
            }
            Assert(failed, "integrity");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test-result.txt"), "PASS: backup roundtrip, ABI, minSdk, path traversal, integrity");
        }
        finally
        {
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath())) && Path.GetFileName(root).StartsWith("AppBridge-test-"))
                Directory.Delete(root, true);
        }
    }
}

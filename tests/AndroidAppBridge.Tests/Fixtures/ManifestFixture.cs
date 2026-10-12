using System.IO.Compression;
using System.Text;

namespace AndroidAppBridge.Tests.Fixtures;

/// <summary>Synthetic binary manifests exercise import metadata and the backup transaction without a device.</summary>
static class ManifestFixture
{
    public static byte[] Manifest(bool utf8, string package = "org.example.game", string split = "")
    {
        var strings = new[] { "manifest", "package", package, "http://schemas.android.com/apk/res/android", "versionCode", "versionName", "1.0", "uses-sdk", "minSdkVersion", "targetSdkVersion", "split", split };
        using var stringBytes = new MemoryStream();
        using var data = new BinaryWriter(stringBytes, Encoding.UTF8, true);
        var offsets = new List<int>();
        foreach (var s in strings)
        {
            offsets.Add((int)stringBytes.Length);
            if (utf8)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                data.Write((byte)s.Length);
                data.Write((byte)bytes.Length);
                data.Write(bytes);
                data.Write((byte)0);
            }
            else
            {
                data.Write((ushort)s.Length);
                data.Write(Encoding.Unicode.GetBytes(s));
                data.Write((ushort)0);
            }
        }
        using var body = new MemoryStream();
        using var writer = new BinaryWriter(body, Encoding.UTF8, true);
        var poolSize = 28 + strings.Length * 4 + (int)stringBytes.Length;
        writer.Write((ushort)1);
        writer.Write((ushort)28);
        writer.Write(poolSize);
        writer.Write(strings.Length);
        writer.Write(0);
        writer.Write(utf8 ? 0x100 : 0);
        writer.Write(28 + strings.Length * 4);
        writer.Write(0);
        foreach (var offset in offsets)
            writer.Write(offset);
        writer.Write(stringBytes.ToArray());
        void Element(int name, (uint Ns, uint Name, byte Type, uint Value)[] attrs)
        {
            writer.Write((ushort)0x102);
            writer.Write((ushort)16);
            writer.Write(36 + attrs.Length * 20);
            writer.Write(1);
            writer.Write(uint.MaxValue);
            writer.Write(uint.MaxValue);
            writer.Write(name);
            writer.Write((ushort)20);
            writer.Write((ushort)20);
            writer.Write((ushort)attrs.Length);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            foreach (var a in attrs)
            {
                writer.Write(a.Ns);
                writer.Write(a.Name);
                writer.Write(uint.MaxValue);
                writer.Write((ushort)8);
                writer.Write((byte)0);
                writer.Write(a.Type);
                writer.Write(a.Value);
            }
        }
        var attributes = new List<(uint, uint, byte, uint)> { (uint.MaxValue, 1, 3, 2), (3, 4, 0x10, 5), (3, 5, 3, 6) };
        if (split.Length > 0)
            attributes.Add((uint.MaxValue, 10, 3, 11));
        Element(0, attributes.ToArray());
        Element(7, [(3, 8, 0x10, 21), (3, 9, 0x10, 35)]);
        using var full = new MemoryStream();
        using var header = new BinaryWriter(full);
        header.Write((ushort)3);
        header.Write((ushort)8);
        header.Write((int)body.Length + 8);
        header.Write(body.ToArray());
        return full.ToArray();
    }
}

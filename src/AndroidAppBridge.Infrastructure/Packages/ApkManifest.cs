using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace AndroidAppBridge.Infrastructure;

/// <summary>Reads literal package metadata from Android binary XML without executing the APK.</summary>
public static class ApkManifest
{
    public static ApkInfo Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry("AndroidManifest.xml") ?? throw new InvalidDataException("В APK отсутствует AndroidManifest.xml.");
        if (entry.Length > 16 * 1024 * 1024)
            throw new InvalidDataException("Манифест APK слишком большой.");
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return Parse(memory.ToArray());
    }

    // Layout follows AOSP ResourceTypes.h: chunk headers, string pools and start-element attributes.
    internal static ApkInfo Parse(byte[] bytes)
    {
        int limit = bytes.Length;
        void Bounds(int offset, int size)
        {
            if (offset < 0 || size < 0 || offset > limit - size)
                throw new InvalidDataException("Манифест APK повреждён.");
        }
        ushort U16(int offset)
        {
            Bounds(offset, 2);
            return BitConverter.ToUInt16(bytes, offset);
        }
        uint U32(int offset)
        {
            Bounds(offset, 4);
            return BitConverter.ToUInt32(bytes, offset);
        }
        int I32(int offset) => checked((int)U32(offset));
        if (U16(0) != 3 || U16(2) < 8 || I32(4) != bytes.Length)
            throw new InvalidDataException("Формат манифеста APK не поддерживается.");
        string[] strings = [];
        string String(uint index) => index == uint.MaxValue ? "" : index < strings.Length ? strings[index] : throw new InvalidDataException("Некорректный индекс строки в манифесте APK.");
        var values = new Dictionary<string, string>();
        for (var offset = (int)U16(2); offset < bytes.Length;)
        {
            limit = bytes.Length;
            var type = U16(offset);
            var header = U16(offset + 2);
            var size = I32(offset + 4);
            if (header < 8 || size < header)
                throw new InvalidDataException("Некорректный блок манифеста APK.");
            Bounds(offset, size);
            limit = offset + size;
            if (type == 1)
            {
                if (header < 28)
                    throw new InvalidDataException("Некорректная таблица строк манифеста APK.");
                var count = I32(offset + 8);
                if (count > 100000)
                    throw new InvalidDataException("В манифесте APK слишком много строк.");
                Bounds(offset + header, checked(count * 4));
                var utf8 = (U32(offset + 16) & 0x100) != 0;
                var start = checked(offset + I32(offset + 20));
                strings = new string[count];
                for (var i = 0; i < count; i++)
                {
                    var position = checked(start + I32(offset + header + i * 4));
                    int Length8()
                    {
                        Bounds(position, 1);
                        var n = bytes[position++];
                        if ((n & 0x80) == 0)
                            return n;
                        Bounds(position, 1);
                        return ((n & 0x7f) << 8) | bytes[position++];
                    }
                    int length;
                    if (utf8)
                    {
                        Length8();
                        length = Length8();
                        Bounds(position, checked(length + 1));
                        strings[i] = new UTF8Encoding(false, true).GetString(bytes, position, length);
                        if (bytes[position + length] != 0)
                            throw new InvalidDataException("Повреждена строка манифеста APK.");
                    }
                    else
                    {
                        length = U16(position);
                        position += 2;
                        if ((length & 0x8000) != 0)
                        {
                            length = ((length & 0x7fff) << 16) | U16(position);
                            position += 2;
                        }
                        var byteCount = checked(length * 2);
                        Bounds(position, checked(byteCount + 2));
                        strings[i] = new UnicodeEncoding(false, false, true).GetString(bytes, position, byteCount);
                        if (U16(position + byteCount) != 0)
                            throw new InvalidDataException("Повреждена строка манифеста APK.");
                    }
                }
            }
            else if (type == 0x102)
            {
                var ext = offset + header;
                Bounds(ext, 20);
                var element = String(U32(ext + 4));
                var attrSize = U16(ext + 10);
                var count = U16(ext + 12);
                var attrs = checked(ext + U16(ext + 8));
                if (attrSize < 20)
                    throw new InvalidDataException("Некорректный размер атрибута манифеста APK.");
                Bounds(attrs, checked(count * attrSize));
                for (var i = 0; i < count; i++)
                {
                    var a = attrs + i * attrSize;
                    var ns = String(U32(a));
                    var name = String(U32(a + 4));
                    if (element != "manifest" && element != "uses-sdk")
                        continue;
                    var android = ns == "http://schemas.android.com/apk/res/android";
                    if (!(android || ns.Length == 0 && element == "manifest" && name is "package" or "split"))
                        continue;
                    var raw = U32(a + 8);
                    var data = U32(a + 16);
                    var dataType = bytes[a + 15];
                    var value = raw != uint.MaxValue ? String(raw) : dataType == 3 ? String(data) : dataType is 0x10 or 0x11 ? data.ToString(CultureInfo.InvariantCulture) : "";
                    values[element + "/" + name] = value;
                }
            }
            offset += size;
        }
        string Value(string key, string fallback = "") => values.GetValueOrDefault(key, fallback);
        int Sdk(string key, int fallback) => !values.ContainsKey(key) ? fallback : int.TryParse(Value(key), out var n) && n > 0 ? n : throw new InvalidDataException("Значение SDK в манифесте APK не поддерживается.");
        var package = Value("manifest/package");
        if (!Storage.PackageValid(package))
            throw new InvalidDataException("Некорректное имя пакета APK.");
        if (!long.TryParse(Value("manifest/versionCode"), out var versionCode))
            throw new InvalidDataException("В APK отсутствует код версии.");
        var min = Sdk("uses-sdk/minSdkVersion", 1);
        return new ApkInfo(package, Value("manifest/versionName", versionCode.ToString(CultureInfo.InvariantCulture)), versionCode, min, Sdk("uses-sdk/targetSdkVersion", min), Value("manifest/split"));
    }
}

# Third-party components

AndroidAppBridge uses .NET 8 and Windows Forms. The .NET runtime is installed
separately; it is not part of the source archive or the framework-dependent
application package.

Android SDK Platform-Tools provides ADB. Platform-Tools binaries are not
committed to this repository. Obtain the Windows package from Google's
[Platform-Tools download page](https://developer.android.com/tools/releases/platform-tools).

The packaging script copies only `adb.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`,
`NOTICE.txt`, `source.properties` and, when present, `package.xml` from the
provided Platform-Tools directory. The original notices and package metadata
are preserved. The application's copyright notice does not apply to ADB.

APK and OBB files are user data and are not included in the repository or source
archive. Their original signatures, copyright and license terms remain with
their rights holders.

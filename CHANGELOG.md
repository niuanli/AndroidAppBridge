# Changelog

## 0.5.1

- Identify the base APK by its manifest instead of its filename, allowing export and installation of preinstalled APKs such as `Canva.apk` and `Moffice_196.apk`.
- Add regression tests for original APK filenames and a split incorrectly named `base.apk`.

## 0.5.0

- Separate Domain, Application, Infrastructure and WinForms projects, with dependencies assembled in one composition root.
- Extract export, import, installation and launch use cases from the main form.
- Introduce device, backup, package import, process and preferences contracts with constructor injection.
- Add a standalone NUnit suite covering policies, binary manifests, import, file integrity, cancellation, ADB arguments and transfer ordering.
- Revalidate an approved backup immediately before installation.
- Commit backup manifests atomically and support cancellation while hashing.
- Distinguish failed OBB transfers after successful APK installation.
- Include local APK/OBB import with automatic OBB discovery and a manual file picker when no matching file is found.
- Update CI, packaging, architecture documentation and the centralized application version.

## 0.3.2

- First published release: device-to-device backups, original APK signatures, optional OBB files and Russian/English interface.

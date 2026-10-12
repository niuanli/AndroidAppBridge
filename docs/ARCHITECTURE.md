# Architecture

AndroidAppBridge separates policy, use cases, external adapters and presentation into independently compiled projects.

```text
WinForms ──> Application ──> Domain
   │              ▲
   └──> Infrastructure ──> Domain
```

WinForms references Infrastructure only to wire concrete adapters in `Program.cs`, the composition root. Forms reference Application contracts and Domain values; they do not run processes or access backup files. Application has no reference to WinForms or Infrastructure. Domain has no project dependencies.

## Responsibilities

| Project | Responsibility |
| --- | --- |
| Domain | Package identity, selection and compatibility policy; transfer values |
| Application | Export, import, installation preparation/execution and launch use cases; external ports |
| Infrastructure | ADB processes, Android response parsing, local package reading, backup repository and user settings |
| WinForms | Controls, dialogs, localization, state display and explicit installation confirmation |
| Tests | NUnit tests for policy, use cases and file/ADB adapters using synthetic fixtures and fakes |

The application uses constructor injection without a container. Its dependency graph is small enough to remain explicit. `IDeviceGateway`, `IBackupRepository` and `ILocalPackageImporter` allow use cases to be exercised without Android devices, ADB or dialogs.

## Patterns and GRASP

| Principle or pattern | Implementation |
| --- | --- |
| Controller | `TransferService` coordinates application use cases independently of form events |
| Information Expert | Domain policies validate APK identities, expansion names and compatibility; the repository validates owned file content |
| Low Coupling / Protected Variations | Application ports isolate device, file and package-reading implementations |
| High Cohesion | Each project owns one layer; ADB execution, protocol parsing, manifest reading and presentation are separate components |
| Pure Fabrication | `FileBackupRepository` and `ProcessRunner` contain technical responsibilities without putting them on Domain records |
| Indirection | Use cases call interfaces rather than processes or controls |
| Polymorphism | Concrete adapters and test fakes implement the same contracts; use cases need no environment-specific branches |
| Creator | The composition root assembles dependencies; the repository owns backup locations and manifest commits |
| Adapter / Repository | `AdbDeviceGateway`, `AdbClient`, `LocalPackageImporter` and `FileBackupRepository` adapt external systems |
| Observer | `IProgress<TransferUpdate>` reports progress and logs; the view dispatches updates to its UI thread |
| Dependency Inversion | Application defines its ports; Infrastructure implements them |

Patterns such as Singleton, Abstract Factory and Visitor are not added without a concrete need. They would increase the dependency graph without solving a current variation or responsibility problem.

## Transfer invariants

1. Export reads package metadata before and after extraction. An update during copying prevents committing the manifest.
2. Import checks one base APK, matching package/version identities for splits, and OBB names before creating a snapshot.
3. The repository rejects paths outside the backup, linked paths, duplicate entries, malformed metadata, wrong sizes and SHA-256 mismatches.
4. Hashing and copy loops observe cancellation. The manifest is written to a temporary file, then renamed atomically after all files are ready.
5. Installation preparation returns an explicit plan. The view requests confirmation before executing it. Execution validates the copy again and rejects changes since confirmation.
6. APK installation precedes OBB transfer. A failed OBB phase raises `PartialTransferException`; cancellation remains cancellation. Neither silently removes the installed app.
7. ADB receives separate process arguments. Both output streams are drained concurrently, and cancellation or timeout terminates the child process tree.
8. SDK and ABI checks are preliminary. Android determines deprecated target SDK, signature and final runtime compatibility.

## Testing

Unit tests run without Android devices. Domain policies use table-driven cases. Use cases use fake ports to verify ordering, cancellation, version races, replacement plans and partial failures. Infrastructure tests use temporary APK/OBB fixtures and a fake process runner to check manifests, checksums, paths and argument boundaries.

File fixtures are generated inside owned temporary directories and removed on disposal. Real apps, device serials, personal directories and copyrighted application binaries are not part of the tests.

`--ui-check` is a separate WinForms visual regression tool. It checks both languages, action availability and layout/font scaling at 100%, 125% and 150%; actual monitor DPI transitions and real device installation remain manual checks.

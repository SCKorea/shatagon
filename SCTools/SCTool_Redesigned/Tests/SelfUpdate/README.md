Run the regression checks from Windows PowerShell with the .NET SDK installed:

```powershell
.\Tests\SelfUpdate\Run.ps1
```

The script builds headless fixtures from the production updater sources. It tests
release asset selection, download size/hash/interruption handling, executable
identity and version checks, single-file and ordinary builds, failed helper
startup, missing update files, startup crashes, version mismatches and a locked
installed executable. Failure cases must preserve the old executable and download
only once. Success cases must start the newer version and remove update files.
The locked-file case takes about 30 seconds.

Artifacts and bundle extraction stay under the application's `bin` directory.
Fixtures use a fake release repository and do not contact GitHub, start the WPF
application or change game files.

Existing artifacts can be rerun with
`Run.ps1 -SkipBuild -ArtifactsPath <directory>`. The runner DLL can also be run
from WSL to check release selection and downloads; Windows is required for PE
metadata and process/file replacement checks.

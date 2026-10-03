# Feature composition checks

This package-free console runner links the patcher's actual catalog, manifest, composer, selection migration and HTTP release client. It uses NEWTRY's shared fixture and a fake HTTP handler; no game writes, credentials or network are required.

From `SCTool_Redesigned`:

```bash
dotnet run --project Tests/FeatureComposition/FeatureComposition.Tests.csproj -- /mnt/e/StarCitizenLab/SCKorea/NEWTRY/tools/tests/fixtures/feature-compose.json
```

When only .NET 10 is installed on Linux, add `-p:FeatureTestFramework=net10.0` before `--`. The application itself still targets .NET 8 Windows. Use this dedicated property so restore and execution target the same framework.

Optionally pass a generated NEWTRY candidate root as the second argument. The runner serves its assets through the fake handler, validates them and creates `release/local/csharp-{base,default,all}.ini`. If the corresponding `python-{base,default,all}.ini` files exist, it also compares the bytes. Temporary installation ZIPs are removed.

Checks cover shared expected bytes, priority ties, UTF-8/BOM/CRLF, literal escapes, whitespace and whole keys, invalid catalogs/INI, null versus empty saved selections, historical migration, tagged metadata, private API headers, selected downloads, missing selected assets, fatal integrity/authentication failures, cancellation and legacy single-pack/source installation paths. Transport checks also cover direct INI compatibility, compressed base/features, nested and uppercase INI names, no-INI ZIPs receiving the same base failure or selected-feature warning/omission as missing files, ambiguous/corrupt ZIPs, archive and extracted-content hashes, extraction size limits and ZIP download cancellation. Actual WPF interaction and live private GitHub downloads require a separate Windows/environment check.

For an explicitly authorized live download check, append `--live-tag <tag>` and supply `SC_FEATURE_TEST_TOKEN` through the process environment (never a command-line argument or committed file). This additionally loads that private release's actual metadata and default files through the production HTTP client, validates the prepared ZIP and removes it. It does not install into the game. Keep credentials out of output. Ordinary tests remain offline.

Use `dotnet <runner.dll> --live-only <tag>` to run only that environment check. Before publication, GitHub's tag endpoint can return 404 for drafts; the runner then finds exactly one matching tag in the authenticated release list. `SC_FEATURE_EXPECTED_SHA256` optionally requires the live composition to match an already verified local result.

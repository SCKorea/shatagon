# Patch release listing checks

This console runner links the real custom release repository, update factory, settings and SCToolsLib. Offline checks use synthetic settings and fake GitHub responses to cover saved true/false preferences, Nightly defaults, draft metadata without publication/source URLs, retained assets, authenticated listing, filtering and true/false refreshes.

From `SCTool_Redesigned`:

```bash
dotnet run --project Tests/ReleaseListing/ReleaseListing.Tests.csproj -p:NuGetAudit=false
```

It targets .NET 8 and permits a later installed runtime via `RollForward=Major`; the WPF application still targets .NET 8 Windows.

For the separately authorized, read-only private GitHub check, pass `-- --live-only` and supply `SC_DRAFT_LIST_TEST_TOKEN` through the process environment. This confirms that the current `0.67.2` draft and its compressed-base metadata are present when allowed and absent when disabled. It does not modify settings, download/install game data or change releases. Never put credentials on the command line or in committed files.

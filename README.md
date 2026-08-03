# DirectorySorter
## Build (Windows, .NET 8 SDK required)

```powershell
git clone/copy this folder, then from the repo root:
.\build.ps1
```

This produces a self-contained `dist\` folder with `DirectorySorter.exe`,
`DirectorySorter.Watcher.exe`, `DirectorySorter.Core.dll`, a `Plugins\` folder with the
three plugin DLLs, and `sorter.config.json`. Copy `dist\` anywhere on the machine.

If you don't have the SDK: install "**.NET 8 SDK**" from
https://dotnet.microsoft.com/download (not just the runtime), or open
`DirectorySorter.sln` in Visual Studio 2022+ and hit Build.

## Run

```powershell
DirectorySorter.exe C:\Users\HowardWolowitz\Downloads --strategy=extension --dry-run
DirectorySorter.exe C:\Users\HowardWolowitz\Downloads --strategy=date --recursive
DirectorySorter.exe --list-plugins
DirectorySorter.exe --undo "Journals\sort-journal-20260803-120000.json"
```

Edit `sorter.config.json` to set default strategy, conflict-resolution mode
(`rename` | `skip` | `overwrite`), and the folders `DirectorySorter.Watcher.exe`
should monitor continuously.

## Writing your own plugin DLL

```csharp
using DirectorySorter.Core;

public sealed class ByOwnerPlugin : ISortPlugin
{
    public string Key => "owner";
    public string DisplayName => "By File Owner";

    public string? GetDestinationFolder(FileInfo file, SortContext context)
        => System.Security.AccessControl.FileSystemAclExtensions
             .GetAccessControl(file).GetOwner(typeof(System.Security.Principal.NTAccount))?.ToString();
}
```
Build it as a `net8.0` class library referencing `DirectorySorter.Core.dll`, drop the
output DLL into `dist\Plugins\`, and `--strategy=owner` becomes available immediately.
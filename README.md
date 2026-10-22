# Controlability

A Windows controller compatibility project, intended to map physical game controllers to virtual Xbox 360 controllers and support keyboard + controller local co-op.

**Status: scaffold only (0.1.0).** Controller input, virtual output, mapping, device hiding, and co-op are not implemented yet. The desktop app opens an empty window and the CLI is a placeholder. This is a development foundation, not a usable controller utility.

## Build and run

Requirements: Windows 10/11 x64 and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

Run from the repository root:

```powershell
dotnet restore Controlability.sln
dotnet build Controlability.sln --configuration Release --no-restore
dotnet test Controlability.sln --configuration Release --no-build
dotnet run --project src/Controlability.App
dotnet run --project src/Controlability.Cli
```

No controller drivers are required for the current scaffold. Existing tests are placeholder smoke checks; they do not validate controller behavior.

## Structure

| Project | Intended responsibility |
| --- | --- |
| Controlability.Core | Platform-independent models, mapping, profiles, abstractions |
| Controlability.Input | Controller and keyboard input |
| Controlability.Output | Virtual controller output |
| Controlability.Platform | Windows integration and device hiding |
| Controlability.Engine | Processing and controller slots |
| Controlability.App | WPF desktop interface |
| Controlability.Cli | Console diagnostics |
| tests/ | Core and engine tests |

Core targets `net10.0`; Windows projects target `net10.0-windows`. Nullable reference types and warnings as errors are enabled.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development guidance.

## License

Released under the [MIT License](LICENSE). Copyright (c) 2026 SinisterRed.

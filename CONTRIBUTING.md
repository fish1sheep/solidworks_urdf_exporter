# Contributing to SolidWorks URDF Exporter

## Branch Strategy

- `main` — stable release branch, always builds successfully
- `dev` — active development branch (primary target for PRs)
- Feature branches should be named `dev/<feature-name>`

## Development Environment

- Visual Studio 2022 or later
- .NET Framework 4.8 SDK
- SolidWorks 2021–2024 (x64)
- Git Bash for development scripts

## Code Structure

```
SW2URDF/
├── SW/               # SolidWorks add-in lifecycle (SwAddin, EventHandling)
├── UI/               # WinForms/WPF UI (AssemblyExportForm, PartExportForm, TreeView)
├── URDF/             # URDF data model classes (Robot, Link, Joint, etc.)
├── URDFExport/       # Export pipeline (ExportHelper, PropertyManager, Merge)
│   ├── CSV/          # CSV import/export
│   └── URDFMerge/    # TreeView merge logic
├── ROS/              # ROS2 file generation (Rviz, Gazebo)
├── Utilities/        # Logging, math helpers
├── Test/             # xUnit tests (some require SolidWorks, some don't)
└── Versioning/       # Build version info
```

## Coding Standards

- Follow existing patterns (file-scoped namespaces, brace placement)
- Prefer `IComponentHandle` over `Component2` for SW component references in the URDF model layer
- Use typed exceptions (`URDFAttributeException`, `ExportException`) rather than `throw new Exception()`
- All public methods should have XML doc comments

## Adding Tests

Tests requiring SolidWorks are in `SW2URDF/Test/` and use `[Collection("Requires SW Test Collection")]`.

Pure-logic tests (no SolidWorks dependency) should prefer the `SW2URDF.Tests.Unit` project:
```bash
cd SW2URDF.Tests.Unit
dotnet test
```

To run all tests (including SW-dependent):
```bash
# Via TestRunner:
TestRunner\bin\Debug\TestRunner.exe

# Or in Visual Studio Test Explorer (SolidWorks must be running)
```

## Pull Request Process

1. Branch from `dev`
2. Implement changes following coding standards
3. Add or update tests as appropriate
4. Verify all tests pass (non-SW tests via `dotnet test`, SW tests via TestRunner)
5. Submit PR against `dev`
6. Code review required before merge

## Build Configuration Matrix

| Configuration | Platform | COM Registration | TLB Output | Use Case |
|---|-----------|-----------------|------------|----------|
| Debug | x64 | Yes (RegAsm) | No | Local development |
| Release | x64 | No | Yes (.tlb) | Distribution build |
| Test | x64 | Yes (RegAsm) | No | Test execution |

## Performance Notes

- The `ExportHelper` class is large (~2000 lines). When modifying it, prefer extracting helper classes
- STL preference management should use `STLPreferenceManager` (injectable via `ISolidWorksSession`)
- Avoid adding new SW COM type references to the URDF model layer (`SW2URDF/URDF/`)

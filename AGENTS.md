# Wildfire

- Keep `Wildfire.Core` host-agnostic. The simulator owns fire rules; Timberborn translates world inputs and applies outputs through adapter contracts.
- Use Bun and .NET 10. Development commands are in [README.md](README.md).
- Native tests require Timberborn assemblies; shader tests require licensed Unity. See [validation commands](docs/TEST_PLAN.md).
- [GitHub Issues](https://github.com/Ustice/Timberborn-Wildfire/issues) is the backlog.
- One agent controls the shared Timberborn session at a time. Check the QA lock and current process state; use disposable or copied saves for experiments.

Project knowledge: [design](docs/DESIGN.md), [architecture](docs/ARCHITECTURE.md), [source map](docs/source-map.md), and [technical references](docs/INDEX.md).

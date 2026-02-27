# DocVault

DocVault is a monolith-first document repository with ingestion, search, and background indexing. This repo scaffolds .NET 10 / C# 14 projects with minimal APIs, EF Core 10, and clean layering (Api, Application, Domain, Infrastructure, Tests).

## Getting Started
- Prereqs: .NET 10 SDK preview, Docker (optional for postgres), node not required.
- Restore: `dotnet restore`
- Build: `dotnet build`
- Run API: `dotnet run --project src/DocVault.Api`

## Projects
- Api: minimal API endpoints and middleware.
- Application: use cases, pipelines, abstractions.
- Domain: entities and events.
- Infrastructure: EF Core, storage, search.
- Tests: unit and integration shells.

## Docs
- See docs/system-design.md, docs/api.md, docs/data-model.md for design notes.

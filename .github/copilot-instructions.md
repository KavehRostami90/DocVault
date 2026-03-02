# GitHub Copilot Instructions

## Files and folders Copilot should ignore

Do NOT read, suggest edits to, or include content from:

- `secrets/` — contains local credentials and API keys
- `**/.env`, `**/.env.*` — environment variable files with secrets
- `**/appsettings.*.Local.json` — machine-local configuration overrides
- `**/Logs/` — Serilog rolling log files (`.log`, `.clef`)
- `**/bin/`, `**/obj/` — build output
- `**/TestResults/`, `**/coverage/` — test and coverage output
- `docker-compose.override.yml` — local Docker overrides

## Project conventions

- **Framework**: .NET 10 Minimal API, C# 13
- **Architecture**: Clean Architecture — Domain / Application / Infrastructure / Api layers
- **Logging**: Serilog with two-stage init (`CreateBootstrapLogger` → `UseSerilog`); use `[LoggerMessage]` source-generated methods in `partial` classes
- **Error handling**: `GlobalExceptionHandler : IExceptionHandler`; all exceptions map to `application/problem+json` via `IProblemDetailsService`
- **Validation**: FluentValidation via `ValidationFilter`; JSON type errors via `JsonValidationBinder` + `JsonBindingException`
- **Error codes**: nested static classes in `ErrorCodes` (`ErrorCodes.BadRequest.VALIDATION_FAILED`, etc.)
- **Package versions**: managed centrally in `Directory.Packages.props` — never add `Version=` to `<PackageReference>` in `.csproj` files
- **Constants**: `UPPER_SNAKE_CASE`
- **No inline TODO comments** — raise issues instead

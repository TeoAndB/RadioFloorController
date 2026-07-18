# AGENTS.md

Guidance for AI coding agents working in this repository.

## Project

RadioFloorController is an ASP.NET Core minimal API on .NET 9. It is currently
at the initial scaffold stage (single `Program.cs`, no controllers/services
split yet, no persistence layer).

## Build / run / test

```bash
dotnet restore
dotnet build
dotnet run
dotnet test          # no test project exists yet
```

Docker:

```bash
docker compose build
docker compose up
```

The container exposes ports 8080 and 8081 (see [Dockerfile](Dockerfile)).

## Structure

- [Program.cs](Program.cs) — app entry point and all endpoint definitions (minimal API style, not MVC controllers).
- [RadioFloorController.csproj](RadioFloorController.csproj) — target framework `net9.0`, nullable reference types and implicit usings enabled.
- [appsettings.json](appsettings.json) / [appsettings.Development.json](appsettings.Development.json) — configuration.
- [compose.yaml](compose.yaml) / [Dockerfile](Dockerfile) — container build/run.

## Conventions

- Nullable reference types are enabled (`<Nullable>enable</Nullable>`) — treat nullability warnings as real.
- Prefer extending the existing minimal-API style in `Program.cs` (or splitting into grouped endpoint files) over introducing MVC controllers, unless the user asks for that shift explicitly.
- No test project exists yet; if adding one, follow standard .NET conventions (`RadioFloorController.Tests`, xUnit).

## Available subagents

- `.claude/agents/dotnet-build-checker.md` — builds the solution/tests and reports errors concisely.
- `.claude/agents/csharp-code-reviewer.md` — reviews C# diffs for correctness, security, and idiomatic .NET 9 minimal-API style.
- `.claude/agents/dotnet-minimal-api-builder.md` — implements new REST endpoints (routes, DTOs, route groups) in minimal-API style.
- `.claude/agents/floor-control-logic-builder.md` — implements floor-control logic (request/grant/release/revoke, mutual exclusion, timeouts) for the radio call management domain.
- `.claude/agents/postgres-db-integration-builder.md` — adds PostgreSQL persistence (EF Core DbContext/entities/migrations) and a docker-compose Postgres service for local dev.
- `.claude/agents/backend-task-planner.md` — decomposes multi-part instructions and delegates subtasks to the other subagents in dependency order.

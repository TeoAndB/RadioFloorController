# AGENTS.md

Guidance for AI coding agents working in this repository.

## Project

RadioFloorController is an ASP.NET Core minimal API on .NET 9 implementing a
**Floor Control API** for a simplified radio group call management system:
only one user may hold the "floor" (the right to transmit) for a given radio
group at a time. See [floor-control-logic-builder.md](.claude/agents/floor-control-logic-builder.md)
for the domain background.

### API

- `POST /groups/{groupId}/floor` — body `{"userId": "..."}`. 200 if obtained
  (including idempotent re-obtain by the current holder), 400 if `userId` is
  missing/blank, 409 if another user currently holds the floor.
- `DELETE /groups/{groupId}/floor/{userId}` — 200 if released, 403 if that
  user does not currently hold the floor for the group.

Floor state is persisted per group in PostgreSQL (table `FloorGrants`:
`GroupId` PK, `HolderUserId` nullable, `ObtainedAt` nullable) — see
[Data/AppDbContext.cs](Data/AppDbContext.cs) and
[Services/FloorControlService.cs](Services/FloorControlService.cs). Mutual
exclusion is enforced by the database via an atomic conditional `UPDATE`
(`ExecuteUpdateAsync` with the ownership check in its `WHERE` clause) plus a
bounded retry loop for the first-obtain-on-a-group race — see the XML doc
comment on `FloorControlService` for details.

## Build / run / test

**Easiest — Docker Compose** (starts Postgres + the API together, applies EF
Core migrations automatically on startup):

```bash
docker compose up --build
```

The API is then reachable at `http://localhost:8080` (matches the OpenAPI
spec's `servers` entry), e.g.:

```bash
curl -X POST http://localhost:8080/groups/group-alpha-123/floor \
  -H "Content-Type: application/json" -d '{"userId":"user-456"}'

curl -X DELETE http://localhost:8080/groups/group-alpha-123/floor/user-456
```

**Without Docker** — run a local Postgres (or point `ConnectionStrings:Default`
in [appsettings.json](appsettings.json) at one you already have) and:

```bash
dotnet restore
dotnet build
dotnet run           # applies pending EF Core migrations on startup
dotnet test          # no test project exists yet
```

The `postgres`/`postgres` credentials in [compose.yaml](compose.yaml) and
`appsettings.json` are **local dev-only defaults** — not for any shared or
production environment.

## Structure

- [Program.cs](Program.cs) — app entry point, DI registration, and the Floor Control endpoint definitions (minimal API style, not MVC controllers).
- [Domain/](Domain) — `FloorObtainResult` / `FloorReleaseResult`, explicit closed-hierarchy result types (no exceptions for expected outcomes like "floor busy").
- [Services/](Services) — `IFloorControlService` / `FloorControlService`, the floor-control domain logic and its concurrency handling.
- [Data/](Data) — `AppDbContext`, `FloorGrantEntity`, and EF Core migrations under `Data/Migrations`.
- [RadioFloorController.csproj](RadioFloorController.csproj) — target framework `net9.0`, nullable reference types and implicit usings enabled.
- [appsettings.json](appsettings.json) / [appsettings.Development.json](appsettings.Development.json) — configuration, including the local dev Postgres connection string.
- [compose.yaml](compose.yaml) / [Dockerfile](Dockerfile) — container build/run for both the API and its Postgres dependency.

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

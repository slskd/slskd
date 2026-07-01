# Copilot instructions for slskd

## Build, test, and lint commands

- Prerequisites: .NET SDK 10.0, Node.js 18+ with npm 9.6+.
- Full local build and tests on Windows: `.\bin\build.ps1`. Use `-DotnetOnly`, `-WebOnly`, or `-SkipTests` to narrow scope.
- Full local build and tests in bash/Git Bash/WSL: `./bin/build`. Use `--dotnet-only`, `--web-only`, or `--skip-tests`.
- Backend build only: `dotnet build .\src\slskd\slskd.csproj`.
- Backend unit tests: `dotnet test .\tests\slskd.Tests.Unit\slskd.Tests.Unit.csproj`.
- Single backend test/class: `dotnet test .\tests\slskd.Tests.Unit\slskd.Tests.Unit.csproj --filter "FullyQualifiedName~FileServiceTests"` or filter by method name.
- Backend formatting/lint check: `./bin/lint` in bash, or `dotnet format --verbosity normal --verify-no-changes`.
- Backend format fix: `./bin/lint --fix` in bash, or `dotnet format --verbosity normal`.
- Coverage: `./bin/cover` in bash, which runs `dotnet test -p:CollectCoverage=true -p:CoverletOutputFormat=lcov -p:CoverletOutput=TestResults/lcov.info`.
- Web dependencies: run `npm ci` from `.\src\web` before web build/test/lint.
- Web build: `npm run build` from `.\src\web`.
- Web tests: `npm run test-unattended` from `.\src\web`.
- Single web test file: `npm test -- --watchAll=false searches.test.js` from `.\src\web`.
- Web lint: `npm run lint` from `.\src\web`; use `npm run lint:fix` to apply fixes.
- Development watch: `./bin/watch` for backend; `./bin/watch --web` for the React app after the backend is running.

## Architecture

- slskd is a client-server Soulseek application. The backend is a .NET 10 ASP.NET Core Web API in `src\slskd`; the frontend is a React 16/CRACO app in `src\web`. Production builds copy `src\web\build` into `src\slskd\wwwroot`, and the .NET app serves the static UI unless running headless.
- `Program.cs` owns startup: command-line/environment/YAML/default configuration, service registration, SQLite initialization, authentication, API versioning, SignalR hubs, static web content, Swagger, health checks, and the ASP.NET middleware pipeline.
- `Application.cs` is the hosted runtime coordinator. It subscribes to Soulseek client events, connection/state changes, transfer/search/share/message events, and publishes updates through services and SignalR hubs.
- Feature areas are organized by domain under `src\slskd` (`Search`, `Transfers`, `Shares`, `Messaging`, `Users`, `Files`, `Telemetry`, `Relay`, `Integrations`, `Events`, `Core`, `Common`). API controllers live in each domain's `API` subfolder, DTOs in `API\DTO`, SignalR hubs in `API\Hubs`.
- Persistent data is split across SQLite databases named by `Database.List`: `search.db`, `transfers.db`, `messaging.db`, and `events.db`. EF Core `DbContext` factories are registered per database; Dapper is also used where appropriate.
- Database migrations are explicit classes under `src\slskd\Core\Data\Migrations`. New migrations implement `IMigration`, use the `Z<YYYY_MM_DD>_<ShortDescription>Migration` naming pattern, must be idempotent, and must be registered in `Migrator.Migrations`.
- Configuration is modeled in `Core\Options.cs`. Defaults, environment variables, YAML, and command line are layered in precedence order. Inject `OptionsAtStartup` for values marked or treated as restart-only; use `IOptionsMonitor<Options>` in singleton services and `IOptionsSnapshot<Options>` in scoped/transient contexts for live options.
- Application state uses `AddManagedState<State>()`, which registers mutator/monitor/snapshot abstractions. Use the monitor for singleton observers, snapshots for shorter-lived consumers, and `IManagedState<T>` when both reading and mutating state in the same component.
- The web app centralizes HTTP through `src\web\src\lib\api.js`, which configures axios with the API base URL, JSON content type, bearer token handling, and 401 logout behavior. SignalR clients are created through `src\web\src\lib\hubFactory.js`; top-level app state and routing are coordinated in `components\App.jsx`.

## Key conventions

- Backend files commonly use the repository copyright/SPDX header and file-scoped or nested `namespace slskd...` style already present in the surrounding file; match nearby files when editing.
- API routes use `[Route("api/v{version:apiVersion}/[controller]")]`, `[ApiVersion("0")]`, explicit `[Authorize(Policy = AuthPolicy.Any)]` or stricter policies, and explicit binding attributes such as `[FromRoute]`, `[FromQuery]`, `[FromBody]`, and `[FromServices]`. ASP.NET automatic model-state 400s and implicit service binding are disabled.
- Controllers return plain status results/messages rather than ProblemDetails; expected domain exceptions are translated to HTTP status codes locally and unexpected exceptions are logged with Serilog before returning 500.
- Relay-agent mode is guarded in many endpoints with `if (Program.IsRelayAgent) return Forbid();`; preserve this on endpoints that should not run in relay agents.
- Register new backend services in `Program.ConfigureDependencyInjectionContainer`; most domain services are singletons. Services that only subscribe to the event bus may need forced instantiation during startup, matching `ScriptService`, `WebhookService`, `VPNService`, and `TelemetryService`.
- Use `IHttpClientFactory` for outbound HTTP calls. The named client `Constants.IgnoreCertificateErrors` exists for the few paths that intentionally bypass certificate validation.
- JSON serialization uses camel-case-compatible ASP.NET defaults plus custom converters for IP addresses and string enums, and ignores nulls. SignalR JSON options mirror the same converters.
- Prefer domain extension methods for projections/copies between Soulseek types and slskd types, such as `WithSoulseekSearch`, `WithSoulseekTransfer`, `ToStatus`, `ToInfo`, and `ToStatistics`.
- Unit tests are xUnit tests in `tests\slskd.Tests.Unit`, often with Moq and AutoFixture. Test names use underscore-separated behavior names like `ListContentsAsync_Throws_ArgumentException_Given_Relative_Path`.
- Web formatting is governed by `.prettierrc`: single quotes, `singleAttributePerLine`, and `endOfLine: auto`. The React app uses class components in several core areas and Semantic UI React components; follow the existing component style in the folder being changed.

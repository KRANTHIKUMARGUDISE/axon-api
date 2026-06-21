# AXON API — Claude Context

## What this is
AXON is an AI-powered software delivery platform. This repo is the .NET 10 backend that drives pipeline orchestration, agent execution, and delivery tracking.

## Solution layout
```
Axon.API/            ← ASP.NET Core entry point (controllers, Program.cs)
Axon.Core/           ← Domain: models, enums, interfaces, DTOs (no infrastructure deps)
Axon.Infrastructure/ ← MongoDB repositories, JWT service
```

## Build & run
```
dotnet build          # must stay at 0 warnings, 0 errors
dotnet run --project Axon.API
```
Swagger UI at `https://localhost:7xxx/swagger` in Development.

## Configuration (appsettings.json)
```json
MongoDB.Uri                       mongodb://localhost:27017
MongoDB.DatabaseName              axon
Jwt.Secret                        (min 32 chars — change in prod)
Jwt.Issuer                        axon-api
Jwt.Audience                      axon-clients
Jwt.AccessTokenExpiryMinutes      15
Jwt.RefreshTokenExpiryDays        7
Cors.Origins                      ["http://localhost:3000", "http://localhost:5173"]
Marketplace.GithubRepo            owner/repo
Marketplace.GithubToken           (GitHub PAT — leave empty for public repos)
Marketplace.CacheTtlHours         6
```

## Sessions completed
| Session | What was built |
|---------|---------------|
| B1 | Project scaffold, `User` model, JWT auth (`/api/auth/login`, `/api/auth/refresh`, `/api/auth/me`), admin seed user |
| B2 | All domain models + enums, repository interfaces, stub MongoDB repositories, `MongoIndexInitialiser`, updated `Program.cs` DI |
| B3 | Blocks controller + `MongoBlockRepository` fully implemented, marketplace sync endpoint |
| B4 | Pipelines controller + `MongoPipelineRepository` fully implemented, DAG validator (`PipelineValidator`), pipeline DTOs |
| B5 | Deliveries controller + `MongoDeliveryRepository` fully implemented, delivery DTOs, `IDeliveryHub` interface + stub |
| B6 | Marketplace service implemented (GitHub API, rate-limit + 404 handling), `MarketplaceController` (`GET /browse`, `POST /import`), `MarketplaceSyncService` (background sync), `MarketplaceConfig` options, `ImportAgentRequest` DTO |
| B7 | Ownership consolidation: added `CreatedByName` to all models, added `OwnerTeam` to pipelines & deliveries, removed `AutonomyLevel` from delivery, added `RepoUrl` + `WorkspaceType` + nullable `WorkspacePath`/`TicketTitle`, added `GET /api/users/teams` + `PATCH /api/deliveries/{id}` endpoints |
| B8 | BuildingBlock artifact model refactor: renamed `Type` → `Role` (`BlockRole` enum), replaced `ExecutorType`/`AdapterType`/`CachedDefinition` with `SourceType`/`ArtifactName`/`Version`/`AgentRuntime`/`ArtifactFormat`/`CachedFiles`; added `SourceType` (Axon, Local, Marketplace, CDF), `AgentRuntime` (Axon, Claude, Codex), `ArtifactFormat` (Native, Skill), `CachedFile` model; data migration (idempotent) + seeding Axon blocks (ticket-fetcher, pr-creator); validation rules for Axon-block immutability; updated repository indexes for (ArtifactName, Version) uniqueness |

## Domain models (Axon.Core/Models/)
| File | Key fields |
|------|-----------|
| `User` | Id, Email, DisplayName, Team, Role(enum), PasswordHash, RefreshTokenHash, CreatedAt, LastLoginAt |
| `BuildingBlock` | Id, Name, Role(BlockRole), SourceType, ArtifactName, Version, AgentRuntime, ArtifactFormat, CachedFiles(List\<CachedFile\>?), EntryPointPath?, ContextRequirements, OutputSchema, Tags, RunCount, CreatedBy, CreatedByName, MarketplaceSource?, MarketplacePath?, MarketplaceVersion?, SyncStatus, IsActive, CreatedAt, UpdatedAt |
| `CachedFile` | RelativePath, Content |
| `PipelineDefinition` | Id, Name, Nodes(List\<PipelineNode\>), Edges(List\<PipelineEdge\>), Visibility, Version, CreatedBy, CreatedByName, OwnerTeam, TeamId, Tags |
| `PipelineNode` | Id, BlockId, Label, Position(NodePosition{X,Y}), TimeoutSeconds(120), ConfidenceThreshold(0.7) |
| `PipelineEdge` | Id, SourceNodeId, TargetNodeId, IsDefault, Condition(EdgeCondition?) |
| `Delivery` | Id, TicketId, TicketTitle?, PipelineId, PipelineSnapshot, RepoUrl, WorkspaceType, WorkspacePath?, Status, Steps(List\<DeliveryStep\>), Inputs(BsonDocument?), RetriedFromDeliveryId?, AttemptNumber(int, default 1), JobNumber(int), OwnerTeam, CreatedBy, CreatedByName, CurrentNodeId?, CreatedAt, UpdatedAt, StartedAt?, CompletedAt? |
| `DeliveryStep` | NodeId, BlockId, BlockName, Status(StepStatus), ContextSnapshot(BsonDocument?), IsContextTruncated(bool), ContextFileRef?, Output(AgentOutput?), StartedAt, CompletedAt |
| `AgentOutput` | Status(string), Confidence(float), Summary, Result(BsonDocument), HumanGateReason?, ErrorMessage?, IsTruncated(bool), OutputFileRef? |

Note: `Result`/`Inputs`/`ContextSnapshot` are `BsonDocument`, not `Dictionary<string,object>` —
the latter trips a `BsonSerializationException` on `System.Text.Json.JsonElement` values when
bound from a JSON request body. A `BsonDocumentJsonConverter` (registered globally in
`Program.cs`) makes `BsonDocument` round-trip as plain JSON over the API regardless.

`IsTruncated`/`OutputFileRef` (on `AgentOutput`) and `IsContextTruncated`/`ContextFileRef` (on
`DeliveryStep`) are DS1 (Large Output Storage) — populated entirely by `axon-desktop`'s
executor when a step's input/output exceeds 50KB; the backend just stores/returns them
(plus a 413 safety cap at 500KB on `POST /api/deliveries/{id}/steps`). See `axon-desktop/CLAUDE.md`
for the executor-side details.

`JobNumber` is a globally unique, atomically-incrementing integer (1, 2, 3...) assigned once
per delivery at creation via `IDeliveryRepository.GetNextJobNumberAsync()` — a raw `BsonDocument`
counter document (`{_id: "delivery_job_number", seq: N}`) in a `counters` collection, incremented
via `$inc` + upsert (the standard Mongo auto-increment pattern, since Mongo has no native one).
Desktop uses it to name the delivery's git branch: `axon/{osUsername}/{jobNumber}/{ticketId}`
(see `axon-desktop/CLAUDE.md` for the naming rationale). The actual created branch name is
persisted back via `PATCH /api/deliveries/{id}/status` (`Delivery.Branch`) — there's no separate
endpoint for it, it just rides along on the existing status-update call.

## Enums (Axon.Core/Enums/)
- `UserRole` — Admin, Member, Viewer
- `BlockRole` — Decision, Execution, Knowledge, Control, Utility, IO (renamed from `BlockType` in B8)
- `SourceType` — Axon, Local, Marketplace (Phase 2), CDF (Phase 2) [new in B8]
- `AgentRuntime` — Axon, Claude, Codex [new in B8]
- `ArtifactFormat` — Native, Skill [new in B8]
- `SyncStatus` — Synced, Stale, Missing, Local, NeverSynced
- `PipelineVisibility` — Personal, Team, Organisation
- `DeliveryStatus` — Pending, Running, Paused, Completed, Failed, Cancelled
- `StepStatus` — Pending, Running, Completed, Failed, Paused
- `WorkspaceType` — Default, Browse

## Interfaces (Axon.Core/Interfaces/)
```csharp
IUserRepository      // GetAllAsync, GetByEmailAsync, GetByIdAsync, GetByRefreshTokenHashAsync, CreateAsync, UpdateLastLoginAsync, UpdateRefreshTokenAsync
IBlockRepository     // GetAllAsync, GetByIdAsync, CreateAsync, UpdateAsync, DeactivateAsync, UpdateSyncAsync
IMarketplaceService  // BrowseAsync() → List<MarketplaceAgentDto>, FetchBlockDefinitionAsync(path) → string?
IPipelineRepository  // GetAllAsync(visibility?, userId?), GetByIdAsync, CreateAsync, UpdateAsync, DeleteAsync
IDeliveryRepository  // GetAllByUserAsync(userId, status?), GetByIdAsync(id, userId), CreateAsync, UpdateAsync, UpdateStatusAsync, AppendStepAsync, UpdateStepAsync, UpdateGateStatusAsync
IDeliveryHub         // SendGateApprovedAsync, SendGateRejectedAsync, SendStatusChangedAsync — stub in B5, SignalR in B7+
```

## Infrastructure (Axon.Infrastructure/)
- `MongoContext` — singleton; exposes `Users`, `Blocks`, `Pipelines`, `Deliveries` as typed `IMongoCollection<T>`
- `MongoIndexInitialiser` — static, called at startup; creates indexes (see below)
- `MongoUserRepository` — fully implemented
- `MongoBlockRepository` — fully implemented
- `MongoPipelineRepository` — fully implemented
- `MongoDeliveryRepository` — fully implemented
- `DeliveryHubStub` — implements `IDeliveryHub` as no-op; replace with real SignalR hub in B7
- `MarketplaceService` — implements `IMarketplaceService`; uses typed `HttpClient` against GitHub API; `BrowseAsync` lists root directories, `FetchBlockDefinitionAsync` fetches and concatenates `AGENT.md` + `prompt.md`; throws `MarketplaceUnavailableException` on rate-limit or network failure; missing `prompt.md` is handled gracefully
- `JwtService` — `GenerateAccessToken`, `GenerateRefreshToken`, `HashRefreshToken` (SHA-256), `ValidateAccessToken`
- `PipelineValidator` (Axon.Core/Services/) — DAG validation: start/end node detection, cycle detection (DFS), orphan reachability, edge reference integrity, block existence, default-edge constraint; registered as scoped in DI

## MongoDB indexes
| Collection | Index |
|------------|-------|
| users | unique on Email |
| blocks | Role + SyncStatus + IsActive; (ArtifactName, Version) compound index |
| pipelines | CreatedBy + Visibility |
| deliveries | CreatedBy + Status + CreatedAt desc |
| migrations_run | (internal tracking for idempotent migrations) |

## Delivery DTOs (Axon.Core/DTOs/Deliveries/)
| DTO | Purpose |
|-----|---------|
| `DeliverySummaryDto` | List view — Id, TicketId, TicketTitle?, PipelineId, PipelineName, Status, WorkspaceType, RepoUrl, StepCount, CompletedSteps, OwnerTeam, CreatedBy, CreatedByName, CreatedAt, CompletedAt |
| `DeliveryDetailDto` | Full detail — all of Summary plus Steps, PipelineSnapshot, WorkspacePath?, CurrentNodeId?, UpdatedAt |
| `CreateDeliveryRequest` | TicketId, TicketTitle?, PipelineId, RepoUrl, WorkspaceType (all required except TicketTitle) |
| `UpdateDeliveryRequest` | RepoUrl?, WorkspaceType?, WorkspacePath? (only when Status == Pending) |
| `UpdateStatusRequest` | Status, CurrentNodeId?, WorkspacePath?, TicketTitle? |
| `AppendStepRequest` | NodeId, BlockId, BlockName, Status, Output? |
| `GateDecisionRequest` | Approved (bool), Reason? |

## Deliveries access control
- All delivery endpoints scoped to requesting user (`CreatedBy == userId`)
- `GetByIdAsync` filters by both id and userId — no cross-user leakage
- Gate endpoints (approve/reject) use `CurrentNodeId` from the delivery record

## Pipeline DTOs (Axon.Core/DTOs/Pipelines/)
| DTO | Purpose |
|-----|---------|
| `PipelineSummaryDto` | List view — Id, Name, Description?, Tags, Version, VersionLabel?, Visibility, Nodes, Edges, NodeCount, OwnerTeam, CreatedBy, CreatedByName, CreatedAt |
| `PipelineDetailDto` | Full detail — all of Summary plus UpdatedAt, TeamId? |
| `CreatePipelineRequest` | Name, Description?, Tags, Visibility, Nodes, Edges (ownerTeam is server-derived from user.Team) |
| `UpdatePipelineRequest` | Same fields, all optional (patch-style) |
| `ValidateRequest` | Nodes, Edges — validates DAG without saving |
| `ValidateResponse` | IsValid (bool), Errors (List\<string\>) |

## Block DTOs (Axon.Core/DTOs/Blocks/)
| DTO | Purpose |
|-----|---------|
| `BlockSummaryDto` | List view — Id, Name, Description, Role, SourceType, ArtifactName, AgentRuntime, ArtifactFormat, Tags, RunCount, IsActive, CreatedBy, CreatedByName |
| `BlockDetailDto` | Full detail — all of Summary plus Version, ContextRequirements, OutputSchema, CachedFiles?, EntryPointPath?, MarketplaceSource?, MarketplacePath?, MarketplaceVersion?, SyncStatus, CreatedAt, UpdatedAt |
| `CreateBlockRequest` | Name, Description, Role, SourceType, ArtifactName, AgentRuntime, ArtifactFormat, ContextRequirements, OutputSchema, Tags, CachedFiles?(List\<CachedFileRequest\>), EntryPointPath?; rejects SourceType=Axon with 400 |
| `CachedFileRequest` | RelativePath, Content |
| `UpdateBlockRequest` | All fields optional; rejects updates to SourceType=Axon blocks with 403 |
| `ImportAgentRequest` | Path, Name, Description?, Role, Tags (creates Local blocks with marketplace metadata) |

## Pipeline access control
- Personal pipelines: only visible to/modifiable by creator
- Team + Organisation pipelines: visible to all authenticated users
- Modify/delete: creator or Admin role only
- Access check in controller; `GetByIdAsync` does not filter by user

## Marketplace (Axon.Core/Config/ + Axon.Infrastructure/Services/ + Axon.API/)
- `MarketplaceConfig` (Axon.Core/Config/) — bound from `Marketplace` config section via `IOptions<MarketplaceConfig>`
- `MarketplaceService` registered as typed `HttpClient` (`AddHttpClient<IMarketplaceService, MarketplaceService>()`)
- `MarketplaceSyncService` (Axon.API/Services/) — `BackgroundService`; runs every `CacheTtlHours`; fetches all `Synced+IsActive` blocks with a `MarketplacePath`, refreshes `CachedDefinition`, sets `SyncStatus=Synced` on success or `Stale` on error (never clears existing cache)
- `MarketplaceUnavailableException` — thrown by service on rate-limit (403/429) or network failure; controllers catch it and return 503
- `ImportAgentRequest` DTO (Axon.Core/DTOs/Blocks/) — `Path, Name, Description?, Type, AdapterType, Tags`
- `MarketplaceAgentDto` fields — `Name, Path, Description?, Version?, LastUpdated?`

## Program.cs startup order
1. Register controllers, Swagger, MongoDB singleton, all 4 repositories (scoped), JwtService (singleton), `PipelineValidator` (scoped), `IOptions<MarketplaceConfig>`, `MarketplaceService` as typed HttpClient, `MarketplaceSyncService` as hosted service, `DeliveryHubStub` as `IDeliveryHub` (scoped)
2. Configure JWT bearer auth + CORS
3. On app start: run `MongoIndexInitialiser.InitialiseAsync`, then seed admin user (`admin@axon.local` / `axon-admin`)

## Auth
- Access token: JWT, 15 min, claims: `sub`=userId, `email`, `role`, `jti`
- Refresh token: 64-byte random → base64, stored as SHA-256 hash in `User.RefreshTokenHash`
- Protected endpoints use `[Authorize]`; user id extracted via `ClaimTypes.NameIdentifier` or `"sub"`

## Conventions
- Models use `string Id` (not `ObjectId`) — new IDs generated with `ObjectId.GenerateNewId().ToString()`
- All timestamps are `DateTime` (UTC)
- No comments in code unless the why is non-obvious
- New repositories go in `Axon.Infrastructure/Repositories/`, implement the corresponding `Axon.Core/Interfaces/I*Repository.cs`
- Controllers go in `Axon.API/Controllers/`, route prefix `api/<resource>`
- Build must stay at 0 warnings before committing

## API Endpoints (B7 consolidation)
### Users
- `GET /api/users/teams` — returns sorted list of distinct, non-empty Team values from all users

### Deliveries  
- `GET /api/deliveries` — list all deliveries for current user (optional `?status=` filter)
- `GET /api/deliveries/{id}` — get delivery detail (user-scoped)
- `POST /api/deliveries` — create new delivery (requires pipelineId, ticketId, repoUrl, workspaceType)
- `PATCH /api/deliveries/{id}` — update delivery (only when Status == Pending; supports repoUrl, workspaceType, workspacePath)
- `PATCH /api/deliveries/{id}/status` — update status & optionally set workspacePath, ticketTitle, currentNodeId
- `POST /api/deliveries/{id}/steps` — append step to delivery
- `POST /api/deliveries/{id}/gate/approve` — approve human gate decision
- `POST /api/deliveries/{id}/gate/reject` — reject human gate decision (with optional reason)

### Pipelines
- `GET /api/pipelines` — list pipelines (optional `?visibility=` filter)
- `GET /api/pipelines/{id}` — get pipeline detail
- `POST /api/pipelines` — create pipeline (ownerTeam auto-set from user.Team)
- `PUT /api/pipelines/{id}` — update pipeline (creator or Admin only)
- `DELETE /api/pipelines/{id}` — delete pipeline (creator or Admin only)
- `POST /api/pipelines/{id}/validate` — validate pipeline DAG without saving

### Blocks
- `GET /api/blocks` — list blocks (with filters: role, syncStatus, search, isActive, tags)
- `GET /api/blocks/{id}` — get block detail
- `POST /api/blocks` — create block (rejects SourceType=Axon)
- `PUT /api/blocks/{id}` — update block (rejects updates to SourceType=Axon blocks with 403)
- `POST /api/blocks/{id}/sync` — no-op for SourceType=Axon/Local; fetches and caches marketplace definition for other sources
- `POST /api/blocks/{id}/deactivate` — deactivate block (rejects SourceType=Axon blocks with 403)

## B7 Consolidation summary
**Models updated:** BuildingBlock, PipelineDefinition, Delivery — all now have `CreatedByName` field  
**Delivery changes:** Removed `AutonomyLevel`, added `RepoUrl` (required), `WorkspaceType` (required), made `WorkspacePath` and `TicketTitle` nullable  
**Pipeline changes:** Added `CreatedByName`, `OwnerTeam` now set from creator's `User.Team`  
**Block changes:** Added `CreatedByName`, now tracked in DTO summary view  
**New endpoints:** GET /api/users/teams, PATCH /api/deliveries/{id}  
**Updated endpoints:** PATCH /api/deliveries/{id}/status now accepts and persists workspacePath & ticketTitle  

## B8 Consolidation summary  
**Enums renamed/added:** `BlockType` → `BlockRole`; new `SourceType` (Axon/Local/Marketplace/CDF), `AgentRuntime` (Axon/Claude/Codex), `ArtifactFormat` (Native/Skill)  
**Models updated:** BuildingBlock completely refactored — removed `ExecutorType`, `AdapterType`, `CachedDefinition`, `CachedAt`; added `SourceType`, `ArtifactName`, `Version`, `AgentRuntime`, `ArtifactFormat`, `CachedFiles` (List<CachedFile>?), `EntryPointPath`  
**New models:** `CachedFile` (RelativePath, Content)  
**Validation rules:** Axon blocks (SourceType=Axon) are immutable — reject PUT/DELETE with 403; reject creation of Axon blocks via API (403); Local blocks require exactly one CachedFile matching EntryPointPath; unique compound index on (ArtifactName, Version)  
**Data migration:** One-time idempotent migration on startup; existing blocks migrated to Local (SourceType=Local, AgentRuntime=Claude, ArtifactFormat=Skill) or Axon (SourceType=Axon, AgentRuntime=Axon, ArtifactFormat=Native); Axon blocks seeded (ticket-fetcher, pr-creator)  
**DTOs updated:** `BlockSummaryDto` now shows Role/SourceType/ArtifactName/AgentRuntime/ArtifactFormat; `CreateBlockRequest` rejects SourceType=Axon; `ImportAgentRequest` updated to use Role instead of Type/AdapterType  
**POST /api/blocks/sync behavior:** No-op for SourceType=Axon/Local (returns 200 with message); fetches marketplace for other sources (Phase 2 deferred)  

## What comes next (B9+)
- **B9:** SignalR hub — replace `DeliveryHubStub` with real SignalR hub wired to `IDeliveryHub`
- **Phase 2:** Marketplace/CDF import logic (SourceType=Marketplace/CDF), version-bump endpoint, reference-file injection into prompts, D6 Engine execution code (reads CachedFiles[EntryPointPath] for prompts)

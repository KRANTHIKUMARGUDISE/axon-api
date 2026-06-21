# Session: BuildingBlock Artifact Model Refactor (Backend)

## Do not explore
Read only: `Axon.Core` domain models for BuildingBlock, the BuildingBlock MongoDB repository in
`Axon.Infrastructure`, the Blocks controller in `Axon.API`, and any DTOs/mappers referencing
`BuildingBlock`, `ExecutorType`, `AdapterType`, `CachedDefinition`. Do not explore the rest of the
solution. Do not touch Pipelines, Deliveries, or Engine execution code except where explicitly
listed under "Execution touch points" below — this session is schema and API surface only.

## Context

BuildingBlock currently models execution mechanics with three separate fields —
`ExecutorType` (free text), `AdapterType` (enum: claude-skill|mock|native|legacy-json), and
`CachedDefinition` (single string) — that were designed before AXON had more than one way to
source or run a block. We're collapsing these into a clearer "Artifact" concept, and separating
it from the existing "Role" concept (the old `Type` field: Decision/Execution/Knowledge/Control/
Utility/IO), which is unchanged and stays as-is, just renamed for clarity in display contexts.

Two new ideas this session introduces:
1. **Source Type** — where the artifact comes from. `Axon` (predefined, system-seeded, immutable)
   or `Local` (engineer-uploaded). `Marketplace` and `CDF` are reserved enum values for Phase 2 —
   add them to the enum now so the API contract doesn't need to change later, but no fetch/import
   logic for them in this session.
2. **Multi-file artifacts** — a Skill (per the Agent Skills open standard) is a folder containing
   a SKILL.md (YAML frontmatter: name + description, then markdown instructions) plus optional
   bundled files (scripts/, references/, assets/). A single string field cannot represent this.
   `CachedDefinition` is replaced by a list of files with relative paths.

## Final schema for BuildingBlock

```csharp
public enum BlockRole          // renamed from BlockType — same values, same meaning, just renamed
{
    Decision, Execution, Knowledge, Control, Utility, IO
}

public enum SourceType
{
    Axon,           // predefined, system-seeded, server-side immutable
    Local,          // engineer-uploaded
    Marketplace,    // reserved for Phase 2 — accepted by enum, no fetch logic this session
    CDF             // reserved for Phase 2 — accepted by enum, no fetch logic this session
}

public enum AgentRuntime
{
    Axon,           // fixed value when SourceType = Axon
    Claude,
    Codex           // reserved for future use, no execution logic this session
}

public enum ArtifactFormat
{
    Native,         // fixed value when SourceType = Axon — AXON's own hardcoded method
    Skill           // Agent Skills folder format (SKILL.md + optional bundled files).
                     // NOTE: do not call this "ClaudeSkillMarkdown" anywhere in code, comments,
                     // or DTOs. "Skill" is an open, cross-vendor format (agentskills.io), not
                     // Claude-specific. Name the enum value and any related types using "Skill"
                     // alone, not "ClaudeSkill".
}

public class CachedFile
{
    public string RelativePath { get; set; } = default!;   // e.g. "SKILL.md", "references/api-conventions.md"
    public string Content { get; set; } = default!;
}

public class BuildingBlock
{
    // unchanged
    public string Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public List<string> ContextRequirements { get; set; } = [];
    public List<string> OutputSchema { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public int RunCount { get; set; }
    public string CreatedBy { get; set; } = default!;
    public string CreatedByName { get; set; } = default!;

    // renamed
    public BlockRole Role { get; set; }                     // was: Type (BlockType)

    // new — replaces ExecutorType + AdapterType + CachedDefinition
    public SourceType SourceType { get; set; }
    public string ArtifactName { get; set; } = default!;    // dispatch key when SourceType=Axon,
                                                               // descriptive identifier when Local
    public int Version { get; set; } = 1;                    // generic; both source types use it
    public AgentRuntime AgentRuntime { get; set; }
    public ArtifactFormat ArtifactFormat { get; set; }
    public List<CachedFile>? CachedFiles { get; set; }        // null when SourceType=Axon
    public string? EntryPointPath { get; set; }               // e.g. "SKILL.md", null when SourceType=Axon

    // Marketplace/CDF — nullable, unused this session, preserved for Phase 2 per existing
    // "preserve structural seams" convention used elsewhere in this codebase
    public string? MarketplaceSource { get; set; }
    public string? MarketplacePath { get; set; }
    public string? MarketplaceVersion { get; set; }
}
```

### Fields removed entirely
- `ExecutorType` (string) — dispatch is now `SourceType` + `ArtifactName`
- `AdapterType` (enum) — derived from `ArtifactFormat` + `AgentRuntime` at execution time, not stored
- `CachedDefinition` (string) — replaced by `CachedFiles` (list) + `EntryPointPath`

Do not keep these as deprecated/nullable leftover fields. This is a pre-launch internal refactor
with no external consumers of the old shape (confirm by checking for any other repo or service
referencing these names before deleting — if found, stop and report rather than guessing intent).

## Validation rules to enforce server-side

These are invariants, not UI hints — enforce them in the API/domain layer so a direct API call
can't violate them:

- If `SourceType == Axon`: `AgentRuntime` must be `Axon`, `ArtifactFormat` must be `Native`,
  `CachedFiles` must be null, `EntryPointPath` must be null. Reject creation/update requests that
  violate this with a clear 400 error naming the conflicting field.
- If `SourceType == Local`: `ArtifactFormat` must be `Skill` for this session (no other local
  format is supported yet, but don't hardcode this as an unchangeable assumption — validate
  against the enum, not a literal). `CachedFiles` must be non-empty and must contain exactly one
  file whose relative path matches `EntryPointPath`.
- Blocks with `SourceType == Axon` must reject `PUT` (update) and `DELETE` requests at the API
  layer regardless of caller — return 403 with a message indicating system blocks are immutable.
  This is the actual enforcement point; there is no `IsSystemBlock` flag, the check is
  `block.SourceType == SourceType.Axon`.
- `(ArtifactName, Version)` should be unique together — add a compound index in Mongo and
  surface a clear conflict error on violation, not a raw duplicate-key exception.

## Predefined Axon blocks — seeding

Seed these as `SourceType = Axon` blocks on startup (idempotent upsert by `ArtifactName`, so
re-running the seeder doesn't duplicate or error):

- `ticket-fetcher` — Role: IO, Version: 1
- `pr-creator` — Role: Execution, Version: 1

Use whatever existing IDs/data these blocks already have in Mongo today if they exist (check the
Building Blocks screen seed/fixture data referenced in the project — Ticket Fetcher and PR Creator
already appear in the current UI per existing screenshots, so this is very likely a data migration
of existing documents rather than fresh inserts). If existing documents are found with the old
schema shape, migrate them in place: set `SourceType=Axon`, `ArtifactName` to a slugified version
of their current name, `AgentRuntime=Axon`, `ArtifactFormat=Native`, `Version=1`, and null out
`CachedFiles`/`EntryPointPath`. Do not create duplicates.

For any other existing BuildingBlock documents in Mongo with the old schema (RRA Assessment,
Code Generator, human-gate, etc. — these appear to be `Local` test/demo blocks per current
screenshots): migrate them to `SourceType=Local`, `ArtifactName` = slugified name,
`AgentRuntime=Claude`, `ArtifactFormat=Skill`, `Version=1`. For `CachedFiles`, if the old
`CachedDefinition` string had content, wrap it as a single `CachedFile` with
`RelativePath="SKILL.md"` and that content; if it was null/empty, leave `CachedFiles` as an empty
list and flag in your session summary that these blocks will need a real artifact uploaded via
the new UI before they can execute.

Write this migration as a one-time startup migration step (versioned, so it only runs once) —
follow whatever migration pattern already exists in this codebase if one exists; if none exists,
add a simple `MigrationsRun` collection with a migration name/version to prevent re-running.

## API contract changes

Update `CreateBlockRequest` (and any corresponding `UpdateBlockRequest` if one exists) to match
the new schema:

```csharp
public class CreateBlockRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public BlockRole Role { get; set; }
    public SourceType SourceType { get; set; }
    public string ArtifactName { get; set; } = default!;
    public AgentRuntime AgentRuntime { get; set; }
    public ArtifactFormat ArtifactFormat { get; set; }
    public List<string> ContextRequirements { get; set; } = [];
    public List<string> OutputSchema { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public List<CachedFileRequest>? CachedFiles { get; set; }
    public string? EntryPointPath { get; set; }
}

public class CachedFileRequest
{
    public string RelativePath { get; set; } = default!;
    public string Content { get; set; } = default!;
}
```

`Version` is not in `CreateBlockRequest` — server always sets it to `1` on creation. There is no
version-bump endpoint or update-creates-new-version logic in this session; that's explicitly out
of scope (confirmed deferred — do not build it).

Reject creation attempts where `SourceType == Axon` from this endpoint entirely (403 or 400 —
your choice, but document which) since Axon blocks only come from the seeder, never from the API.

Update response DTOs (whatever currently serializes BuildingBlock to the frontend) to include all
new fields and exclude the removed ones. Check the Blocks controller for any other endpoints
(`GET /api/blocks`, `GET /api/blocks/{id}`, `POST /api/blocks/{id}/sync`,
`POST /api/blocks/{id}/deactivate`) and update their response shapes consistently. The `sync`
endpoint's behavior for `SourceType=Local`/`Axon` blocks (neither has anything to sync against in
this session) should be a no-op that returns success with a clear message, not an error — Phase 2
Marketplace sync will give it real behavior later.

## Execution touch points

D6 (Claude Integration) already exists and currently reads from `CachedDefinition` as a flat
string to build the prompt. Find that code path and update it to:
1. Read `CachedFiles`, locate the file matching `EntryPointPath`, use its content as the primary
   prompt/system content (same role `CachedDefinition` played before).
2. For any other files in `CachedFiles` beyond the entry point: do not inline them into the
   prompt this session. Leave a clear `// TODO` comment noting that reference-file injection
   (via a tool call or inline, depending on size) is a follow-up decision, not implemented here.
   The goal this session is just: don't break execution for single-file artifacts, and don't
   silently drop multi-file uploads either — store them, just don't wire them into the prompt yet.
3. For `SourceType=Axon` blocks (`ticket-fetcher`, `pr-creator`): confirm whatever dispatch
   mechanism currently exists (if D6 already special-cased these, find and preserve it; if D6
   only ever handled Claude-skill blocks and Axon-native blocks were dispatched elsewhere in the
   Engine, find that dispatch point and update it to switch on `ArtifactName` rather than whatever
   field it used before).

If you find the execution code relies on `AdapterType` to decide which path to take, replace that
branch with a derivation: `SourceType == Axon` → native dispatch by `ArtifactName`;
`SourceType == Local && ArtifactFormat == Skill` → Claude API path using `CachedFiles`.

## Testing / verification

- Confirm existing seeded/migrated blocks load correctly via `GET /api/blocks` with the new shape.
- Confirm a `POST /api/blocks` with `SourceType=Local`, one `CachedFile`, valid `EntryPointPath`
  succeeds.
- Confirm a `POST /api/blocks` with `SourceType=Axon` is rejected.
- Confirm `PUT`/`DELETE` against a seeded Axon block (e.g. `ticket-fetcher`) is rejected with 403.
- Confirm `(ArtifactName, Version)` uniqueness is enforced.
- Run whatever existing pipeline/delivery flows exercise blocks end-to-end (if a smoke-test
  delivery exists) to confirm execution still works post-migration for at least one Local block
  and one Axon block.

## Out of scope (explicitly deferred, do not build)
- Marketplace/CDF fetch or import logic
- Version increment/bump UI or endpoint
- Reference-file injection into prompts (tool-based or inline) beyond the entry point file
- Codex runtime execution logic (enum value exists, no dispatch code for it)

## Deliverable
At the end of this session, update `CLAUDE.md` in `axon-api` with the new BuildingBlock schema
(field names, enums, validation invariants) so the next session — UI changes — has an accurate,
zero-exploration-cost reference for what the API now accepts and returns.
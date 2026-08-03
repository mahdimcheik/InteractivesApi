# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

An ASP.NET Core (.NET 10) Web API starter: JWT + refresh-token auth on top of ASP.NET Identity, PostgreSQL via EF Core/Npgsql, and Razor-templated transactional mail. Single project (`BoilerPlateApi/`) in an `.slnx` solution. Extracted from a larger "bontechnicien" app, so some leftovers remain (see _Known broken/stale spots_).

## Commands

```powershell
dotnet build BoilerPlateApi.slnx
dotnet run --project BoilerPlateApi                  # https profile; Swagger UI at /swagger (Development only)
dotnet run --project BoilerPlateApi --launch-profile http

# EF Core (needs dotnet-ef: dotnet tool install --global dotnet-ef)
dotnet ef migrations add <Name> --project BoilerPlateApi
dotnet ef database update --project BoilerPlateApi
```

There is no test project. Migrations are applied automatically at startup (`db.Database.Migrate()` in `Program.cs`), so `database update` is rarely needed — just run the app.

Requires a reachable PostgreSQL instance before startup, otherwise the migrate-on-boot step throws.

## Configuration

Config comes from a **`.env` file, not appsettings**. `Program.cs` calls `DotNetEnv.Env.NoClobber().TraversePath().Load()` first thing; `appsettings.json` only holds logging/AllowedHosts.

Copy `BoilerPlateApi/.env.sample` → `BoilerPlateApi/.env` to bootstrap. Every variable is read through `Utilities/EnvironmentVaraibles.cs` (note the typo in the filename; the class is `EnvironmentVariables`) — a static class of properties with hardcoded dev fallbacks. **Add new settings there, never `Environment.GetEnvironmentVariable` at the call site**, and mirror them into `.env.sample`.

The DB connection string is composed from `DB_HOST/PORT/NAME/USER/PASSWORD` by `EnvironmentVariables.ConnectionString`.

## Architecture

**Layering**: Controller → Service (interface + impl in one file under `Services/`) → `MainContext` / Identity `UserManager`. Services are registered in `Program.cs` (`AddScoped<IAuthService, AuthService>`); there is no repository layer — services use `MainContext` directly.

**Response envelope**: every service method returns `Response<T>` (`Models/Responses/Response.cs`) carrying `Message`, `Status` (an HTTP code), optional `Data` and `Count`. Controllers do no status mapping of their own — they validate `ModelState`, call the service, and `return StatusCode(result.Status, result);`. Messages are user-facing **French**. Follow this pattern for new endpoints.

**Identity model**: `MainContext : IdentityDbContext<UserApp, RoleApp, Guid>` — Guid keys throughout. `UserApp` extends `IdentityUser<Guid>` with names, consents, `AuthProvider` (Local/Google), `ProviderKey`, timestamps, and an FK to the `StatusAccount` lookup.

**Fixed GUIDs**: `Utilities/HardCode.cs` holds stable role and account-status GUIDs plus role name constants. `MainContext.SeedLookups` seeds roles and statuses via `HasData` with a fixed `CreatedAt` so migrations stay deterministic. Seed data changes require a new migration. The super-admin _user_ is seeded separately at runtime in `Program.SeedSuperAdmin` from `SUPER_ADMIN_EMAIL/PASSWORD`.

**Entity conventions** (`Models/Interfaces/Interfaces.cs`): new entities derive from `BaseModel` (Guid `Id`, `CreatedAt`/`UpdatedAt`/`ArchivedAt` as `timestamp with time zone`) — `ArchivedAt` is a soft-delete marker, not a hard delete. Lookup/option tables derive from `BaseModelOption` (adds `Name`/`Color`/`Icon`).

**Auth flow**: `AuthService.IssueSession` is the single funnel for login, Google sign-in and refresh. It rotates the refresh token (one row per user, unique index on `UserId`, stored **SHA-256-hashed** via `Utilities/SecurityToken.cs`), sets an HttpOnly `refreshToken` cookie (`Secure`+`SameSite=None` in prod, relaxed to non-Secure+`Lax` in Development so localhost HTTP works), and returns a short-lived JWT. JWT issuer _and_ audience are both `API_BACK_URL`. Roles ride as `ClaimTypes.Role` claims; `CheckUser.GetUserFromClaim` resolves the current user **by email claim**.

Identity is configured with relaxed password rules (8 chars, no character-class requirements), 5-attempt/5-minute lockout, unique email, and `RequireConfirmedEmail = true` — so registration is unusable without working mail or a manual DB flip. Password-reset/confirmation tokens live 2 hours.

Email-existence probing is deliberately avoided: login and forgot-password return the same generic message whether or not the account exists. Preserve that when editing.

**Controllers** are `[Authorize]` at the class level with `[AllowAnonymous]` on the public endpoints — keep that default-deny shape. RAll responses must be of type `Response<T>` .

**CORS**: `Utilities/CorsHelper.cs` is a hardcoded localhost allowlist plus `API_FRONT_URL`/`API_BACK_URL`, wired as the default policy with `AllowCredentials` (required for the refresh cookie).

**Mail**: `Services/MailService.cs` renders `.cshtml` templates through a **static** RazorLight engine (shared compiled-template cache even though the service is Scoped) and sends via MailKit — port 465 ⇒ implicit TLS, otherwise STARTTLS. Send methods return `false` on failure rather than throwing; callers treat mail as best-effort (registration succeeds even if the confirmation mail doesn't). Template view-models are records in `Models/MailModels/MailClasses.cs`.

**Enums** serialize as strings (`JsonStringEnumConverter`) but persist as ints — `AuthProviderEnum`/`MediaTypeEnum` are marked append-only in `HardCode.cs`; do not reorder them.

**Storage (upload/download)**: `IStorageService` (`Services/StorageService.cs`) has two implementations — `LocalStorageService` (disk) and `S3StorageService` (any S3-compatible endpoint; SeaweedFS by default). **Both are registered at once**; `IStorageResolver.Resolve(provider)` picks per call, falling back to `STORAGE_PROVIDER`, so objects stored before a provider switch stay readable. `MediasController` exposes upload / url / exists / download / signed-file / delete.

The design is **stateless — no `Media` entity, no DbSet, no migration**. A storage key (`{visibility}/{folder}/{yyyy}/{MM}/{guid}{ext}`) is the only thing worth persisting; whichever feature uploads a file owns that key (`UserApp.ImgUrl` is the natural first consumer). Visibility is the key's first segment, so a key alone is enough to download, re-sign or delete.

- **Public** objects: Local → `wwwroot/uploads/{key}` served by `UseStaticFiles`; S3 → `S3_PUBLIC_URL/{key}` with a public-read ACL. Set `S3_PUBLIC_READ=false` when the gateway refuses anonymous GETs — public objects then fall back to signed URLs too.
- **SeaweedFS needs `-s3.config`** (`SeaWeed/s3.json`, wired in both compose files). Started with a bare `-s3` it has no identities and rejects *every* SigV4-signed request — which is all the AWS SDK sends — so uploads fail even though the gateway answers unsigned curl fine. The keys there must match `S3_ACCESS_KEY`/`S3_SECRET_KEY`. The `anonymous` identity is scoped `Read:medias/public/*`: prefix rules only match with a trailing `*` (a bare `Read:bucket/prefix` is compared for equality against `Read:bucket`), and that scope is what makes public URLs readable while `private/` stays closed.
- Storage failures are classified: endpoint unreachable → **503** `Stockage indisponible`; an error the gateway actually returned (bad credentials, missing permission) → **502** `Erreur du service de stockage`, with `ErrorCode`/message logged. Keep them apart — collapsing both into 503 makes a config error look like an outage.
- **Private** objects: Local → stored **outside wwwroot** (`storage/`) and reachable only via `GET /medias/file/{key}?exp=&sig=`, an `[AllowAnonymous]` route where the HMAC (`JWT_KEY`, see `StorageHelper.Sign/Verify`) *is* the credential; S3 → native presigned GET. Never move the private root under `wwwroot` — static files would serve it unsigned.
- **Validation** lives in `Utilities/StorageHelper.ValidateAsync` so both backends reject identically: size, extension allowlist, MIME/extension agreement, and a magic-number check (`Utilities/FileSignatures.cs`). An extension absent from `FileSignatures` is rejected even if allowlisted — add its signature there when extending `ALLOWED_EXTENSIONS`.
- `StorageHelper.IsValidKey` is the path-traversal gate; call it before touching any key from a request.
- Because there is no metadata, **there is no per-object ownership check** — any authenticated caller who knows a key can read it (keys are Guid-based, so unguessable) and `DELETE` is restricted to `SuperAdmin`/`Admin`. Add ownership at the feature that persists the key.
- Upload size is capped twice: the transport ceiling (`Program.cs`, `MAX_UPLOAD_SIZE_MB` + 1 MB headroom) and the service check. A body over the ceiling is rejected on `Content-Length` by a small middleware so "too big" always returns the same 413 envelope instead of an opaque model-binding 400.

**Background jobs (Hangfire)**: `Hangfire.AspNetCore` + `Hangfire.PostgreSql`, storing state in the *same* PostgreSQL database under its own schema (`HANGFIRE_SCHEMA`, default `hangfire`). The schema is created on first use (`PrepareSchemaIfNecessary`) — it is **not** part of the EF model, so job tables never appear in a migration.

- Job code lives in `Services/JobService.cs` behind `IJobService`. Always enqueue through the *interface* (`_jobs.Enqueue<IJobService>(j => j.Method(...))`) so a serialized job doesn't pin the implementation type. Jobs get a scoped DI container per execution, so injecting `MainContext` is safe.
- Recurring jobs are declared in one place: `Program.RegisterRecurringJobs`, called on every boot. `AddOrUpdate` is keyed by job id and idempotent, but deleting a registration there does not delete the row — remove it from the dashboard or via `IRecurringJobManager.RemoveIfExists`.
- `HANGFIRE_ENABLED` turns the whole thing off; `HANGFIRE_SERVER_ENABLED=false` keeps the client (enqueue works) but processes nothing, which is how a scaled-out API delegates to a single worker instance.
- The dashboard (`HANGFIRE_DASHBOARD_PATH`, default `/hangfire`) sits outside the JWT pipeline — a browser navigation can't carry a bearer token — and is gated by HTTP Basic in `Utilities/HangfireDashboardAuthorization.cs`. An empty `HANGFIRE_DASHBOARD_PASSWORD` means "unconfigured" and the filter then serves it in Development only; it never falls open in production.
- Dashboard mapping and recurring registration both hit storage, so they stay *after* the `db.Database.Migrate()` block in `Program.cs`.

## Known broken/stale spots

- `MailService`'s RazorLight file-system root is `Features/Mail/Templates`, but the templates live in `Utilities/Templates/` and the `.csproj` has no rule copying them to the output — mail rendering will fail at runtime.
- Ports disagree: `launchSettings.json` serves 7033/5268 while `.env.sample`/`CorsHelper` assume 7125/5275. `API_BACK_URL` is what signed local URLs are built from, so a mismatch yields links pointing at a port nothing is listening on.
- `MailClasses.cs` still declares models for templates from the origin project (case-status, invoice, document-reminder) that don't exist here. `BoilerPlateApi.http` still points at the scaffolded `/weatherforecast` endpoint.
- The `.csproj` carries packages nothing currently uses (ImageSharp, Magick.NET, QuestPDF).
- The AWS SDK v4 emits `https://` presigned URLs even when `ServiceURL` is plaintext and `UseHttp` is set; `S3StorageService.AlignScheme` rewrites the scheme back (safe — SigV4 signs the host, not the scheme). Presigned links remain host-bound, so `S3_SERVICE_URL`/`S3_PUBLIC_URL` must be the host clients actually reach.

# NEXTERP ERP - Project TODO & Documentation

> Last Updated: 2026-09-22

---

## ✅ 2026-09-22: CI's "Apply database migrations" step, actually fixed this time

The earlier CI fix today (net10 alignment) got past the `System.Runtime` load error,
but the very next push still failed at the same step with a *different* error:
`password authentication failed for user "postgres"` against
`Host=localhost;Port=5432;Database=erp_db`. `dotnet ef database update` doesn't go
through the app's normal `IConfiguration`/env-var pipeline — it uses
`DesignTimeDbContextFactory`, which only reads a connection string from its first
positional argument and otherwise silently falls back to its hardcoded default
(`erp_db`/`postgres`/`postgres`), which doesn't match the CI job's actual Postgres
service (`nexterp_ci`/`nexterp`/`nexterp_ci_password`) at all. Fixed
`.github/workflows/ci.yml` to pass `-- "$ConnectionStrings__DefaultConnection"`
explicitly. Verified locally end-to-end with the exact same env-var-driven command
CI runs (migrations, then starting the API, then a real `/health/live` hit) against
fresh Postgres+Redis containers before pushing, specifically so this wouldn't take a
third round-trip through CI to catch.

---

## 🔐 2026-09-22: Frontend was defeating the backend's own secure cookie auth

The backend already implemented proper `HttpOnly; Secure` cookie-based auth (see
`AuthController`) — but the frontend ignored it: it also read the raw access token out
of the JSON login response, stored it in `localStorage` (both an explicit
`nexterp_token` key and a second copy inside Zustand's persisted `nexterp-auth` state),
and sent it via an `Authorization: Bearer` header on every request. Since that header
takes priority over the cookie fallback, any XSS anywhere in the app could exfiltrate
the session via `localStorage.getItem('nexterp_token')` — the `HttpOnly` protection the
backend went to the trouble of setting up was never actually load-bearing.

- [x] `nextjs-frontend/src/lib/api.ts`: removed the request interceptor that read
  `nexterp_token` from `localStorage` and built the `Authorization` header; added
  `withCredentials: true` so the browser sends the `HttpOnly` cookies automatically.
  Backend already had `AllowCredentials()` in CORS, ready for this.
- [x] `nextjs-frontend/src/lib/store.ts`: stopped writing the token to `localStorage`
  and excluded it from Zustand's `persist` `partialize` — it no longer needs to leave
  the login response at all.
- [x] **Found while fixing this: the "Logout" command in the command palette
  (`CommandPalette.tsx`) never actually logged anyone out.** It tried to clear the
  auth cookies via `document.cookie = 'nexterp_token=; expires=...'` — a silent no-op,
  since `HttpOnly` cookies aren't visible to `document.cookie` at all, for reading or
  writing. It also never called the backend's logout endpoint, which is the *only* way
  to invalidate the `HttpOnly` cookies (`authApi.logout()` already existed for this but
  was never called from anywhere in the app). The sidebar's logout button had the same
  gap. Both now call `authApi.logout()` (which clears the cookies server-side and the
  local UI state) before navigating to `/login`. Consolidated the "clear server +
  clear local state" logic into `authApi.logout()` itself instead of duplicating it at
  both call sites.
- [x] **Caught by a follow-up `/code-review`, before this ever shipped:** the login/
  refresh cookies were set with `SameSite=Strict`. The production frontend
  (`*.vercel.app`) and backend (`*.railway.app`) are different registrable domains —
  a genuinely cross-site setup — and `Strict` (and `Lax`) cookies are never attached to
  cross-site XHR/fetch calls. Had this shipped as-is, removing the `Authorization`-
  header fallback above would have made production login *appear* to succeed (the
  `Set-Cookie` response still arrives) and then immediately loop back to `/login`,
  because the very next API call would go out with no cookie attached and get a 401 —
  invisible in local dev, where frontend and backend are both `localhost` (same-site).
  Changed to `SameSite=None` (requires, and already had, `Secure=true`); CORS's
  explicit origin allowlist plus `AllowCredentials()` is what actually keeps this
  restricted to the app's own frontend, same as before.
- [x] Verified for real, end-to-end, against a real Postgres+Redis+the actual running
  API (not mocked): login sets the cookies with `secure; samesite=none; httponly`;
  a request authenticated *only* by the cookie jar (no header) succeeds; a request with
  neither gets 401; logout returns 200 and empties the cookie jar; a subsequent request
  with the pre-logout cookie jar gets 401 (proving logout actually invalidates the
  session server-side, not just client-side).

---

## 🚨 2026-09-22: The backend has never actually been booted and run before today

Everything up to this point this session was verified via `dotnet build`/`dotnet test`
— never `dotnet run`. Actually running it against real Postgres+Redis (Docker, not
mocked) surfaced two crash-level bugs that predate this session entirely, plus fixed
two issues in the rate limiting work done earlier today. All of this was verified by
actually booting the app and hitting real endpoints with curl, not just reading code.

### Fixed
- [x] **The API crashed on startup in `Development` — every time, unconditionally.**
  `RateLimitingMiddleware` constructor-injected `IRateLimitService` (a Scoped service)
  directly; ASP.NET Core constructs middleware once from the root container, so this
  is invalid and ASP.NET Core's Development-mode scope validation refuses to start:
  `Cannot resolve scoped service 'IRateLimitService' from root provider`. This has been
  there since the initial commit. It didn't crash in `Production` (scope validation is
  off there by default) — but that meant the middleware silently ran as a de-facto
  singleton instead, sharing one rate-limit-service instance across every request for
  the process lifetime. **This is exactly the environment `ci.yml`'s E2E job uses**
  (`ASPNETCORE_ENVIRONMENT: Development`), so the "Start backend API" step would have
  failed immediately once the earlier CI fix got it past the migration step — the CI
  fix earlier today was necessary but not sufficient. Fixed by moving the dependency
  from the constructor to an `InvokeAsync(HttpContext, IRateLimitService)` parameter,
  which ASP.NET Core correctly resolves from the per-request scope. Verified: app now
  boots and serves `/health/live` in Development against real Postgres/Redis.
- [x] **Rate limiting never actually distinguished authenticated from anonymous
  callers.** `app.UseRateLimiting()` ran *before* `app.UseAuthentication()`, so
  `context.User.Identity.IsAuthenticated` was always false when the middleware read
  it — every request, logged in or not, was keyed by IP and capped at the 100/min
  anonymous limit; the 1000/min authenticated limit was dead code. Reordered so
  rate limiting runs after authentication (but still before authorization). Verified
  with curl: an authenticated request now gets `X-RateLimit-Limit: 1000`, an
  anonymous one gets `100`.
- [x] **`RedisRateLimitService`'s reset-time calculation was wrong.** Its Lua script
  returned `windowStart + windowSeconds`, which algebraically equals `now` — so
  `X-RateLimit-Reset` (and thus any client's `Retry-After` logic) always claimed the
  limit resets *immediately*, never in the future. Fixed to `now + windowSeconds`
  (matching `InMemoryRateLimitService`'s already-correct semantics). Verified against
  a real Redis container: reset time now lands 5s in the future for a 5s window, not
  at "now".
- [x] **A migration generated earlier today needed reconciling before it could be
  trusted.** Adding the `Money` EF Core value converters (see the entry below) forced
  EF to notice the model had *other*, unrelated pending changes: `OrganizationModule.
  ModuleCode` was added to the domain entity in a prior, unrelated commit
  (`f0f045c`) with no migration ever generated for it — meaning **no migrated database
  has this column**, despite `EnableOrganizationModuleCommand`/
  `GetOrganizationModulesQuery` already depending on it. Generated and applied the
  missing migration (`20260922114419_AddOrganizationModuleModuleCodeColumn` — renamed
  from a misleading auto-generated name after confirming its actual content had
  nothing to do with Money). Verified: `dotnet ef database update` applies cleanly
  against real Postgres with zero remaining pending-model-changes warnings.

### Still true, unrelated to today: `ApplicationDbContext`'s design-time scan warning
`No instantiatable types implementing IEntityTypeConfiguration were found while
scanning assembly 'ERP.Infrastructure'` prints on every startup. Not investigated —
`OnModelCreating` clearly does have configuration (the Money/encryption converters,
global filters), just not via that specific interface/pattern EF's scanner expects.
Cosmetic unless something is actually silently missing its configuration; worth a
look if a decimal/PII column ever isn't behaving as configured.

---

## 🧱 2026-09-22: First real DDD Value Object (Money)

Both READMEs claimed `ValueObjects/`/`Events/` folders and Value Objects/Domain Events
as part of the architecture; neither ever existed (0 files). Rather than build unused
Domain Event plumbing speculatively, introduced one concrete, justified Value Object:

- [x] **`ERP.Domain/Common/ValueObjects/Money.cs`** — immutable, guarantees the
  "amount can't be negative" invariant that was previously re-checked by hand at every
  call site (e.g. `SalesOrderLine.Create`'s manual `unitPrice < 0` guard). Deliberately
  doesn't auto-round on construction (`Rounded()` is explicit) so introducing it can't
  silently change a previously-computed amount. `FromPersistedValue()` is a separate,
  non-validating factory used only by the EF Core read path, so a historical row that
  predates this invariant doesn't throw and break the whole query.
- [x] Applied it to **`SalesOrderLine`** (`UnitPrice`, `DiscountAmount`, `TaxAmount`,
  `LineTotal`) as the concrete example — chosen because nothing in
  `ERP.Application`/`ERP.API` reads these fields directly (verified by grep), so the
  change is fully contained to `ERP.Domain` + one EF Core value-converter registration
  in `ERPDbContext` (same schema, no migration, same pattern already used for the
  banking-PII encryption converter). `CalculateTotals()`'s arithmetic is untouched
  (still plain decimal math) — only wrapped into `Money` at the property boundary, so
  no computed amount changes for existing behavior.
- [x] A follow-up review of this same change caught two real issues, both fixed:
  `DiscountPercent` had no upper-bound validation (`CreateSalesOrderCommandValidator`),
  so a >100% discount could drive `LineTotal` negative and crash on `Money.Of()` inside
  `CalculateTotals()` instead of failing validation cleanly — added the missing
  `InclusiveBetween(0, 100)` rule. And the EF Core read converter originally called the
  validating `Money.Of()`, which would've thrown on any already-negative historical
  row the moment a future query does `.Include(Lines)` (none currently does) — switched
  the read direction to the new non-validating `FromPersistedValue()`.
- [x] 335 domain + 278 application tests pass (added `MoneyTests.cs` and
  `DiscountPercent` validator tests; updated `TestSalesEntity.cs`'s two assertions that
  read `SalesOrderLine.LineTotal`/`TaxAmount`/`DiscountAmount` as raw `decimal`).
- Not done, and not recommended without a concrete driver: reorganizing
  `ERP.Domain`/`ERP.Application`/`ERP.Infrastructure`/`ERP.API` into one folder per
  bounded context (vertical slices) instead of the current layer-per-project split.
  The current layering already enforces the DDD dependency rule correctly and is
  already sub-organized per module inside each layer — a full reorg would be a large,
  risky, mechanical refactor for no functional gain. Extending `Money` (or adding new
  Value Objects) to the other 16 files with money-like `decimal` fields is a reasonable
  next step if wanted, but should be done deliberately, module by module, not in bulk.

---

## 🔧 2026-09-22 Audit: CI Was Fully Broken + Live Security Gaps

Everything below `## 🎯 AI TASK QUEUE` in this file was mostly self-reported as
"COMPLETED"/"READY" by earlier passes. An independent audit (reading the actual code,
running the actual builds/tests, checking actual `gh run list` history) found that
**every CI and Deploy run on `master` had been failing** and several of the claimed
"COMPLETED" security items were not actually in effect. This section is ground-truth,
verified by running the build/tests, not self-reported.

### Fixed this pass
- [x] **CI was failing on every single push.** Root cause: `TargetFramework` had been
  bumped to `net10.0` across all main projects, but all three test projects
  (`ERP.Application.UnitTests`, `ERP.Domain.UnitTests`, `ERP.API.ContractTests`) were
  still `net8.0` (couldn't even restore — `NU1201`), and CI's `dotnet-ef` tool was
  pinned to `8.0.0` while `Microsoft.EntityFrameworkCore.Design` stayed on `8.0.0` too —
  so `dotnet ef database update` failed every run with
  `Could not load file or assembly 'System.Runtime, Version=10.0.0.0'`. Fixed by
  bumping all test projects to `net10.0`, upgrading EF Core/Npgsql/JwtBearer packages
  to `10.0.12`/`10.0.3`, and bumping CI's `dotnet-ef` tool to `10.0.12`
  (`.github/workflows/ci.yml`). **597 backend unit tests now build and pass** (0 could
  even compile before). Added a `backend-tests` CI job so this can't silently regress
  again.
- [x] **Frontend `jest` tests were never actually runnable.** `jest.config.js`,
  `jest.setup.js`, and 3 real test files existed, but `jest`/`ts-jest`/
  `jest-environment-jsdom`/`identity-obj-proxy`/`@testing-library/react`/
  `@testing-library/jest-dom` were never added to `package.json` — `npm run test:jest`
  just failed with `jest: not found`. Added the missing devDependencies, fixed
  `jest.setup.js` (had ESM `import` in a CommonJS-run file), and fixed one stale test
  assertion in `useDraftStorage.test.ts` that expected `localStorage.clear()` even
  though the real (safer) implementation only removes its own keys. **25/25 Jest tests
  now pass.** Added a Jest step to the CI `typecheck` job.
- [x] **Vulnerable dependencies.** Next.js `16.0.0–16.3.2` had a critical unauthenticated
  RCE advisory (GHSA-p293-qw3h-jr36, GHSA-2xp9-vwfh-vxw4) → bumped to `16.3.5`. The
  backend `Npgsql`/`SQLitePCLRaw`/`Microsoft.Extensions.Caching.Memory` NU1903
  high-severity advisories were transitive from EF Core `8.0.0` → resolved by the EF
  Core upgrade above. `npm audit --omit=dev` and `dotnet build` are both clean now.
- [x] **Cross-tenant IDOR/leak across the entire Roles + Users + Organization-modules
  surface.** `Role` and `User` are (intentionally, for login to work) excluded from
  `ERPDbContext`'s global tenant query filter, and the following handlers trusted a
  client-supplied `OrganizationId` (query string, path param, or body) instead of the
  authenticated session — meaning any authenticated "Admin" could read or modify
  **another organization's** roles/users/permissions/enabled-modules just by changing an
  ID in the request:
  - `CreateRoleCommand`, `UpdateRoleCommand`, `DeleteRoleCommand`,
    `AddRolePermissionsCommand`, `RemoveRolePermissionsCommand`, `GetRolesQuery`,
    `GetRoleByIdQuery` (`ERP.Application/Base/{Commands,Queries}/Roles/`)
  - `CreateUserCommandHandler`, `AssignUserRolesCommand`, `GetUserByIdQuery`,
    `GetUsersPaginatedQuery` (`ERP.Application/Base/{Commands,Queries}/Users/`) — the
    paginated list query was the worst of these: an **unfiltered `GET /api/v1/users`
    returned every user across every organization** in the platform.
  - `EnableOrganizationModuleCommand`, `DisableOrganizationModuleCommand`,
    `GetOrganizationModulesQuery` (`ERP.Application/Common/{Commands,Queries}/Organizations/`)
    — route allows role `Admin`, not just `SuperAdmin`, so any org's Admin could
    enable/disable/view another org's licensed modules via the URL.
  All now derive the organization from `ICurrentUserService` (SuperAdmin still permitted
  to act cross-org where that's the intended platform-admin behavior). This is the same
  bug class flagged as a partial fix in the previous "Security Hardening" pass below —
  that pass covered `CreateUserCommand`/`CreateRoleCommand`'s client-trust issue but not
  the sibling Update/Delete/permissions/list handlers, nor the module-management gap.
  Not exhaustively re-audited beyond this surface (Sales/Purchasing/Accounting/etc. were
  covered in the prior pass and weren't re-checked here).
- [x] **Demo admin account silently reset in production on every restart.**
  `ERP.API/Program.cs` unconditionally (re)created the `admin` user and reset its
  password hash to `DevPassword2024!` on *every* app startup in *every* environment
  unless `DEMO_PASSWORD` was set — and `DEMO_PASSWORD` was never in the documented
  Railway env var list, so production was very likely running with this known default
  password, silently re-applied on every redeploy even if someone had changed it. Now
  gated behind `IsDevelopment()` or an explicit `SEED_DEMO_DATA=true` **and**
  `DEMO_PASSWORD` opt-in (see `CLAUDE.md`).
- [x] **Employee banking PII stored in plaintext.** `Employee.BankName` /
  `BankAccountNumber` / `BankAccountName` were flagged
  `// SECURITY: should be encrypted` since day one but nothing implemented it. Added
  `IEncryptionService` (ASP.NET Core Data Protection, keys persisted to Redis so they
  survive Railway's ephemeral container redeploys — see `Program.cs`), wired into
  `ERPDbContext` as an EF Core value converter on those three columns. Verified
  encrypt/decrypt round-trips correctly. Old plaintext rows still read fine (decrypt
  falls back to passthrough on non-protected input) and get encrypted on next save — no
  data migration needed since the columns were already unbounded `text`.

### Follow-up code review (same day) caught a real regression in the above
- [x] **`GetUsersPaginatedQueryHandler` fail-open bug.** The IDOR fix above computed
  `organizationFilter = IsSuperAdmin ? request.OrganizationId : _currentUser.OrganizationId`
  and only applied a `.Where()` when it had a value — so a non-SuperAdmin whose token
  somehow carried no/an unparseable `org` claim got the query with **no org filter at
  all** (every user, every org) instead of being denied. Its sibling
  `GetUserByIdQueryHandler` already failed closed correctly (`user.OrganizationId !=
  _currentUser.OrganizationId` is `true` when the right side is `null`, since a real
  Guid never equals null) — this one didn't. Fixed to explicitly fail when a
  non-SuperAdmin has no `OrganizationId`.
- [x] **Dropped organization-existence check.** `CreateRoleCommandHandler` and
  `CreateUserCommandHandler` used to verify `_context.Organizations.AnyAsync(o => o.Id
  == request.OrganizationId && !o.IsDeleted)` before proceeding; deriving the org from
  `ICurrentUserService` instead of the request dropped that check entirely. Restored it
  against the trusted `organizationId` (not the client-supplied value) — closes the
  (narrow, ≤ access-token-lifetime) window where a user whose org gets soft-deleted
  mid-session could still create roles/users under it.
- [ ] **Not fixed, flagged for a deliberate follow-up:** the IDOR fix is ~10 handlers of
  copy-pasted `if (_currentUser.OrganizationId == null) return Failure(...)` boilerplate
  instead of one shared mechanism — which is exactly how the fail-open bug above crept
  in (one copy took a different, unsafe shape). This codebase already has a MediatR
  pipeline-behavior pattern for cross-cutting authorization
  (`PermissionAuthorizationBehavior`, `ModuleAuthorizationBehavior` in
  `ERP.Application/Common/Behaviors/`) — a `TenantScopeBehavior` following the same
  pattern would close this class of bug at the framework level instead of per-handler.
  Left as a recommendation rather than done here: it's an architecture change (new
  marker interface, touches every Role/User request DTO) that deserves its own review,
  not something to fold into a bug-fix pass.
- [ ] **Noted, not changed:** Redis connection setup went from a lazily-resolved DI
  factory to an eager `ConnectionMultiplexer.Connect()` at startup (needed so
  `PersistKeysToStackExchangeRedis` and the DI singleton share one instance). Since
  Redis is already required infrastructure here (caching, rate limiting, health checks
  all depend on it) this just moves an unavoidable connection cost earlier rather than
  introducing a new one — acceptable, but worth knowing if Railway/Redis cold-starts
  ever show up as slow app boot times.

### Repo cleanup (same day, on request)
- [x] **`ERP.API/erp.db` (440KB SQLite file) was committed to git**, and `.gitignore` had
  no `*.db` rule. Contents checked — just the same demo seed data (admin user + a
  handful of demo customers/suppliers), not real production data, but committing a
  binary DB file is bad practice regardless. Removed from git, added `*.db`/`*.sqlite`/
  `*.sqlite3` to `.gitignore`.
- [x] **Deleted `ERP.WebUI/` (legacy frontend, 2.1MB/77 files, untouched since the
  initial commit) and `docs/TODO_ROADMAP.md`** (superseded by this file, also untouched
  since the initial commit) — both confirmed dead by the user before deletion.
- [x] **`docker-compose.yml`/`docker-compose.prod.yml`'s frontend build was broken** —
  `context: .` with `dockerfile: nextjs-frontend/Dockerfile` means `COPY package*.json
  ./` in the Dockerfile looks for a lockfile at the repo root, which doesn't exist, so
  `npm ci` always failed. Verified with `docker build` (fails with the old config,
  succeeds after). Fixed to `context: ./nextjs-frontend` + `dockerfile: Dockerfile`.
  This wasn't caused by the `ERP.WebUI` deletion — it was already broken before, just
  pointed at a path that happened to still exist.
- [x] Fixed lingering `ERP.WebUI` references in `README_DEV.md`, `README.md`,
  `.claude/skills/{testing,frontend,architecture}.md`, `docs/RAILWAY_DEPLOYMENT.md`,
  `docs/TESTING_STRATEGY.md`, and a stale net8.0 comment in
  `.claude/hooks/session-start.sh` left over from before the CI fix above.

### Still broken / needs your action (couldn't fix without credentials or much bigger scope)
- [ ] **Railway auto-deploy is broken right now.** The `RAILWAY_TOKEN` GitHub secret is
  invalid/expired — `gh run view` shows `Invalid RAILWAY_TOKEN. Please check that it is
  valid and has access to the resource you're trying to use.` on the latest push. Needs
  a new token from the Railway dashboard, set via `gh secret set RAILWAY_TOKEN`.
- [ ] **`ERP.API.ContractTests` has never compiled, since its initial commit.** It
  referenced two NuGet packages that don't exist on nuget.org at all
  (`PactProviderVerifier`, `PactFlow.io`), and the test bodies are written against a
  PactNet API that doesn't match any released version (checked both 4.5.0 and 5.0.1 —
  same ~40 compile errors on both, e.g. `IPactBuilder.UponReceiving`/`.Build()` don't
  exist). Removed the two phantom packages and fixed the two easy issues (missing
  `FluentAssertions` reference, missing `using Xunit.Abstractions;`), but
  `Consumer/AuthConsumerContractTests.cs` and `Provider/ErpApiProviderContractTests.cs`
  need a real rewrite against PactNet 5.x's actual API to ever compile. Not wired into
  CI either way (`ci.yml` never references it), so it isn't currently blocking anything
  — just dead code.
- [ ] **Email/SMS notifications are stubs.** `NotificationGateway.cs` just logs and
  returns success — no SendGrid/Twilio/SES integration. Needs real provider API keys
  before this can be built for real.
- [ ] **No real-time notifications.** SignalR/WebSocket push was never implemented
  (frontend polls every 30s instead), despite being marked `⚠️ COMPLEX` (not `READY`)
  under Phase 12 below — that flag was accurate.
- [ ] The cross-tenant fixes above have no regression test coverage — there's no
  existing pattern in this repo for testing a MediatR handler against a mocked/in-memory
  `IApplicationDbContext` + `ICurrentUserService`, so none was added. Worth setting up.

---

## 🚀 AUTO-RUN COMMANDS

### 1. Docker Full Stack Start
```bash
cd "d:\RW\PROJECT RW\ERP"
docker-compose up -d
docker-compose logs -f
```

### 2. Backend Development
```bash
cd "d:\RW\PROJECT RW\ERP"
dotnet build ERP.API/ERP.API.csproj
dotnet run --project ERP.API
```

### 3. Frontend Development
```bash
cd "d:\RW\PROJECT RW\ERP\nextjs-frontend"
npm install
npm run dev
```

### 4. Storybook
```bash
cd "d:\RW\PROJECT RW\ERP\nextjs-frontend"
npm run storybook
```

### 5. E2E Tests
```bash
cd "d:\RW\PROJECT RW\ERP\nextjs-frontend"
npm run test
```

### 6. Build & Deploy
```bash
cd "d:\RW\PROJECT RW\ERP"
git add -A && git commit -m "message" && git push
```

---

## 🎯 AI TASK QUEUE

### Phase 6: User Experience Enhancements ✅ COMPLETED

#### Task 6.1: Global Search Enhancement ⭐ READY
```
Goal: Improve global search with fuzzy search, recent searches, and category filters

Files to create:
- nextjs-frontend/src/components/SearchModal.tsx (enhance CommandPalette)
- nextjs-frontend/src/hooks/useGlobalSearch.ts
- nextjs-frontend/src/stores/searchHistoryStore.ts
- nextjs-frontend/src/lib/search.ts (fuzzy search utilities)

Features:
- [x] Fuzzy search with Fuse.js
- [x] Recent searches (localStorage)
- [x] Search by category (Employees, Inventory, etc.)
- [x] Keyboard navigation
- [x] Highlight matched terms
```

#### Task 6.2: Notification Bell & Badge ⭐ READY
```
Goal: Add notification bell with unread count badge and dropdown

Files to create:
- nextjs-frontend/src/components/NotificationBell.tsx
- nextjs-frontend/src/stores/notificationStore.ts
- nextjs-frontend/src/hooks/useNotifications.ts
- nextjs-frontend/src/components/NotificationDropdown.tsx

Features:
- [x] Unread count badge
- [x] Notification dropdown list
- [x] Mark as read functionality
- [x] Notification types (info, warning, error, success)
- [x] Real-time updates (polling every 30s)
```

#### Task 6.3: Dark Mode Toggle ⭐ READY
```
Goal: Add permanent dark mode toggle with system preference detection

Files to create:
- nextjs-frontend/src/components/ThemeToggle.tsx
- nextjs-frontend/src/lib/theme-utils.ts
- Update: nextjs-frontend/src/app/layout.tsx

Features:
- [x] System preference detection (prefers-color-scheme)
- [x] Manual toggle button
- [x] Persist preference to localStorage
- [x] Smooth transition animation
- [x] Dark mode for all components
```

#### Task 6.4: Bulk Actions for Tables ⭐ READY
```
Goal: Add bulk select, bulk delete, bulk update for all tables

Files to create:
- nextjs-frontend/src/components/DataTableWithBulkActions.tsx
- nextjs-frontend/src/hooks/useBulkActions.ts

Features:
- [x] Checkbox selection column
- [x] Select all / Deselect all
- [x] Bulk delete confirmation
- [x] Bulk status toggle
- [x] Selected count indicator
```

#### Task 6.5: Inline Edit for Tables ⭐ READY
```
Goal: Enable inline editing for quick field updates

Files to create:
- nextjs-frontend/src/components/InlineEdit.tsx
- nextjs-frontend/src/hooks/useInlineEdit.ts

Features:
- [x] Double-click to edit
- [x] Enter to save, Escape to cancel
- [x] Optimistic update
- [x] Validation feedback
- [x] Edit history tracking
```

#### Task 6.6: Export Enhancements ⭐ READY
```
Goal: Add Excel export and column selection for exports

Files to create:
- nextjs-frontend/src/components/ExportDialog.tsx
- nextjs-frontend/src/lib/export.ts

Features:
- [x] Export to CSV
- [x] Export to Excel (.xlsx)
- [x] Column selection checkboxes
- [x] Date range filter
- [x] Custom filename
```

---

### Phase 7: Additional Module Pages ⭐ READY

#### Task 7.1: Employee Profile Page ⭐ READY
```
Goal: Create detailed employee profile page

Files to create:
- nextjs-frontend/src/app/dashboard/hrm/[id]/page.tsx
- nextjs-frontend/src/components/EmployeeProfile.tsx
- nextjs-frontend/src/components/EmployeeTimeline.tsx

Features:
- [x] Personal information section
- [x] Employment history
- [x] Documents list
- [x] Leave balance
- [x] Performance ratings
- [x] Edit profile modal
```

#### Task 7.2: Stock Adjustment & Transfer ⭐ READY
```
Goal: Add stock adjustment and warehouse transfer features

Files to create:
- nextjs-frontend/src/components/StockAdjustment.tsx
- nextjs-frontend/src/components/StockTransfer.tsx
- nextjs-frontend/src/app/dashboard/inventory/adjustments/page.tsx
- nextjs-frontend/src/app/dashboard/inventory/transfers/page.tsx

Features:
- [x] Stock count adjustment
- [x] Adjustment reasons (damaged, lost, found)
- [x] Warehouse-to-warehouse transfer
- [x] Transfer approval workflow
- [x] Adjustment history log
```

#### Task 7.3: PO Tracking Timeline ⭐ READY
```
Goal: Visual timeline for purchase order status tracking

Files to create:
- nextjs-frontend/src/components/POTimeline.tsx
- nextjs-frontend/src/components/PODetails.tsx

Features:
- [x] Visual status timeline (Draft → Submitted → Approved → Shipped → Received)
- [x] Expected delivery date
- [x] PO item details
- [x] Approval history
- [x] Notes/comments
```

#### Task 7.4: Journal Entry Preview ⭐ READY
```
Goal: Add journal entry preview and validation

Files to create:
- nextjs-frontend/src/components/JournalEntryForm.tsx
- nextjs-frontend/src/components/JournalEntryPreview.tsx

Features:
- [x] Debit/Credit balance validation
- [x] Preview before posting
- [x] Recurring journal templates
- [x] Journal approval workflow
```

#### Task 7.5: Financial Reports ⭐ READY
```
Goal: Add Balance Sheet and Profit & Loss reports

Files to create:
- nextjs-frontend/src/app/dashboard/accounting/reports/balance-sheet/page.tsx
- nextjs-frontend/src/app/dashboard/accounting/reports/profit-loss/page.tsx
- nextjs-frontend/src/components/ReportChart.tsx
- nextjs-frontend/src/components/ReportExport.tsx

Features:
- [x] Balance Sheet with assets, liabilities, equity
- [x] Profit & Loss statement
- [x] Date range selection
- [x] Period comparison
- [x] Export to PDF/Excel
```

#### Task 7.6: Audit Trail Page ⭐ READY
```
Goal: Create audit trail viewer for admin

Files to create:
- nextjs-frontend/src/app/dashboard/audit/page.tsx
- nextjs-frontend/src/components/AuditLogTable.tsx
- nextjs-frontend/src/components/AuditFilters.tsx

Features:
- [x] Searchable audit log
- [x] Filter by user, action, date range
- [x] Filter by entity type
- [x] Export audit log
- [x] Detail view with before/after values
```

#### Task 7.7: User Activity Log ⭐ READY
```
Goal: Track and display user session activities

Files to create:
- nextjs-frontend/src/app/dashboard/activity/page.tsx
- nextjs-frontend/src/components/ActivityTimeline.tsx

Features:
- [x] Last login timestamp
- [x] Pages visited
- [x] Actions performed
- [x] Session duration
- [x] Active now indicator
```

#### Task 7.8: Approval Workflow Visual ⭐ READY
```
Goal: Visual workflow builder for approval processes

Files to create:
- nextjs-frontend/src/app/dashboard/workflows/page.tsx
- nextjs-frontend/src/components/WorkflowBuilder.tsx
- nextjs-frontend/src/components/WorkflowStep.tsx

Features:
- [x] Drag-and-drop workflow steps
- [x] Approver assignment
- [x] Condition-based routing
- [x] Approval threshold (e.g., 2 of 3)
- [x] Deadline configuration
```

---

### Phase 8: Code Quality & Testing ⭐ READY

#### Task 8.1: Fix Error Handling ✅ COMPLETED
```
Goal: Fix all empty catch blocks and improve error handling

Files to check:
- nextjs-frontend/src/app/dashboard/**/*.tsx
- nextjs-frontend/src/hooks/*.ts
- nextjs-frontend/src/lib/*.ts

Changes:
- [x] Add console.error or toast for all catch blocks
- [x] Add error boundary per page
- [x] Centralize error messages
- [x] Add error recovery options
```

#### Task 8.2: API Response Types ⭐ READY
```
Goal: Centralize all API response types

Files to create:
- nextjs-frontend/src/types/api.ts
- nextjs-frontend/src/types/entities.ts

Features:
- [x] ApiResponse<T> generic type
- [x] PaginatedResponse<T> type
- [x] All entity types centralized
- [x] Export from single index.ts
```

#### Task 8.3: Jest Unit Tests ⭐ READY
```
Goal: Add Jest unit tests for hooks and utilities

Files to create:
- nextjs-frontend/src/**/*.test.ts
- nextjs-frontend/src/**/*.test.tsx
- nextjs-frontend/jest.config.js

Test coverage:
- [x] useAutoSave hook
- [x] useDraftStorage functions
- [x] cn() utility
- [x] Export utilities
- [x] Component snapshot tests
```

#### Task 8.4: Storybook Interaction Tests ⭐ READY
```
Goal: Add interaction tests to Storybook stories

Files to update:
- nextjs-frontend/src/components/**/*.stories.tsx

Interactions to test:
- [x] Button click handlers
- [x] Form validation
- [x] Loading states
- [x] Error states
- [x] User interactions
```

#### Task 8.5: Component Library Refactor ⭐ READY
```
Goal: Create reusable component library

Files to create:
- nextjs-frontend/src/components/ui/Button.tsx
- nextjs-frontend/src/components/ui/Input.tsx
- nextjs-frontend/src/components/ui/Select.tsx
- nextjs-frontend/src/components/ui/Dialog.tsx
- nextjs-frontend/src/components/ui/Card.tsx
- nextjs-frontend/src/components/ui/Badge.tsx

Features:
- [x] Consistent design tokens
- [x] Variant props (primary, secondary, danger)
- [x] Size variants (sm, md, lg)
- [x] Disabled states
- [x] Loading states
```

---

### Phase 9: DevOps & Infrastructure ⭐ READY

#### Task 9.1: Frontend Dockerfile ⭐ READY
```
Goal: Create production-ready Dockerfile for frontend

Files to create:
- nextjs-frontend/Dockerfile

Features:
- [x] Multi-stage build (builder → runner)
- [x] Node 22 Alpine
- [x] Non-root user
- [x] Health check
- [x] .dockerignore
```

#### Task 9.2: GitHub Actions CI/CD ⭐ READY
```
Goal: Add CI/CD pipeline for frontend

Files to create:
- .github/workflows/ci.yml
- .github/workflows/deploy.yml

Pipeline stages:
- [x] Lint check
- [x] TypeScript check
- [x] Unit tests
- [x] Build
- [x] E2E tests
- [x] Deploy to preview (PR)
- [x] Deploy to production (main)
```

#### Task 9.3: Pre-commit Hooks ⭐ READY
```
Goal: Add lint-staged for pre-commit validation

Files to create:
- .lintstagedrc

Features:
- [x] TypeScript check
- [x] ESLint fix
- [x] Prettier format
- [x] Commit message validation
```

#### Task 9.4: Sentry Integration ⭐ READY
```
Goal: Add error tracking with Sentry

Files to create:
- nextjs-frontend/src/lib/sentry.ts
- nextjs-frontend/sentry.client.config.ts
- nextjs-frontend/sentry.server.config.ts

Features:
- [x] Client-side error tracking
- [x] Server-side error tracking (if applicable)
- [x] Source maps upload
- [x] Performance monitoring
- [x] User context
```

---

### Phase 10: Documentation ⭐ READY

#### Task 10.1: Postman Collection ⭐ READY
```
Goal: Create comprehensive Postman collection for API testing

Files to create:
- docs/NEXTERP-API.postman_collection.json

Coverage:
- [x] All API endpoints
- [x] Auth flows (login, refresh, logout)
- [x] CRUD operations for all entities
- [x] Environment variables
- [x] Example responses
```

#### Task 10.2: Architecture Decision Records ⭐ READY
```
Goal: Document architectural decisions

Files to create:
- docs/adr/ADR-001-tech-stack.md
- docs/adr/ADR-002-authentication.md
- docs/adr/ADR-003-state-management.md
- docs/adr/ADR-004-database-design.md

Content:
- [x] Context and decision
- [x] Consequences
- [x] Alternatives considered
```

#### Task 10.3: CONTRIBUTING.md ⭐ READY
```
Goal: Create contribution guidelines

Files to create:
- CONTRIBUTING.md

Content:
- [x] Setup instructions
- [x] Coding standards
- [x] Git workflow
- [x] Testing requirements
- [x] Pull request template
```

#### Task 10.4: CHANGELOG.md ⭐ READY
```
Goal: Create standardized changelog

Files to create:
- CHANGELOG.md

Format:
- [x] Keep a Changelog standard
- [x] Semantic versioning
- [x] Categorized changes (Added, Changed, Deprecated, Removed, Fixed, Security)
- [x] Auto-generate from commits (conventional-changelog)
```

---

### Phase 11: Mobile & Performance ⭐ READY

#### Task 11.1: Mobile Responsive Audit ⭐ READY
```
Goal: Fix mobile responsiveness issues

Pages to audit:
- All dashboard pages
- Login page
- Modal dialogs
- Tables
- Navigation

Fixes:
- [x] Sidebar collapse on mobile
- [x] Table horizontal scroll
- [x] Modal full-screen on mobile
- [x] Touch-friendly buttons
- [x] Viewport meta tags
```

#### Task 11.2: Performance Optimization ⭐ READY
```
Goal: Optimize Core Web Vitals

Files to check/update:
- nextjs-frontend/src/app/layout.tsx
- nextjs-frontend/src/app/page.tsx
- nextjs-frontend/src/components/**

Optimizations:
- [x] Image optimization with next/image
- [x] Dynamic imports for heavy components
- [x] Bundle size analysis
- [x] Remove unused dependencies
- [x] Add loading skeletons
```

---

### Phase 12: Notifications & Communication ⚠️ COMPLEX

#### Task 12.1: Email Notification Templates ⭐ READY
```
Goal: Create email notification templates

Backend files to create:
- ERP.Application/Notifications/EmailTemplates/
- ERP.Application/Notifications/IEmailService.cs
- ERP.Infrastructure/Services/SmtpEmailService.cs

Templates:
- [x] Welcome email
- [x] Password reset
- [x] PO approval request
- [x] PO status update
- [x] Low stock alert
- [x] Weekly summary
```

#### Task 12.2: SMS Notifications ⭐ READY
```
Goal: Add SMS notification support

Backend files to create:
- ERP.Application/Notifications/ISmsService.cs
- ERP.Infrastructure/Services/TwilioSmsService.cs

Features:
- [x] Twilio integration
- [x] SMS templates
- [x] Delivery status tracking
- [x] Rate limiting
```

#### Task 12.3: WebSocket Notifications (SignalR) ⚠️ COMPLEX
```
Goal: Real-time notifications via SignalR

Backend files to create:
- ERP.Infrastructure/Services/NotificationService.cs
- ERP.API/Hubs/NotificationHub.cs
- ERP.API/Program.cs (add SignalR)

Frontend files to create:
- nextjs-frontend/src/hooks/useWebSocket.ts
- nextjs-frontend/src/components/NotificationBell.tsx
- nextjs-frontend/src/stores/notificationStore.ts

Commands to run:
cd ERP.API && dotnet add package Microsoft.AspNetCore.SignalR

⚠️ Requires backend changes - Need Confirmation
```

---

## 📋 QUICK WINS (Week 1) ✅ COMPLETED

| # | Task | Priority | Status |
|---|------|----------|--------|
| 1 | Fix `catch {}` empty blocks | HIGH | ✅ DONE |
| 2 | Add Export CSV button to tables | HIGH | ✅ DONE |
| 3 | Consistent skeleton loaders | MEDIUM | ✅ DONE |
| 4 | Add unread notification badge | HIGH | ✅ DONE |
| 5 | Dark mode toggle | MEDIUM | ✅ DONE |

---

## 📋 QUICK WINS (Week 2) ✅ COMPLETED

| # | Task | Priority | Status |
|---|------|----------|--------|
| 1 | User activity log page | MEDIUM | ✅ DONE |
| 2 | Inline edit for tables | MEDIUM | ✅ DONE |
| 3 | Bulk actions for tables | HIGH | ✅ DONE |
| 4 | Audit trail page | HIGH | ✅ DONE |
| 5 | Jest unit tests for hooks | HIGH | ✅ DONE |

---

## 📋 NEXT SPRINT ✅ COMPLETED

| # | Task | Priority | Status |
|---|------|----------|--------|
| 1 | Financial reports (Balance Sheet, P&L) | HIGH | ✅ DONE |
| 2 | Approval workflow visual | HIGH | ✅ DONE |
| 3 | Sentry integration | HIGH | ✅ DONE |
| 4 | Mobile responsive audit | MEDIUM | ⭐ READY |
| 5 | Postman collection | MEDIUM | ✅ DONE |
| 6 | Docker frontend | HIGH | ✅ DONE |
| 7 | GitHub Actions CI/CD | HIGH | ✅ DONE |

---

## ✅ COMPLETED FEATURES

### Phase 1-5 (2026-08-20)

- [x] Dashboard Draggable Widgets with @dnd-kit
- [x] Auto-Save Draft to localStorage
- [x] MSW Mock API for development
- [x] Storybook component documentation
- [x] Phase 1 Security (CORS, JWT, Rate Limiting, etc.)
- [x] Phase 2-4 TypeScript, Accessibility, Error Boundary

---

## 📊 PROJECT STATUS

| Component | Status | URL |
|-----------|--------|-----|
| Frontend (Next.js) | ✅ Ready | http://localhost:3000 |
| Backend (.NET) | ✅ Ready | http://localhost:5000 |
| Database (PostgreSQL) | ✅ Ready | localhost:5432 |
| Cache (Redis) | ✅ Ready | localhost:6379 |
| Swagger Docs | ✅ Ready | http://localhost:5000/swagger |
| Health Check | ✅ Ready | http://localhost:5000/health/ready |
| Storybook | ✅ Ready | localhost:6006 |

---

## 🔗 COMMITS HISTORY

```
d52cf23 - feat: add Storybook component documentation
8e3028a - feat: implement MSW mock API for development
32af134 - feat: implement auto-save form drafts to localStorage
69a8886 - feat: implement draggable dashboard widgets with @dnd-kit
5ab43a8 - docs: update TODO.md - all major features complete
1d8895a - feat: implement low priority features
edd3551 - feat: implement remaining production features
dbca178 - feat: implement High to Low features
90ebbdf - feat: Phase 1 Critical Security
```

---

## 🔐 Security Hardening: Permission System Rollout + Cross-Tenant Fixes

- [x] `[RequiresPermission("module.resource.action")]` rolled out to commands/queries across all 9 modules (Accounting, Analytics, Assets, Hrm, Inventory, Projects, Purchasing, Quality, Sales) — previously only `Common/Commands/WorkflowCommands.cs` enforced permissions.
- [x] Full permission catalog seeded in `DatabaseSeeder.cs` (`ModulePermissions` table) matching every attribute above.
- [x] **Cross-tenant IDOR fix (read side):** added a global EF Core query filter (`ERPDbContext.ApplyGlobalFilters`) that scopes every query against an `ITenantEntity` to the caller's `OrganizationId`. Closes gaps in handlers that fetched by Id (or listed) without an explicit org check (e.g. `CompleteInspectionCommand`, `UpdateAssetCommand`, `GetSalesOrdersQuery`, `GetAccountsQuery`, and others across Sales/Purchasing/Accounting/Quality/Assets/Projects/Analytics). Wires up the previously-unused `ITenantContext`/`TenantContext` scaffolding.
- [x] **Cross-tenant write fix:** several create handlers trusted a client-supplied `OrganizationId` in the request body instead of the authenticated user's session (`CreateProjectCommand`, `CreateProjectTaskCommand`, `CreateDepartmentCommand`, `CreatePositionCommand`, `CreateEmployeeCommand`, `CreatePayrollCommand`, `CreateBatchPayrollCommand`, `CreateAuditLogCommand`) — a caller could create/forge records under another org. Fixed to derive org from `ICurrentUserService`/the parent entity.
- [ ] Not covered here (flagged for follow-up): `Base/Commands/Roles/CreateRoleCommand.cs`, `Base/Commands/Users/CreateUserCommandHandler.cs` still trust a client-supplied `OrganizationId`; `User`/`Role` were intentionally excluded from the global tenant filter (would break login, which looks up users before an org is known).
- [ ] Could not run `dotnet build`/tests — no .NET SDK in this sandbox and outbound install was blocked by the environment's egress policy. Changes were reviewed manually (imports, factory-method signatures, constructor DI, brace balance) but not compiler-verified — **run a full build before deploying.**

---

## 📁 KEY FILES

| Feature | File |
|---------|------|
| JWT Security | `ERP.Infrastructure/Services/JwtService.cs` |
| Rate Limiting | `ERP.Infrastructure/Services/LoginRateLimitService.cs` |
| RBAC | `ERP.Application/Common/Behaviors/PermissionAuthorizationBehavior.cs` |
| Caching | `ERP.Infrastructure/Services/RedisCacheService.cs` |
| Export | `ERP.Application/Common/Reports/ExportService.cs` |
| API Key Auth | `ERP.API/Authentication/ApiKeyAuthenticationHandler.cs` |
| Data Masking | `ERP.Application/Common/Security/DataMaskingService.cs` |
| Audit Logging | `ERP.API/Middleware/ApiAuditLoggingMiddleware.cs` |
| Docker | `docker-compose.yml` |
| Tests | `nextjs-frontend/tests/*.spec.ts` |
| Dashboard Widgets | `nextjs-frontend/src/components/dashboard/` |
| Auto-Save | `nextjs-frontend/src/hooks/useAutoSave.ts` |
| MSW Mock | `nextjs-frontend/src/mocks/` |
| Storybook | `nextjs-frontend/.storybook/` |

---

## 📝 QUICK REFERENCE

### Demo Credentials
```
Username: admin
Password: DevPassword2024!
```

### Environment Variables
```
Frontend (.env.local):
NEXT_PUBLIC_API_URL=http://localhost:5000
NEXT_PUBLIC_MSW_ENABLED=false

Backend (Railway env vars):
JwtSettings__SecretKey=<generate-32-chars>
JwtSettings__AccessTokenExpirationMinutes=15
JwtSettings__RefreshTokenExpirationDays=7
ConnectionStrings__DefaultConnection=<postgres-url>
Redis__ConnectionString=<redis-url>
```

---

*Last updated: 2026-08-20*

# NEXTERP

A multi-tenant ERP system with 9 business modules, built on a .NET Clean Architecture backend and a Next.js frontend.

[![CI](https://github.com/thevoidsyntax/nexterp/actions/workflows/ci.yml/badge.svg)](https://github.com/thevoidsyntax/nexterp/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com)
[![Next.js](https://img.shields.io/badge/Next.js-16-black?logo=next.js)](https://nextjs.org)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Modules

Accounting · Analytics · Assets · HRM · Inventory · Projects · Purchasing · Quality · Sales

Access to every command and query is enforced by a `[RequiresPermission("module.resource.action")]`
attribute checked in a MediatR pipeline behavior, and data is isolated per organization by an
EF Core global query filter — see [Multi-tenancy & permissions](#multi-tenancy--permissions).

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, MediatR (CQRS), FluentValidation, EF Core |
| Frontend | Next.js 16, React 19, TypeScript (strict), TailwindCSS, Zustand |
| Database | PostgreSQL |
| Cache | Redis |
| Auth | JWT + BCrypt |
| Testing | Playwright (E2E), Storybook, Jest |
| Deployment | Railway (backend) + Vercel (frontend) |

## Project Structure

```
nexterp/
├── ERP.API/              # REST API — controllers, Program.cs, middleware
├── ERP.Application/      # CQRS commands/queries, validators, permission attributes
├── ERP.Domain/           # Entities, enums — no external dependencies
├── ERP.Infrastructure/   # EF Core DbContext, tenant query filters, JWT/Redis services
├── ERP.API.ContractTests/
├── ERP.Application.UnitTests/
├── ERP.Domain.UnitTests/
├── nextjs-frontend/      # Next.js app (App Router) — the active frontend
├── docker/               # Dockerfiles and init scripts for local services
├── docs/                 # Additional documentation and assets
└── docker-compose.yml    # Full local stack: db, redis, api, frontend, pgAdmin
```

## Getting Started

### Option A — Docker Compose (fastest)

```bash
cp .env.example .env    # then fill in JWT_SECRET_KEY, POSTGRES_PASSWORD, etc.
docker-compose up -d
docker-compose logs -f
```

| Service | URL |
|---|---|
| Frontend | http://localhost:3000 |
| Backend API | http://localhost:5000 |
| PostgreSQL | localhost:5432 |
| Redis | localhost:6379 |
| pgAdmin | http://localhost:5050 |
| Redis Commander | http://localhost:8081 |

### Option B — Run locally

Requires the .NET 10 SDK, Node.js 18+, and PostgreSQL/Redis (via Docker or local install).

```bash
# Backend
dotnet restore
dotnet ef database update --project ERP.Infrastructure --startup-project ERP.API
dotnet run --project ERP.API

# Frontend (separate terminal)
cd nextjs-frontend
npm install
npm run dev
```

## Common Commands

```bash
# Frontend
cd nextjs-frontend
npm run dev        # Development server
npm run build      # Production build
npm run lint        # ESLint
npx tsc --noEmit    # TypeScript check
npm run test        # Playwright E2E tests
npm run storybook   # Component explorer

# Backend
dotnet build
dotnet test
dotnet run --project ERP.API
```

## Multi-tenancy & Permissions

- Every tenant-owned entity implements `ITenantEntity`. `ERPDbContext` applies a global EF Core
  query filter (`OrganizationId == currentTenantId`) to all of them via reflection, so a query
  simply cannot cross tenants by accident.
- Every command/query handler is decorated with `[RequiresPermission("module.resource.action")]`
  and checked centrally by `PermissionAuthorizationBehavior` in the MediatR pipeline — permission
  checks are not scattered per-endpoint.
- Write handlers derive `OrganizationId` from the authenticated user's `ICurrentUserService`
  context, never from client-supplied request fields.

See `TODO.md` for known follow-ups (e.g. `Base/Roles` and `Base/Users` commands that still need
the same write-side hardening).

## CI/CD

GitHub Actions runs lint, typecheck, and E2E tests on every push/PR (`.github/workflows/ci.yml`),
and deploys `master` to Railway (backend) and Vercel (frontend) on merge
(`.github/workflows/deploy.yml`). Deploys require `RAILWAY_TOKEN`, `VERCEL_TOKEN`,
`VERCEL_ORG_ID`, `VERCEL_PROJECT_ID`, `JWT_SECRET_KEY`, and `REDIS_CONNECTION_STRING` to be set as
repository secrets.

## More Documentation

| File | Purpose |
|---|---|
| [`README_DEV.md`](README_DEV.md) | Architecture deep-dive, CQRS examples, API reference |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Branching, commit style, PR process |
| [`SECURITY.md`](SECURITY.md) | Reporting vulnerabilities |
| [`TODO.md`](TODO.md) | Task list, progress, known gaps |
| [`CHANGELOG.md`](CHANGELOG.md) | Release history |

## License

MIT — see [`LICENSE`](LICENSE).

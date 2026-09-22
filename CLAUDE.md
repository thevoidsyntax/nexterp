# NEXTERP ERP - Project Context

## Quick Reference

**Project Type:** Full-Stack ERP Application
- Frontend: Next.js 16, TypeScript strict, Tailwind, Zustand
- Backend: .NET 8, MediatR, FluentValidation
- Auth: JWT + BCrypt
- DB: PostgreSQL
- Deployment: Vercel (frontend) + Railway (backend)

## Important Files

| File | Description |
|------|-------------|
| `TODO.md` | Full task list & progress |
| `ERP.API/Program.cs` | Backend CORS config |
| `nextjs-frontend/src/lib/api.ts` | Frontend API client |
| `nextjs-frontend/src/app/dashboard/` | All dashboard pages |

## Current Priority

1. **Critical:** JWT security hardening, rate limiting
2. **High:** RBAC, audit logging, health checks
3. **Medium:** Batch operations, export features

## Code Style Rules

1. **TypeScript:** Strict mode, no `as any`, use `err: unknown`
2. **Error Handling:** Always use try-catch with proper error messages
3. **Accessibility:** All icon buttons need `aria-label`
4. **Tailwind:** No dynamic classes like `bg-${var}`, use full class names
5. **Commits:** Use Conventional Commits (`feat:`, `fix:`, `refactor:`)

## Common Commands

```bash
# Frontend
cd nextjs-frontend
npm run dev      # Development
npm run build    # Production build
npx tsc --noEmit # TypeScript check

# Backend (requires .NET 8)
cd ERP.API
dotnet run

# Git
git add -A && git commit -m "message" && git push
```

## Environment Variables

**Frontend (.env.local):**
```
NEXT_PUBLIC_API_URL=https://api-production-ab1b.up.railway.app
```

**Backend (Railway env vars):**
```
ConnectionStrings__DefaultConnection=...
Redis__ConnectionString=...
JwtSettings__SecretKey=...
JwtSettings__AccessTokenExpiryMinutes=15
JwtSettings__RefreshTokenExpiryDays=7
```

Demo/sample data (including the `admin` account) is only ever seeded in `Development`,
or in another environment if you explicitly opt in — see `TODO.md`'s 2026-09-22 audit
entry before setting these on a real deployment:
```
SEED_DEMO_DATA=true   # opt-in outside Development; omit in production
DEMO_PASSWORD=...     # required if SEED_DEMO_DATA=true outside Development
```

---

*Last updated: 2026-09-22*

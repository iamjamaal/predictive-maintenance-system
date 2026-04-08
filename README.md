# Predictive Maintenance System

ASP.NET Core web application for equipment, maintenance, alert, inventory, and dashboard workflows.

## Current Status (Audit: 2026-04-08)

This repository contains substantial implementation and active migration history, but the checked-in state is development-oriented and not fully production-ready yet.

### Verified state

- Main branch is clean and synced.
- Core stack is present: MVC, Identity, EF Core, SignalR, service layer.
- Multiple features are currently disabled in startup and/or project compile includes.
- No dedicated unit/integration test project is currently included.

## Tech Stack

- .NET: net9.0
- Web: ASP.NET Core MVC + Razor Pages
- Auth: ASP.NET Core Identity + JWT endpoints
- Data: EF Core + SQL Server
- Realtime: SignalR

## Quick Start (Development)

1. Install .NET SDK 9.0 or newer.
2. Install SQL Server / SQL Express and ensure the instance in appsettings is reachable.
3. Configure secrets using user-secrets or environment variables:

   - ConnectionStrings:DefaultConnection
   - EmailSettings (SMTP host, sender, password)
   - Jwt:Key, Jwt:Issuer, Jwt:Audience

4. Restore packages:

   - dotnet restore

5. Apply migrations:

   - dotnet ef database update

6. Run the app:

   - dotnet run

## Important Notes

- Some services and Swagger wiring are intentionally commented out in startup.
- Some API controllers are excluded in the project file.
- Development bypass/debug endpoints exist and should be reviewed before deployment.
- Placeholder/demo credentials in appsettings must be replaced for real deployment.

## Stabilization Priorities

### P0 (Before any demo/release)

- Re-enable or intentionally remove disabled startup modules.
- Align project metadata/docs with actual enabled features.
- Move all secrets/keys out of appsettings into secure configuration.
- Remove development bypass endpoints from non-development builds.

### P1 (Reliability)

- Add at least one test project (smoke tests + core service tests).
- Validate end-to-end runbook on a clean machine.
- Add CI build and test checks.

### P2 (Maintainability)

- Consolidate duplicate/alternative controller files.
- Reduce report clutter and keep one canonical status document.
- Add architecture and deployment docs.

## License

See LICENSE.

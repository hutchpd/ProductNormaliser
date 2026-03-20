# ProductNormaliser.Web

ProductNormaliser.Web is the web UI host for the platform.

Its purpose is to provide the user-facing dashboard for category selection, crawl-source management, progress tracking, and product inspection while consuming backend APIs over HTTP instead of coupling directly to MongoDB or repository code.

## Why this project exists

- establish a dedicated UI layer in the solution
- keep the frontend separate from persistence and worker logic
- provide a home for dashboard pages, visualisations, product exploration, and operational workflows
- support category and source management across TVs and broader electrical goods

## Architectural role

This project should talk to backend API endpoints and shared UI-level models only. It should not reference Infrastructure or interact with database concerns directly.

## Current dashboard wiring

The web host now contains a first dashboard slice that calls the Admin API for:

- `GET /api/categories/enabled` to populate category selection
- `GET /api/categories/{categoryKey}/detail` to render metadata plus schema in one request
- `GET /api/sources` and `GET /api/sources/{sourceId}` for managed source views
- source registration, enable or disable, category assignment, and throttling updates

The main dashboard page shows category detail and the live source registry. The source detail page wires through the update, category assignment, and throttling workflows.

## Configuration

Set the Admin API base address in [appsettings.json](appsettings.json) with:

- `AdminApi:BaseUrl`

## Build

```bash
dotnet build ProductNormaliser.Web/ProductNormaliser.Web.csproj
```
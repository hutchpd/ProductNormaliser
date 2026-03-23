# ProductNormaliser.Web

ProductNormaliser.Web is the operator-facing Razor Pages host for Milestone 1. It consumes the Admin API over HTTP and gives operators one consistent workflow surface for category context, crawl launch, job monitoring, product inspection, quality review, and source management.

## Milestone 1 scope

The web host currently delivers:

- an operator landing page that keeps the active category context visible
- category selection for the rollout set: TVs, Monitors, and Laptops
- quick crawl launch and crawl-job monitoring
- canonical product exploration with quality-aware filters and paging
- product detail pages with source comparison, evidence, conflicts, and history
- a quality dashboard for schema coverage, unmapped attributes, stability, and disagreements
- source registry and source detail pages with readiness, health, last-activity, enable or disable, category assignment, and throttling controls

## Architectural role

This project talks to backend API endpoints and UI-level models only. It does not reference Infrastructure and does not talk directly to MongoDB or repository implementations.

## Admin API dependencies

The web host calls the Admin API for:

- category discovery and category detail
- crawl job list, launch, and job detail
- product list, product detail, and product history
- quality dashboards and analytics
- source registry, source detail, and source management actions
- high-level stats used by the operator landing page

## Configuration

Set the Admin API base address in [appsettings.json](appsettings.json) with:

- `AdminApi:BaseUrl`

## Build

```bash
dotnet build ProductNormaliser.Web/ProductNormaliser.Web.csproj
```
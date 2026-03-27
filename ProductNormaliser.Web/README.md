# ProductNormaliser.Web

ProductNormaliser.Web is the operator-facing Razor Pages host for Milestone 1. It consumes the Admin API over HTTP and gives operators one consistent workflow surface for category context, source setup, seeded crawl launch, discovery monitoring, product inspection, quality review, and source management.

## Milestone 1 scope

The web host currently delivers:

- an operator landing page that keeps the active category context visible
- an operator landing page operational health panel for queue depth, retry backlog, recent failures, at-risk sources, and category pressure
- an operator landing page boot-and-populate panel showing boot-ready sources, categories in context, estimated discovery seeds, and recent confirmed-product throughput
- category selection for the current supported set: TVs, Monitors, Laptops, Smartphones, Tablets, Headphones, and Speakers
- category selectors and explorer filters that expose the full supported category set with their live readiness metadata
- seeded crawl launch and crawl-job monitoring with discovery and product progress shown together
- canonical product exploration with quality-aware filters and paging
- product detail pages with source comparison, evidence, conflicts, and history
- a quality dashboard for schema coverage, unmapped attributes, stability, and disagreements
- source registry and source detail pages with readiness, health, last-activity, source registration, discovery profile editing, enable or disable, category assignment, and throttling controls
- source candidate discovery with candidate reasons, evidence strength, automation posture, and classification-informed recommendation cues presented alongside the existing probe heuristics

## Architectural role

This project talks to backend API endpoints and UI-level models only. It does not reference Infrastructure and does not talk directly to MongoDB or repository implementations.

## Admin API dependencies

The web host calls the Admin API for:

- category discovery and category detail
- crawl job list, launch, and job detail
- product list, product detail, and product history
- quality dashboards and analytics
- source registry, source detail, and source management actions
- source candidate discovery and onboarding automation settings
- high-level stats and operational summary used by the operator landing page

The web experience is now intentionally optimized for a simple startup path:

1. boot the app
2. register or enable sources
3. choose category context
4. launch a seeded category crawl
5. watch discovery queue depth, confirmed product targets, and downstream product progress update from the same console

The web UI keeps the source-onboarding experience deliberately explicit. The classification layer is surfaced only as part of the candidate explanation and recommendation trail so operators can see when a candidate looks product-like and when it still needs manual review.

## Observability surface

The landing page now exposes a lightweight operator-facing health summary built from the Admin API stats payload. It is intended to answer three immediate questions without leaving the console:

- is queue pressure building up?
- are retries and failures concentrated in specific sources?
- is one category absorbing disproportionate crawl load?

This UI does not replace external telemetry collection. It gives operators a fast internal view over the same persisted runtime data that backs the rest of the admin surface.

## Configuration

Set the Admin API base address in [appsettings.json](appsettings.json) with:

- `AdminApi:BaseUrl`

## Build

```bash
dotnet build ProductNormaliser.Web/ProductNormaliser.Web.csproj
```
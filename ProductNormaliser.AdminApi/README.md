# ProductNormaliser.AdminApi

ProductNormaliser.AdminApi is the operational, intelligence, and dashboard management API for the platform. It exposes queue state, discovery progress, crawl logs, conflicts, canonical product detail, change history, quality analytics, category metadata, and managed crawl-source administration over the shared MongoDB database.

This is an internal-facing service designed to help operators, analysts, and developers inspect what the system is doing and why.

## Responsibilities

- expose operational monitoring endpoints
- expose discovery-aware job and source progress views
- expose product and change-history views
- expose quality and source-intelligence analytics
- expose category catalog and schema metadata for dashboard discovery
- expose managed crawl-source administration for the web UI
- expose source-candidate discovery and guarded onboarding settings for the web UI
- translate persisted domain records into API DTOs suitable for inspection or dashboarding

## Runtime composition

At startup the API registers:

- ASP.NET Core controllers
- OpenAPI support for development
- MongoDB-backed stores and infrastructure services
- `IAdminQueryService` for operational read models
- `IDataIntelligenceService` for quality and historical intelligence read models
- `ICategoryManagementService` for category catalog and combined detail lookups
- `ISourceManagementService` for source registration, validation, and policy updates
- `ISourceCandidateDiscoveryService` for explainable source-candidate evaluation
- `ISourceOperationalInsightsProvider` for source readiness, health, and recent-activity summaries

The API composition also registers an optional local page-classification service used by source probing. It is treated as a supporting signal behind the existing heuristic pipeline, not as a separate decision system.

The API composition now supports full source discovery profiles end to end, including category entry pages, sitemap hints, allow or deny rules, URL patterns, max depth, and per-run budgets.

## Main controllers and endpoints

### Stats

- `GET /api/stats`

Returns a high-level operational summary.

The stats payload now includes both catalogue counts and an operational snapshot covering:

- active crawl jobs
- crawl queue depth, discovery queue depth, retry backlog, and failed queue items
- throughput and failures over the trailing 24 hours
- per-source operational health metrics
- per-category crawl pressure metrics
- discovered URL counts and confirmed product target counts in the aggregate and per-category views

### Queue

- `GET /api/queue`
- `GET /api/queue/priorities`

The priorities endpoint is especially useful because it surfaces the reasoning behind ordering, including source quality, change frequency, volatility, missing attributes, freshness, and the next scheduled attempt.

### Crawl logs

- `GET /api/crawl/logs`
- `GET /api/crawl/logs/{id}`

Use these endpoints to inspect crawl behavior and individual processing outcomes.

### Conflicts

- `GET /api/conflicts`

Returns merge conflicts where evidence is competing or ambiguous.

### Products

- `GET /api/products`
- `GET /api/products/{id}`
- `GET /api/products/{id}/history`

These endpoints let you inspect filtered product lists, individual canonical records, and the time series of meaningful change events.

### Crawl jobs

- `GET /api/crawljobs`
- `POST /api/crawljobs`
- `GET /api/crawljobs/{jobId}`

These endpoints support the operator crawl console and quick-launch flow.

The crawl-job payloads now carry discovery-aware progress such as discovered URL counts, confirmed product target counts, discovery queue depth, product queue depth, rejected pages, robots-blocked pages, and per-category or per-source coverage.

### Categories

- `GET /api/categories`
- `GET /api/categories/families`
- `GET /api/categories/enabled`
- `GET /api/categories/{categoryKey}`
- `GET /api/categories/{categoryKey}/schema`
- `GET /api/categories/{categoryKey}/detail`

These endpoints exist so the dashboard can discover supported electrical-goods categories, group them by family, and render one-call detail pages that combine metadata and schema.

### Sources

- `GET /api/sources`
- `GET /api/sources/automation-settings`
- `GET /api/sources/{sourceId}`
- `POST /api/sources`
- `PUT /api/sources/{sourceId}`
- `POST /api/sources/{sourceId}/enable`
- `POST /api/sources/{sourceId}/disable`
- `PUT /api/sources/{sourceId}/categories`
- `PUT /api/sources/{sourceId}/throttling`

These endpoints manage the dedicated crawl-source registry used by the web UI. They include OpenAPI response annotations and concrete example payloads in the generated document so dashboard and client developers can inspect the expected shapes directly.

The source payloads now include readiness, health, recent activity, discovery queue depth, confirmed product counts, automation policy, and the full source discovery profile so the operator UI can surface crawl posture without separately stitching together telemetry.

`POST /api/sources` and `PUT /api/sources/{sourceId}` now support source discovery profile data directly. If a profile is omitted during registration, the application layer applies conservative startup defaults so a newly added source can participate in the boot-and-populate flow immediately.

### Source candidate discovery

- `POST /api/sources/candidates/discover`

This endpoint returns candidate sources with market and locale evidence, probe signals, recommendation reasons, and the output of the optional classification layer where available. The API also exposes conservative automation settings so the web UI can keep onboarding controls visible and operator-led.

### Quality and intelligence

- `GET /api/quality/coverage/detailed`
- `GET /api/quality/unmapped`
- `GET /api/quality/sources`
- `GET /api/quality/merge-insights`
- `GET /api/quality/source-history`
- `GET /api/quality/attribute-stability`
- `GET /api/quality/source-disagreements`

Most of the quality endpoints accept a `categoryKey` query parameter and default to the `tv` category.

The `source-history` and `source-disagreements` endpoints also support optional source filtering.

## Services

The two main read-model services are:

- `IAdminQueryService`: crawl logs, queue state, product detail, conflict lists, stats, and product history
- `IDataIntelligenceService`: coverage, unmapped attributes, source quality, merge insights, source history, attribute stability, and disagreement analytics

These services isolate controller logic from the shape of the underlying persisted model.

## Observability model

The API now sits on top of a stronger observability model for crawl operations:

- crawl job lifecycle events are logged as structured entries in the application layer
- worker and queue services emit `ProductNormaliser.Operations` metrics and traces
- `IAdminQueryService.GetStatsAsync` aggregates persisted queue state, jobs, crawl logs, crawl sources, and source-quality snapshots into one dashboard-friendly operational summary

This means the API can answer both business-health and runtime-health questions without asking the web layer to stitch several endpoints together.

## Verification boundaries

Verified in tests:

- operational summary aggregation from persisted state
- contract parity for the extended stats payload

Observed operationally:

- the actual metric stream from the `Meter`
- the trace stream from the `ActivitySource`
- live log collection and search in your hosting environment

## Configuration

Configuration is read from [appsettings.json](appsettings.json), [appsettings.Development.json](appsettings.Development.json), and the environment.

The Admin API key is configured under `ManagementApiSecurity`.

- `ManagementApiSecurity:ApiKeyHeaderName` controls which header is inspected. The default is `X-Management-Api-Key`.
- `ManagementApiSecurity:ApiKeys` contains the configured management keys. Set the `Secret` for the operator key here or override it with environment-specific configuration before exposing the API beyond your machine.
- `ManagementApiSecurity:AllowDevelopmentLoopbackBypass` stays `false` by default. Only set it to `true` for explicit local loopback development when you intentionally want to skip the header on localhost requests.

Because the API reads from MongoDB through shared infrastructure registration, it also needs the same `Mongo` settings used by the worker when running outside the existing local defaults.

If the optional classification layer is enabled, the API host also reads `Llm` settings such as enablement, timeout, confidence threshold, evaluation mode, and local model path. These settings are deliberately optional; the API continues to function with heuristics only when the classification layer is disabled or unavailable.

## How to run

From the repository root:

```bash
dotnet run --project ProductNormaliser.AdminApi
```

Send the configured operator key in the `X-Management-Api-Key` header. The checked-in bootstrap values live in [appsettings.json](appsettings.json) and [appsettings.Development.json](appsettings.Development.json); replace them in your own environment before using the API anywhere other than local development.

OpenAPI is enabled in development.

The included HTTP scratch file suggests a local development base address of `http://localhost:5209`.

## Example use cases

- inspect which pages are queued and when they will be retried
- review why a source is trusted less than before
- identify attributes that are still frequently unmapped
- understand which sources repeatedly disagree with consensus
- view the change history of a canonical product
- build dashboards over coverage, freshness, and merge confidence

## Current scope and limitations

- queue write flows are still not exposed as a public ingestion API
- the API now requires a configured management API key for non-bypassed access, but it is still best treated as an internal operational surface rather than a public internet API
- it is best treated as an operational admin surface, not a public internet API

## Build

```bash
dotnet build ProductNormaliser.AdminApi/ProductNormaliser.AdminApi.csproj
```

## Why this project matters

Commercial product-intelligence systems are often difficult to interrogate when data quality is questioned. This project is the inspection layer that makes ProductNormaliser operationally useful: it exposes the queue, the evidence trail, and the longitudinal quality signals needed to trust the system.
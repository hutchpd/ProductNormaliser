# ProductNormaliser

ProductNormaliser is an open product-intelligence engine for turning messy retail and manufacturer page data into clean, canonical, comparable product records. It crawls source pages, extracts structured product evidence, normalises attributes into a category schema, resolves identity across sources, merges competing claims into a canonical product, and keeps learning over time from quality history, disagreement patterns, and page volatility.

The platform started with televisions and now includes category metadata, schema, and normalisation extension points that support broader electrical goods. `tv` remains the most mature category, with `monitor`, `laptop`, and `refrigerator` now represented in the category and normaliser infrastructure so the system can expand safely.

## What problem this solves

Product data on the public web is inconsistent:

- retailers describe the same product differently
- specifications appear in different units and formats
- some sources omit attributes while others contradict them
- offers change faster than technical specifications
- a page that was accurate last week may be stale today

ProductNormaliser addresses that by maintaining both:

- source-level truth: what each source said, when it said it, and how reliable it has been
- canonical truth: the best current merged view of the product with supporting evidence and merge confidence

## How this compares to GfK, NIQ, CNET-style product insight, Euromonitor, and Mintel

ProductNormaliser overlaps with commercial product-intelligence platforms in outcome, but not in delivery model or scope.

### Relative to GfK and NIQ

GfK and NIQ are typically associated with large-scale market measurement, retail intelligence, panel data, and syndicated commercial datasets. ProductNormaliser is not a replacement for that kind of market infrastructure. It is better thought of as a transparent product-record intelligence layer:

- ProductNormaliser focuses on entity resolution, specification normalisation, source trust, change tracking, and canonical product construction.
- GfK and NIQ typically operate higher in the commercial stack with broader market coverage, proprietary data assets, and enterprise reporting products.
- ProductNormaliser gives you explainable product records and evidence trails that can feed your own analytics, catalog, pricing, or monitoring workflows.

### Relative to CNET-style product insight

CNET-style experiences are usually editorial, shopper-facing, and review-driven. ProductNormaliser does not generate consumer reviews or editorial verdicts. Instead, it provides the structured evidence layer such experiences can consume:

- canonical specs
- source comparisons
- change history
- offer history
- conflict and disagreement visibility

In other words, CNET-style product insight sits closer to presentation and interpretation; ProductNormaliser sits closer to data capture, reconciliation, and explainable product truth.

### Relative to Euromonitor and Mintel

Euromonitor and Mintel are commonly used for macro market research, category strategy, and consumer or industry trend reporting. ProductNormaliser is much more operational and granular:

- it monitors live source pages rather than producing broad market reports
- it tracks product-level evidence rather than consumer-survey or strategy narratives
- it is designed for ingestion into internal systems, not mainly for analyst reports

### Practical positioning

The simplest way to position ProductNormaliser is:

> an open, explainable product-record intelligence engine rather than a full syndicated research platform

It is most useful where you want to own the data pipeline, inspect merge decisions, adapt category logic, and integrate the output into your own downstream services.

## Solution structure

The solution now contains ten projects:

- [ProductNormaliser.Domain](ProductNormaliser.Domain/README.md): domain models, category schema, normalisation contracts, merge logic, and intelligence interfaces
- [ProductNormaliser.Application](ProductNormaliser.Application/README.md): application-layer seam for use cases, orchestration, and future category-agnostic workflow composition
- [ProductNormaliser.Infrastructure](ProductNormaliser.Infrastructure/README.md): MongoDB persistence, crawl queue, fetch/robots services, delta detection, extraction, trust, stability, and disagreement services
- [ProductNormaliser.AdminApi](ProductNormaliser.AdminApi/README.md): operational and intelligence read API for queue state, crawl logs, conflicts, product history, and quality analytics
- [ProductNormaliser.Worker](ProductNormaliser.Worker/README.md): background processing host that executes the crawl and merge pipeline
- [ProductNormaliser.Web](ProductNormaliser.Web/README.md): web UI host that will consume backend APIs rather than talking to persistence directly
- [ProductNormaliser.Domain.Tests](ProductNormaliser.Domain.Tests/README.md): focused test project for domain-level rules and models
- [ProductNormaliser.Application.Tests](ProductNormaliser.Application.Tests/README.md): current broad integration and orchestration test suite while responsibilities are being split by layer
- [ProductNormaliser.AdminApi.Tests](ProductNormaliser.AdminApi.Tests/README.md): focused test project for API host and controller-facing behavior
- [ProductNormaliser.Web.Tests](ProductNormaliser.Web.Tests/README.md): focused test project for the web UI host

## Architecture at a glance

1. A crawl target is queued for a source URL.
2. The worker fetches the page while respecting robots and host delay rules.
3. Structured data is extracted from the page.
4. A source product is built from extracted data.
5. Attributes are normalised into the canonical schema.
6. Identity resolution decides whether the source product matches an existing canonical product.
7. Merge logic computes evidence-weighted attribute winners.
8. Conflicts, quality signals, change events, trust snapshots, and disagreement analytics are persisted.
9. Adaptive scheduling decides when the source should be revisited.
10. The admin API exposes the operational and analytical view of the system.

## Current capabilities

The solution currently includes:

- category metadata and schema discovery for electrical-goods families
- schema-driven attribute normalisation for TV, with category-specific providers in place for monitors, laptops, and refrigerators
- alias handling and measurement parsing
- structured data extraction from HTML and JSON-LD
- MongoDB persistence for source and canonical records
- MongoDB persistence for managed crawl sources and per-source throttling policy
- identity resolution across sources
- explainable merge weighting and conflict detection
- semantic delta detection for product changes
- worker orchestration with retry and skip/fail handling
- admin endpoints for operational observability
- admin endpoints for category catalog management and crawl-source management
- quality analytics for coverage, unmapped attributes, source quality, and merge insights
- temporal intelligence for source trust history, attribute stability, and product change timelines
- adaptive crawl scheduling based on volatility, stability, freshness, and source behavior
- per-source disagreement tracking that feeds back into trust and merge decisions
- a Razor Pages web dashboard that reads category detail and source management data from the Admin API

## Prerequisites

- .NET SDK 10.0.x
- MongoDB running locally or a reachable MongoDB instance

One quick local option for MongoDB is Docker:

```bash
docker run -d --name productnormaliser-mongo -p 27017:27017 mongo:7
```

## Build and test

From the repository root:

```bash
dotnet restore ProductNormaliser.slnx
dotnet build ProductNormaliser.slnx
dotnet test ProductNormaliser.slnx
```

## Configuration

Worker runtime configuration lives in [ProductNormaliser.Worker/appsettings.json](ProductNormaliser.Worker/appsettings.json).

Key settings:

- `Mongo:ConnectionString`
- `Mongo:DatabaseName`
- `Crawl:UserAgent`
- `Crawl:DefaultHostDelayMilliseconds`
- `Crawl:TransientRetryCount`
- `Crawl:WorkerIdleDelayMilliseconds`
- `Crawl:HostDelayMilliseconds`

Admin API configuration lives in [ProductNormaliser.AdminApi/appsettings.json](ProductNormaliser.AdminApi/appsettings.json).

## Running the worker

The worker is the engine that consumes the crawl queue and updates the database.

```bash
dotnet run --project ProductNormaliser.Worker
```

The worker:

- dequeues pending crawl items from MongoDB
- fetches and extracts source data
- builds or updates source products
- merges into canonical products
- records crawl logs, conflicts, trust signals, change events, and disagreement data
- reschedules future attempts using adaptive backoff rules

## Running the admin API

The admin API is a read-side service over the same MongoDB database.

```bash
dotnet run --project ProductNormaliser.AdminApi
```

The included HTTP scratch file suggests a local development base address of `http://localhost:5209`, although the final URL depends on your local ASP.NET Core launch configuration.

OpenAPI is mapped in development builds.

## Admin API surface

### Operational endpoints

- `GET /api/stats`: high-level counts and operational summary
- `GET /api/queue`: current queue state
- `GET /api/queue/priorities`: queue items with computed priority signals and next-attempt timings
- `GET /api/crawl/logs`: recent crawl logs
- `GET /api/crawl/logs/{id}`: individual crawl log detail
- `GET /api/conflicts`: merge conflicts requiring review or analysis
- `GET /api/products/{id}`: canonical product detail
- `GET /api/products/{id}/history`: product change timeline

### Category and source management endpoints

- `GET /api/categories`: list known categories
- `GET /api/categories/families`: list category families for dashboard grouping
- `GET /api/categories/enabled`: list enabled crawlable categories
- `GET /api/categories/{categoryKey}`: get category metadata
- `GET /api/categories/{categoryKey}/schema`: get category schema
- `GET /api/categories/{categoryKey}/detail`: get metadata and schema in one payload
- `GET /api/sources`: list managed crawl sources
- `GET /api/sources/{sourceId}`: get one managed source
- `POST /api/sources`: register a managed source
- `PUT /api/sources/{sourceId}`: update display name, base URL, and description
- `POST /api/sources/{sourceId}/enable`: enable a source
- `POST /api/sources/{sourceId}/disable`: disable a source
- `PUT /api/sources/{sourceId}/categories`: update assigned category keys
- `PUT /api/sources/{sourceId}/throttling`: update host throttling policy

### Quality and intelligence endpoints

- `GET /api/quality/coverage/detailed`: category coverage against the schema
- `GET /api/quality/unmapped`: backlog of unmapped or unknown attributes
- `GET /api/quality/sources`: source quality scores
- `GET /api/quality/merge-insights`: merge and evidence quality summary
- `GET /api/quality/source-history`: historical source trust snapshots
- `GET /api/quality/attribute-stability`: per-attribute stability analytics
- `GET /api/quality/source-disagreements`: per-source disagreement metrics

The quality endpoints default to the `tv` category, which remains the current first-class category schema, but the scoring and completeness model are now category-aware.

## Typical development workflow

1. Start MongoDB.
2. Run the worker against a configured database.
3. Seed crawl queue items through application code, scripts, or tests.
4. Run the admin API against the same database.
5. Inspect queue state, crawl logs, canonical products, and quality endpoints.

## Important implementation notes

- The current admin API is primarily an internal operational interface, not a public hardened product API.
- The worker and API assume a shared MongoDB database.
- The system is designed to preserve evidence rather than flattening source data into a single opaque record.
- Crawl behavior is adaptive: successful, volatile, stable, or failure-prone pages will naturally drift toward different revisit cadences.
- Trust is temporal: source quality is treated as a changing signal, not a fixed source rank.

## Current limitations

- TV remains the most mature category; monitor, laptop, and refrigerator support are currently thinner and should be extended with deeper extraction and normalisation rules over time
- queue write flows are still not exposed as a public management API
- authentication and role-based access are not configured for the admin surface
- production deployment concerns such as distributed workers, secret management, and externalized observability are not yet formalized in the repo

## Why the project structure matters

The separation between Core, Infrastructure, Worker, and AdminApi is deliberate:

- Core stays focused on product intelligence rules and contracts
- Infrastructure implements persistence and external-system adapters
- Worker owns the write-side pipeline
- AdminApi owns the read-side operational and analytical experience

That split keeps the domain logic explainable and testable while still allowing the runtime services to evolve independently.
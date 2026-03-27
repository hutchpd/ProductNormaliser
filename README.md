# ProductNormaliser

ProductNormaliser is an open product-intelligence engine for turning messy retail and manufacturer page data into clean, canonical, comparable product records. It crawls source pages, extracts structured product evidence, normalises attributes into a category schema, resolves identity across sources, merges competing claims into a canonical product, and keeps learning over time from quality history, disagreement patterns, and page volatility.

Milestone 1 is centered on an end-to-end operator workflow for seven supported categories: `tv`, `monitor`, `laptop`, `smartphone`, `tablet`, `headphones`, and `speakers`. The platform still keeps category and normalisation extension points broad enough for broader electrical-goods expansion beyond the current supported electrical-goods set.

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
- [ProductNormaliser.Worker](ProductNormaliser.Worker/README.md): background processing host that executes the discovery, crawl, and merge pipeline
- [ProductNormaliser.Web](ProductNormaliser.Web/README.md): web UI host that will consume backend APIs rather than talking to persistence directly
- [ProductNormaliser.Domain.Tests](ProductNormaliser.Domain.Tests/README.md): focused test project for domain-level rules and models
- [ProductNormaliser.Application.Tests](ProductNormaliser.Application.Tests/README.md): current broad integration and orchestration test suite while responsibilities are being split by layer
- [ProductNormaliser.AdminApi.Tests](ProductNormaliser.AdminApi.Tests/README.md): focused test project for API host and controller-facing behavior
- [ProductNormaliser.Web.Tests](ProductNormaliser.Web.Tests/README.md): focused test project for the web UI host

## Architecture at a glance

1. Operators register or enable managed crawl sources and assign categories such as `tv`, `monitor`, `laptop`, and `smartphone`.
2. Each source carries a discovery profile with category entry pages, sitemap hints, allow or deny path rules, URL patterns, depth limits, and per-run budgets.
3. A category crawl job now seeds deterministic discovery from eligible managed sources instead of relying only on pre-known targets.
4. The discovery worker fetches sitemaps and listing pages while respecting robots rules, source throttling, depth limits, and URL budgets.
5. Discovered URLs and candidate source pages are screened by heuristics and an optional local classification layer before promotion or operator recommendation.
6. The crawl worker fetches confirmed product pages and extracts structured product evidence.
7. A source product is built from extracted data and normalised into the canonical schema.
8. Identity resolution decides whether the source product matches an existing canonical product.
9. Merge logic computes evidence-weighted attribute winners, while conflicts, change events, trust snapshots, disagreement analytics, and discovery progress are persisted.
10. Related-link expansion can feed nearby product and listing links back into discovery after successful product fetches.
11. The admin API and Razor Pages console expose the operational and analytical view of source setup, discovery progress, product crawl progress, and catalogue quality.

## Current capabilities

The solution currently includes:

- category metadata and schema discovery for electrical-goods families
- category registry support for the current supported set: TVs, Monitors, Laptops, Smartphones, Tablets, Headphones, and Speakers
- schema-driven attribute normalisation with category-specific providers for the full supported rollout set across display, mobile, computing, and audio categories
- alias handling and measurement parsing
- structured data extraction from HTML and JSON-LD
- MongoDB persistence for source and canonical records
- MongoDB persistence for managed crawl sources and per-source throttling policy
- conservative source-candidate discovery with market and locale inference, representative-page probing, and explainable recommendation reasons
- source onboarding automation modes with explicit operator controls, guarded thresholds, and visible candidate-level explanations
- an optional local page-classification layer that helps distinguish likely product pages and source candidates from noisy or non-catalog content without replacing the existing heuristic pipeline
- evaluation and golden-dataset test coverage for the classification layer so it can be measured before it is relied on operationally
- source discovery profiles with category entry pages, sitemap hints, allow or deny rules, URL patterns, depth limits, and run budgets
- MongoDB persistence for discovered URLs and the discovery queue
- deterministic discovery infrastructure for sitemap parsing, listing traversal, product-page confirmation, and discovery link policy evaluation
- identity resolution across sources
- explainable merge weighting and conflict detection
- semantic delta detection for product changes
- worker orchestration with dedicated discovery and crawl workers, retry handling, and related-link expansion after successful product fetches
- admin endpoints for operational observability
- admin endpoints for category catalog management and crawl-source management
- admin endpoints for crawl job launch and tracking
- admin endpoints for product list, product detail, and product history inspection
- discovery-aware job, source, and dashboard views showing queue depth, discovered URL counts, confirmed product counts, failures, and per-category or source coverage
- quality analytics for coverage, unmapped attributes, source quality, and merge insights
- temporal intelligence for source trust history, attribute stability, and product change timelines
- adaptive crawl scheduling based on volatility, stability, freshness, and source behavior
- per-source disagreement tracking that feeds back into trust and merge decisions
- a Razor Pages operator console with source registration, category selection, seeded crawl launch, discovery-progress monitoring, product exploration, product detail explainability, quality dashboards, and source management

## Operator workflow

The current Milestone 1 flow is intentionally "boot and populate":

1. boot the API, web host, and worker against MongoDB
2. register or enable sources from the source registry
3. choose the active categories in the operator console
4. launch a seeded category crawl
5. watch discovery queue depth, discovered URL counts, confirmed product targets, crawl failures, and canonical product counts update in the dashboard and crawl-job detail views

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
- `Llm:Enabled`
- `Llm:EvaluationMode`
- `Llm:MaxContentLength`
- `Llm:ConfidenceThreshold`
- `Llm:TimeoutMs`

Admin API configuration lives in [ProductNormaliser.AdminApi/appsettings.json](ProductNormaliser.AdminApi/appsettings.json).

Set the Admin API key under `ManagementApiSecurity:ApiKeys` in [ProductNormaliser.AdminApi/appsettings.json](ProductNormaliser.AdminApi/appsettings.json) and [ProductNormaliser.AdminApi/appsettings.Development.json](ProductNormaliser.AdminApi/appsettings.Development.json). Requests must send that secret in the `X-Management-Api-Key` header unless you have explicitly enabled the loopback-only development bypass.

The optional classification layer is intentionally conservative. It is treated as one more signal in source evaluation and product-page validation, not as an autonomous decision-maker. If it is disabled, times out, or cannot load its local model, the platform continues with heuristics only.

## Running the worker

The worker is the engine that runs both deterministic discovery and product crawling.

```bash
dotnet run --project ProductNormaliser.Worker
```

The worker:

- scans eligible managed sources for discovery work
- expands sitemaps and listing pages into bounded discovery queues
- promotes confirmed product URLs into crawl targets
- fetches and extracts source data from product pages
- builds or updates source products
- merges into canonical products
- records crawl logs, conflicts, trust signals, change events, disagreement data, and discovery progress
- reschedules future attempts using adaptive backoff and discovery budgets

## Observability

The platform now exposes observability at two layers:

- structured lifecycle logs for crawl job creation, start, cancellation, per-target outcome recording, and terminal completion
- runtime telemetry via the `ProductNormaliser.Operations` `ActivitySource` and `Meter`
- persisted operational summary data via `GET /api/stats`
- operator-facing health panels on the web landing page for queue pressure, retry backlog, failure volume, at-risk sources, and category hotspots

The `Meter` emits counters and histograms for:

- crawl jobs created, started, and completed
- crawl job target outcomes by category and status
- queue dequeues, retries, and terminal outcomes
- processed crawl targets and extracted product counts
- crawl target duration and job target counts

The Admin API stats payload now includes:

- queue depth, retry depth, failed-queue depth, and active job count
- throughput and failure counts for the trailing 24 hours
- source-level health metrics derived from quality snapshots, queue state, and recent crawl logs
- category-level crawl pressure metrics derived from jobs, queue state, and recent crawl logs

### Verification status

Verified by automated tests:

- crawl job lifecycle logging is emitted during create, start, and completion flows
- stats aggregation includes the new operational summary from persisted jobs, queue items, crawl logs, sources, and quality snapshots
- the operator landing page renders the operational health panel and updated contract shape

Observed operationally rather than end-to-end tested:

- `ActivitySource` traces from the worker and crawl services
- `Meter` counters and histograms emitted at runtime for external collection
- the usefulness of the dashboard health summary under real crawl load patterns

## Running the admin API

The admin API is a read-side service over the same MongoDB database.

```bash
dotnet run --project ProductNormaliser.AdminApi
```

The checked-in bootstrap management key values are suitable only for local development and tests. Replace them in configuration before using the Admin API anywhere else.

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

### Crawl-job and product endpoints

- `GET /api/crawljobs`: list crawl jobs with filter and paging support
- `POST /api/crawljobs`: create a crawl job for categories, sources, or products
- `GET /api/crawljobs/{jobId}`: inspect one crawl job and its progress
- `GET /api/products`: list canonical products with quality-aware filtering and sorting
- `GET /api/products/{id}`: canonical product detail
- `GET /api/products/{id}/history`: product change timeline

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

- TV remains the deepest category implementation; monitor and laptop support are included in the Milestone 1 rollout but still need broader extraction coverage and richer normalisation rules over time
- queue write flows are currently aimed at internal operator use rather than a public ingestion API
- the admin surface now has API-key authentication and role-based operator access, but production identity, secret rotation, and perimeter hardening are still not fully formalized
- production deployment concerns such as distributed workers, secret management, and externalized observability are not yet formalized in the repo

## Why the project structure matters

The separation between Domain, Application, Infrastructure, Worker, and AdminApi is deliberate:

- Domain stays focused on product intelligence rules, contracts, and shared models
- Application owns orchestration and workflow validation
- Infrastructure implements persistence and external-system adapters
- Worker owns the write-side discovery and crawl pipeline
- AdminApi owns the read-side operational and analytical experience

That split keeps the domain logic explainable and testable while still allowing the runtime services to evolve independently.
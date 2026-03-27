# ProductNormaliser.Infrastructure

ProductNormaliser.Infrastructure contains the concrete adapters and services that make the Domain model operational. It is where MongoDB persistence, crawl and discovery queue handling, robots and HTTP access, deterministic discovery, structured data extraction, source-candidate probing, the optional local classification layer, delta detection, and long-term intelligence services are implemented.

If Domain defines what the system means, Infrastructure defines how those concepts are executed against real storage and external inputs.

## Responsibilities

- register MongoDB and all repository-backed stores
- implement persistence for source records, canonical records, discovery state, logs, conflicts, quality history, change history, and queue items
- implement the crawl queue and priority services
- provide HTTP fetching and robots policy handling
- implement deterministic sitemap and listing discovery under bounded policy rules
- extract structured product data from source pages
- probe representative pages and distinguish likely product sources from noise using heuristics plus an optional local classification layer
- compute delta, trust, stability, disagreement, and adaptive scheduling signals

## Main areas

### `Mongo`

Contains MongoDB configuration, database context, collection naming, mappings, and service registration.

The `AddProductNormaliserMongo` extension method wires up:

- Mongo client and database context
- repositories for category metadata, managed crawl sources, discovered URLs, discovery queue items, raw pages, source products, canonical products, offers, conflicts, queue, logs, unmapped attributes, source quality snapshots, change events, adaptive crawl policies, and source disagreements
- interface bindings used by the worker and API
- intelligence services for trust, stability, disagreement, and backoff

Mongo registration now also creates the collections and indexes needed for deterministic discovery, including bounded queue scans and deduplication across discovered URLs.

Index creation is now an explicit runtime concern rather than a test-only convention. `AddProductNormaliserMongo` registers a startup hosted service that calls the shared Mongo index catalog on host startup, so Worker and Admin API instances converge the database onto the same named index set every time they boot.

The catalog is intentionally aligned to repository and service access paths. Queue collections have dedicated scheduling indexes, source and canonical product collections cover identity and category matching, and the trust-history collections cover category and source time-series reads used by disagreement, trust, and change-event analysis.

### `Crawling`

Contains the crawl and refresh mechanics:

- `HttpFetcher`: retrieves page content
- `RobotsPolicyService`: evaluates whether and how a page should be fetched
- `CrawlQueueService`: manages dequeue, completion, skip, failure, and rescheduling behavior
- `CrawlPriorityService`: ranks future work based on freshness and information value
- `DeltaProcessor`: determines whether a newly extracted product meaningfully changed and emits semantic change detail
- `AdaptiveCrawlBackoffService`: converts behavior history into revisit cadence

### `Discovery`

Contains the deterministic discovery stack:

- `SitemapLocator`
- `SitemapParser`
- `ProductLinkExtractor`
- `ProductPageClassifier`
- `ListingPageClassifier`
- `DiscoveryLinkPolicy`
- `RelatedLinkExpansionService`

Source-candidate probing also lives nearby and combines representative-page fetching, structural heuristics, and the optional local classification layer before source candidates are recommended or rejected.

### `AI`

Contains the optional local classification and evaluation components:

- `LlamaPageClassificationService`
- `LlmOptions`
- `PageClassificationEvaluator`

This layer is intentionally conservative. It provides an extra page-level and source-probe signal, but the rest of the platform still relies on the existing heuristic and policy pipeline. The evaluator exists so the effect of this layer can be measured on a golden dataset before it is enabled more broadly.

These services are responsible for bounded traversal from source entry points and sitemap hints into candidate product URLs while enforcing source-level allowlists, deny rules, URL patterns, robots compliance, and max-depth or max-budget limits.

### `StructuredData`

Contains structured-data extraction and source-product construction. This is the bridge from raw page content into the domain model.

### `Intelligence`

Contains the longer-term quality memory services:

- `SourceTrustService`
- `AttributeStabilityService`
- `SourceDisagreementService`

These services let the system react to how a source behaves over time instead of treating source quality as a static score.

## Persistence model

Infrastructure is responsible for persisting the evidence trail, not just the final answer. That includes:

- raw page captures
- extracted source products
- canonical products
- offers
- merge conflicts
- crawl queue items
- discovered URLs
- discovery queue items
- crawl logs
- unmapped attributes
- source quality snapshots
- product change events
- adaptive crawl policies
- source disagreement records

This is one of the most important distinctions between ProductNormaliser and a simplistic product import job. The system retains enough state to explain how and why it evolved.

The newer managed source registry adds a dedicated `crawl_sources` collection so operator-controlled source state, category coverage, throttling policy, and discovery profile do not need to be inferred from queue or product data.

## Configuration

Mongo configuration is supplied through a `Mongo` section with:

- `ConnectionString`
- `DatabaseName`

Crawl behavior is supplied through a `Crawl` section with:

- `UserAgent`
- `DefaultHostDelayMilliseconds`
- `TransientRetryCount`
- `WorkerIdleDelayMilliseconds`
- host-specific delay overrides

Optional classification behavior is supplied through an `Llm` section with:

- `Enabled`
- `EvaluationMode`
- `ModelPath`
- `MaxContentLength`
- `ConfidenceThreshold`
- `TimeoutMs`

Those settings are typically provided by the Worker host and reused by any other host that calls the service-registration extension.

## Project references and packages

This project references the shared solution layers and brings in:

- MongoDB.Driver
- Microsoft.Extensions configuration and dependency-injection abstractions
- Microsoft.Extensions.Hosting.Abstractions for startup index initialization

That is intentional: Domain stays focused on shared concepts and rules, while Infrastructure owns external dependencies.

## Typical usage

Most code should consume Infrastructure through DI:

```csharp
builder.Services.AddProductNormaliserMongo(builder.Configuration);
```

That single registration call wires up the repositories and the major intelligence services used by both the worker and admin API.

## Build

```bash
dotnet build ProductNormaliser.Infrastructure/ProductNormaliser.Infrastructure.csproj
```

## When to change this project

Change Infrastructure when you need to:

- add or modify Mongo-backed stores
- change crawl scheduling or queue behavior
- improve source extraction
- refine delta detection behavior
- introduce new persistence-backed intelligence signals
- integrate an external service while keeping Domain and Application abstractions stable

## Comparison to commercial platforms

In commercial-platform terms, this project is the operational data-engineering layer. It is the part that turns ProductNormaliser from a set of rules into a continuously updating intelligence system. Where enterprise vendors often hide these mechanics behind proprietary pipelines, this project makes the mechanics explicit and inspectable.
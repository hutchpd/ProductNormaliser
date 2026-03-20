# ProductNormaliser.Infrastructure

ProductNormaliser.Infrastructure contains the concrete adapters and services that make the Core domain operational. It is where MongoDB persistence, crawl queue handling, robots and HTTP access, structured data extraction, delta detection, and long-term intelligence services are implemented.

If Core defines what the system means, Infrastructure defines how those concepts are executed against real storage and external inputs.

## Responsibilities

- register MongoDB and all repository-backed stores
- implement persistence for source records, canonical records, logs, conflicts, quality history, change history, and queue items
- implement the crawl queue and priority services
- provide HTTP fetching and robots policy handling
- extract structured product data from source pages
- compute delta, trust, stability, disagreement, and adaptive scheduling signals

## Main areas

### `Mongo`

Contains MongoDB configuration, database context, collection naming, mappings, and service registration.

The `AddProductNormaliserMongo` extension method wires up:

- Mongo client and database context
- repositories for category metadata, managed crawl sources, raw pages, source products, canonical products, offers, conflicts, queue, logs, unmapped attributes, source quality snapshots, change events, adaptive crawl policies, and source disagreements
- interface bindings used by the worker and API
- intelligence services for trust, stability, disagreement, and backoff

### `Crawling`

Contains the crawl and refresh mechanics:

- `HttpFetcher`: retrieves page content
- `RobotsPolicyService`: evaluates whether and how a page should be fetched
- `CrawlQueueService`: manages dequeue, completion, skip, failure, and rescheduling behavior
- `CrawlPriorityService`: ranks future work based on freshness and information value
- `DeltaProcessor`: determines whether a newly extracted product meaningfully changed and emits semantic change detail
- `AdaptiveCrawlBackoffService`: converts behavior history into revisit cadence

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
- crawl logs
- unmapped attributes
- source quality snapshots
- product change events
- adaptive crawl policies
- source disagreement records

This is one of the most important distinctions between ProductNormaliser and a simplistic product import job. The system retains enough state to explain how and why it evolved.

The newer managed source registry adds a dedicated `crawl_sources` collection so operator-controlled source state, category coverage, and throttling policy do not need to be inferred from queue or product data.

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

Those settings are typically provided by the Worker host and reused by any other host that calls the service-registration extension.

## Project references and packages

This project references ProductNormaliser.Core and brings in:

- MongoDB.Driver
- Microsoft.Extensions configuration and dependency-injection abstractions

That is intentional: Core stays domain-only, while Infrastructure owns external dependencies.

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
- integrate an external service while keeping Core abstractions stable

## Comparison to commercial platforms

In commercial-platform terms, this project is the operational data-engineering layer. It is the part that turns ProductNormaliser from a set of rules into a continuously updating intelligence system. Where enterprise vendors often hide these mechanics behind proprietary pipelines, this project makes the mechanics explicit and inspectable.
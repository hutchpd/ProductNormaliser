# ProductNormaliser.Worker

ProductNormaliser.Worker is the write-side runtime host for the solution. It continuously runs both deterministic discovery and product crawling: it expands source entry points and sitemaps into bounded discovery queues, promotes confirmed product URLs into crawl targets, fetches source pages, extracts and normalises product evidence, merges that evidence into canonical products, records logs and conflicts, and reschedules future work.

If ProductNormaliser.AdminApi is the observability surface, ProductNormaliser.Worker is the engine that keeps the database alive and current.

## Responsibilities

- host the background crawl loop
- host the background discovery loop
- compose the crawl pipeline through dependency injection
- process queued discovery and crawl targets one at a time per worker loop
- mark items completed, skipped, or failed
- ensure future attempts are rescheduled rather than treating crawl as a one-off event

## Runtime composition

At startup the worker registers:

- MongoDB stores and infrastructure services
- structured-data extraction
- attribute normalisation
- identity resolution
- merge weighting and canonical merge
- conflict detection
- HTTP fetcher and robots policy services
- discovery services and application-layer discovery coordination
- the hosted discovery and crawl worker services

The main entry point is intentionally thin. Most behavior lives in the orchestrator and infrastructure services.

## Key classes

- `Program`: DI composition root
- `DiscoveryWorker`: background loop that dequeues and processes discovery work
- `DiscoveryOrchestrator`: orchestration logic for sitemap traversal, listing traversal, URL classification, and promotion into crawl targets
- `CrawlWorker`: background loop that dequeues and processes work
- `CrawlOrchestrator`: orchestration logic for fetch, extract, normalise, merge, persistence, intelligence updates, and related-link expansion after successful product fetches
- `CrawlProcessResult`: result contract used to classify queue outcomes

## Configuration

The worker reads configuration from [appsettings.json](appsettings.json) and the environment.

Current default settings include:

- MongoDB connection string: `mongodb://127.0.0.1:27017`
- MongoDB database: `product_normaliser`
- crawl user agent: `ProductNormaliserBot/1.0`
- default host delay: 1000 ms
- transient retry count: 2
- idle delay when the queue is empty: 1500 ms

## How to run

From the repository root:

```bash
dotnet run --project ProductNormaliser.Worker
```

Or from this project folder:

```bash
dotnet run
```

## How the processing loop behaves

For each discovery lease, the worker:

1. dequeues the next eligible discovery item
2. evaluates robots and throttling rules for the source
3. fetches sitemaps or listing pages within source depth and budget limits
4. classifies and persists discovered URLs
5. promotes confirmed product URLs into the crawl queue

For each crawl lease, the worker:

1. dequeues the next eligible crawl target
2. calls the crawl orchestrator
3. marks the item as completed, skipped, or failed
4. allows the queue service to determine future scheduling

If the queue is empty, the worker sleeps for the configured idle delay and tries again.

If processing throws an unexpected exception, the worker logs the failure and marks the item failed so queue-state history is preserved.

## Operational expectations

- This service can bootstrap category crawls from managed source discovery profiles, so the crawl queue no longer needs to be fully pre-seeded with product URLs.
- It shares its MongoDB database with the admin API.
- It is designed as a long-running background process.
- Its value increases over time because trust history, stability, and disagreement data accumulate across runs.

## Local development checklist

1. Start MongoDB.
2. Ensure the `Mongo` configuration points at the correct instance.
3. Register or enable crawl sources with category coverage and discovery profiles through the Admin API or Web UI.
4. Run the worker.
5. Optionally run the admin API and Web UI to inspect discovery and crawl progress.

## Build

```bash
dotnet build ProductNormaliser.Worker/ProductNormaliser.Worker.csproj
```

## What this project is not

- it is not the place for business rules that should live in Domain or Application
- it is not the read-side API surface
- it is not a scheduler UI or queue-management console

It is intentionally a thin runtime host over the reusable platform services.
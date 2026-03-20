# ProductNormaliser.Worker

ProductNormaliser.Worker is the write-side runtime host for the solution. It continuously pulls items from the crawl queue, fetches source pages, extracts and normalises product evidence, merges that evidence into canonical products, records logs and conflicts, and reschedules the next crawl attempt.

If ProductNormaliser.AdminApi is the observability surface, ProductNormaliser.Worker is the engine that keeps the database alive and current.

## Responsibilities

- host the background crawl loop
- compose the crawl pipeline through dependency injection
- process queued crawl targets one at a time per worker loop
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
- the hosted crawl worker service

The main entry point is intentionally thin. Most behavior lives in the orchestrator and infrastructure services.

## Key classes

- `Program`: DI composition root
- `CrawlWorker`: background loop that dequeues and processes work
- `CrawlOrchestrator`: orchestration logic for fetch, extract, normalise, merge, persistence, and intelligence updates
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

For each queue lease, the worker:

1. dequeues the next eligible crawl target
2. calls the crawl orchestrator
3. marks the item as completed, skipped, or failed
4. allows the queue service to determine future scheduling

If the queue is empty, the worker sleeps for the configured idle delay and tries again.

If processing throws an unexpected exception, the worker logs the failure and marks the item failed so queue-state history is preserved.

## Operational expectations

- This service expects the crawl queue to already contain targets.
- It shares its MongoDB database with the admin API.
- It is designed as a long-running background process.
- Its value increases over time because trust history, stability, and disagreement data accumulate across runs.

## Local development checklist

1. Start MongoDB.
2. Ensure the `Mongo` configuration points at the correct instance.
3. Seed queue items through application code, tests, or scripts.
4. Run the worker.
5. Optionally run the admin API to inspect results.

## Build

```bash
dotnet build ProductNormaliser.Worker/ProductNormaliser.Worker.csproj
```

## What this project is not

- it is not the place for core business rules that should live in Core
- it is not the read-side API surface
- it is not a scheduler UI or queue-management console

It is intentionally a thin runtime host over the reusable platform services.
# ProductNormaliser.AdminApi

ProductNormaliser.AdminApi is the read-side operational and intelligence API for the platform. It exposes queue state, crawl logs, conflicts, canonical product detail, change history, and quality analytics over the shared MongoDB database.

This is an internal-facing service designed to help operators, analysts, and developers inspect what the system is doing and why.

## Responsibilities

- expose operational monitoring endpoints
- expose product and change-history views
- expose quality and source-intelligence analytics
- translate persisted domain records into API DTOs suitable for inspection or dashboarding

## Runtime composition

At startup the API registers:

- ASP.NET Core controllers
- OpenAPI support for development
- MongoDB-backed stores and infrastructure services
- `IAdminQueryService` for operational read models
- `IDataIntelligenceService` for quality and historical intelligence read models

## Main controllers and endpoints

### Stats

- `GET /api/stats`

Returns a high-level operational summary.

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

- `GET /api/products/{id}`
- `GET /api/products/{id}/history`

These endpoints let you inspect the canonical record and the time series of meaningful change events.

### Quality and intelligence

- `GET /api/quality/coverage/detailed`
- `GET /api/quality/unmapped`
- `GET /api/quality/sources`
- `GET /api/quality/merge-insights`
- `GET /api/quality/source-history`
- `GET /api/quality/attribute-stability`
- `GET /api/quality/source-disagreements`

Most of these endpoints accept a `categoryKey` query parameter and default to the `tv` category.

The `source-history` and `source-disagreements` endpoints also support optional source filtering.

## Services

The two main read-model services are:

- `IAdminQueryService`: crawl logs, queue state, product detail, conflict lists, stats, and product history
- `IDataIntelligenceService`: coverage, unmapped attributes, source quality, merge insights, source history, attribute stability, and disagreement analytics

These services isolate controller logic from the shape of the underlying persisted model.

## Configuration

Configuration is read from [appsettings.json](appsettings.json) and the environment.

The default configuration currently includes logging levels and `AllowedHosts`.

Because the API reads from MongoDB through shared infrastructure registration, it also needs the same `Mongo` settings used by the worker when running outside the existing local defaults.

## How to run

From the repository root:

```bash
dotnet run --project ProductNormaliser.AdminApi
```

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

- this API is read-oriented; it does not currently expose queue-write or ingestion-management endpoints
- authentication and authorization are not configured as a complete security model yet
- it is best treated as an operational admin surface, not a public internet API

## Build

```bash
dotnet build ProductNormaliser.AdminApi/ProductNormaliser.AdminApi.csproj
```

## Why this project matters

Commercial product-intelligence systems are often difficult to interrogate when data quality is questioned. This project is the inspection layer that makes ProductNormaliser operationally useful: it exposes the queue, the evidence trail, and the longitudinal quality signals needed to trust the system.
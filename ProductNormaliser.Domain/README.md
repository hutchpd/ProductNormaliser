# ProductNormaliser.Domain

ProductNormaliser.Domain contains the domain model and decision logic for the solution. If you want to understand what a product is, how attributes are represented, how source records become canonical records, and which extension points the rest of the system depends on, this is the project to read first.

This project does not talk to MongoDB, HTTP, or ASP.NET directly. It defines the rules and contracts that the infrastructure and runtime hosts implement.

## Responsibilities

- define core product, evidence, offer, queue, conflict, history, and quality models
- define category schema, metadata, and canonical attribute definitions for electrical-goods families
- define interfaces for extraction, normalisation, identity resolution, merge, trust, stability, disagreement, and backoff services
- implement merge logic and conflict detection
- hold the canonical vocabulary that all other projects share

## Main folders

- `Models`: domain objects such as source products, canonical products, queue items, conflicts, change events, and trust snapshots
- `Interfaces`: cross-project contracts implemented by infrastructure or runtime services
- `Merging`: canonical merge logic, weighting, and conflict detection
- `Normalisation`: attribute name/value normalisation and unit conversion logic
- `Schemas`: category schema definitions and metadata providers for supported electrical-goods categories

## Key models

Important model groups include:

- source capture: `RawPage`, `SourceProduct`, `SourceAttributeValue`, `ProductOffer`
- canonical state: `CanonicalProduct`, `CanonicalAttributeValue`, `AttributeEvidence`
- identity and merge: `ProductFingerprint`, `ProductIdentityMatchResult`, `MergeConflict`
- quality and time: `SourceQualitySnapshot`, `AttributeStabilityScore`, `ProductChangeEvent`, `SourceAttributeDisagreement`
- crawl intelligence: `CrawlQueueItem`, `CrawlContext`, `AdaptiveCrawlPolicy`, `PageVolatilityProfile`

These types are the shared language of the system. Infrastructure persists them. The worker updates them. The admin API projects them into DTOs.

## Core interfaces

The main extension points defined here are:

- `IAttributeNormaliser`: maps raw source attributes to canonical attributes and normalised values
- `IStructuredDataExtractor`: extracts product-like data from source HTML or structured markup
- `IProductIdentityResolver`: decides whether a source product matches an existing canonical product
- `ICanonicalMergeService`: merges source evidence into a canonical product
- `IConflictDetector`: identifies unresolved attribute disagreements worth surfacing
- `ISourceTrustService`: computes and retrieves time-aware source trust signals
- `IAttributeStabilityService`: measures how stable an attribute has been over time
- `ISourceDisagreementService`: tracks where a source repeatedly diverges from consensus
- `ICrawlBackoffService`: calculates future revisit timing based on page and source behavior
- `IUnmappedAttributeRecorder`: captures unknown attributes for schema feedback loops

These interfaces are what make the solution composable. You can replace implementations without rewriting the domain model.

## Merge behavior

The merge subsystem is central to the solution.

It combines:

- source quality
- attribute reliability
- recency
- historical trust
- attribute stability
- disagreement patterns

The goal is not just to pick a winner, but to pick a winner with an audit trail. Canonical attribute values retain evidence and weighting information so downstream users can inspect why a value won.

## Category schema

The first fully modelled category is `tv` in the TV schema provider. Additional category providers for `monitor`, `laptop`, and `refrigerator` now exist so the rest of the platform can score completeness, route normalisation, and expose dashboard metadata without assuming TV-only semantics.

The TV schema still defines the richest required and optional canonical attributes such as:

- brand
- model number
- screen size
- resolution
- display technology
- HDMI ports
- smart platform
- physical dimensions

Adding a new category typically means:

1. adding a new schema provider
2. teaching the normaliser how to map source names and values
3. deciding identity heuristics and attribute reliability rules for that category
4. registering metadata so the admin API and web dashboard can discover the category safely

## How other projects use Domain

- Infrastructure implements many of the Domain interfaces and persists Domain models.
- Application is the new orchestration seam that will coordinate use cases over the Domain model.
- Worker composes Domain services into a write-side crawl pipeline.
- AdminApi reads the persisted Domain model and serves operational views of it.
- Tests validate the decision logic against fixtures and integration scenarios.

## Build

You usually build this project as part of the solution:

```bash
dotnet build ProductNormaliser.slnx
```

Or individually:

```bash
dotnet build ProductNormaliser.Domain/ProductNormaliser.Domain.csproj
```

## When to change this project

Change Domain when you need to:

- introduce a new canonical concept
- adjust merge or conflict semantics
- add a new intelligence signal to the domain
- define a new abstraction the rest of the system should depend on

Do not put storage-specific or transport-specific concerns here unless they truly belong in the domain model.

## Comparison to commercial product-intelligence platforms

If the root project is the platform, Domain is the rules engine behind that platform. In commercial terms, this is the part that gives ProductNormaliser its explainability advantage over black-box catalog consolidation. It defines how product truth is reasoned about rather than just how records are stored.
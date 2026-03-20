# ProductNormaliser.Application

ProductNormaliser.Application is the new application-layer seam for the solution.

Its purpose is to host use cases, orchestration services, command and query flows, and category-agnostic workflow logic that should sit above the domain model but below the runtime hosts.

At this restructuring stage, the project is still intentionally lean, but it now contains the first real dashboard-facing orchestration services for category and source management. It exists so host-specific controllers and pages can stay thin while rules and workflow validation move into a transport-agnostic layer.

## Why this project exists

- separate use-case orchestration from the Domain model
- give AdminApi and Worker a stable application boundary to depend on
- provide a clean place for category-selection, crawl-request, and dashboard-facing workflows in later phases
- prepare for broader electrical-goods support without pushing host-specific logic into Domain or Infrastructure

## Expected future contents

- crawl request and scheduling use cases
- category registry and commodity-selection services
- product search and dashboard query handlers
- DTO mapping that belongs to application workflows rather than persistence
- interfaces that Infrastructure can implement without leaking MongoDB concerns upward

## Current contents

The application layer now includes:

- `CategoryManagementService` for category listing, family grouping, schema lookup, enabled-category filtering, and combined metadata plus schema detail payloads
- `SourceManagementService` for source registration, update, enable or disable, category assignment, and throttling validation
- store abstractions that let Mongo-backed infrastructure stay behind application-facing contracts

These services are what the Admin API and Web dashboard now consume for management workflows.

## Build

```bash
dotnet build ProductNormaliser.Application/ProductNormaliser.Application.csproj
```
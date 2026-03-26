# ProductNormaliser.AdminApi.Tests

ProductNormaliser.AdminApi.Tests is the focused test project for API-host behavior.

It is intended to hold tests for:

- controller behavior
- API composition and DI wiring
- endpoint contract behavior
- read-model projection behavior close to the API boundary
- management authorization over category, source, and crawl-job endpoints
- discovery-aware source and crawl-job contract projections
- source automation and source-candidate discovery contract projections used by the operator UI

## Build

```bash
dotnet build ProductNormaliser.AdminApi.Tests/ProductNormaliser.AdminApi.Tests.csproj
```

## Test

```bash
dotnet test ProductNormaliser.AdminApi.Tests/ProductNormaliser.AdminApi.Tests.csproj
```
# ProductNormaliser.Web.Tests

ProductNormaliser.Web.Tests is the focused test project for the web UI host.

It is intended to hold tests for:

- UI host composition
- page and component behavior
- HTTP client integration boundaries to backend APIs
- frontend-specific view and interaction logic as the dashboard grows
- source registration and source-management flows
- category-seeded crawl launch validation and operator boot-and-populate workflow rendering
- source-candidate discovery rendering, guarded automation controls, and classification-informed operator explanations

## Build

```bash
dotnet build ProductNormaliser.Web.Tests/ProductNormaliser.Web.Tests.csproj
```

## Test

```bash
dotnet test ProductNormaliser.Web.Tests/ProductNormaliser.Web.Tests.csproj
```
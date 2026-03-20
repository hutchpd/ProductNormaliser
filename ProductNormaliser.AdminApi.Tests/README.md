# ProductNormaliser.AdminApi.Tests

ProductNormaliser.AdminApi.Tests is the focused test project for API-host behavior.

It is intended to hold tests for:

- controller behavior
- API composition and DI wiring
- endpoint contract behavior
- read-model projection behavior close to the API boundary

## Build

```bash
dotnet build ProductNormaliser.AdminApi.Tests/ProductNormaliser.AdminApi.Tests.csproj
```

## Test

```bash
dotnet test ProductNormaliser.AdminApi.Tests/ProductNormaliser.AdminApi.Tests.csproj
```
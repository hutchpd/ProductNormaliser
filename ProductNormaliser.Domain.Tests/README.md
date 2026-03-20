# ProductNormaliser.Domain.Tests

ProductNormaliser.Domain.Tests is the focused test project for domain-level behavior.

It is intended to hold tests for:

- category schemas
- normalisation rules
- merge weighting
- identity rules
- conflict semantics
- domain-only utility behavior

## Build

```bash
dotnet build ProductNormaliser.Domain.Tests/ProductNormaliser.Domain.Tests.csproj
```

## Test

```bash
dotnet test ProductNormaliser.Domain.Tests/ProductNormaliser.Domain.Tests.csproj
```
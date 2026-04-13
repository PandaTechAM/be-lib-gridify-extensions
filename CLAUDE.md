# Pandatech.GridifyExtensions - Claude Guide

## What is this?

A .NET NuGet library extending [Gridify](https://github.com/alirezanet/Gridify) with streamlined filtering, ordering,
pagination, aggregation, and column distinct value queries for EF Core. Used by all PandaTech backend projects.

## Architecture

- Single library project: `src/GridifyExtensions/`
- All extension methods live in `QueryableExtensions.cs`
- `FilterMapper<T>` extends Gridify's `GridifyMapper<T>` with default ordering, encrypted columns, and fluent API
- Mapper instances are discovered via reflection at startup (`AddGridify()`) and stored in a `FrozenDictionary`
- `WebApplicationBuilderExtensions.cs` handles registration and Gridify global config

## Key Files

- `Extensions/QueryableExtensions.cs` - core extension methods (filtering, paging, distinct, aggregation)
- `Extensions/WebApplicationBuilderExtensions.cs` - DI registration and mapper discovery
- `Models/FilterMapper.cs` - base mapper class with default ordering and encrypted column tracking
- `Models/GridifyQueryModel.cs` - paged query model (inherits Gridify's GridifyQuery)
- `Models/GridifyCursoredQueryModel.cs` - cursor-based query model (standalone, not inheriting GridifyQuery)
- `Models/AggregateQueryModel.cs` - aggregation model (standalone: Filter + PropertyName + AggregateType)
- `Models/ColumnDistinctValueCursoredQueryModel.cs` - distinct value query (extends GridifyCursoredQueryModel)
- `Operators/FlagOperator.cs` - custom `#hasFlag` bitwise operator

## Code Patterns

- `GridifyQueryModel` inherits from Gridify's `GridifyQuery` and overrides properties for validation
- `GridifyCursoredQueryModel` and `AggregateQueryModel` are standalone (no Gridify base) with internal
  `ToGridifyQueryModel()` for interop with Gridify's filtering
- Encrypted columns tracked via `HashSet<string>` in FilterMapper; decryption happens client-side via `Func<byte[], string>`
- String distinct values use smart ordering: nulls first, exact match, length, alphabetical
- `PagedResponse<T>` and `CursoredResponse<T>` are records

## Build

```bash
dotnet build src/GridifyExtensions/GridifyExtensions.csproj
```

Targets: net8.0, net9.0, net10.0

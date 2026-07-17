# Swevo.EFCore.JsonColumn

[![NuGet](https://img.shields.io/nuget/v/Swevo.EFCore.JsonColumn.svg)](https://www.nuget.org/packages/Swevo.EFCore.JsonColumn)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.JsonColumn.svg)](https://www.nuget.org/packages/Swevo.EFCore.JsonColumn)
[![CI](https://github.com/Swevo/EFCore.JsonColumn/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/EFCore.JsonColumn/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Compile-time JSON column configuration for EF Core 8+. Stamp `[JsonColumn]` on owned navigation properties and the source generator emits `ConfigureJsonColumns(ModelBuilder)` so you do not have to hand-write repetitive `OwnsOne(..., b => b.ToJson())` boilerplate.

## Quick-start

```bash
dotnet add package Swevo.EFCore.JsonColumn
```

### 1 — Mark JSON-owned navigations

```csharp
using EFCore.JsonColumn;

public class Address
{
    public string Street { get; set; } = "";
    public string City { get; set; } = "";
}

public class Order
{
    public int Id { get; set; }

    [JsonColumn]
    public Address ShippingAddress { get; set; } = new();
}
```

### 2 — Configure your DbContext

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureJsonColumns();
}
```

### 3 — Generated output

```csharp
modelBuilder.Entity<global::MyApp.Order>(entity =>
{
    entity.OwnsOne(e => e.ShippingAddress, b => b.ToJson());
});
```

## How it works

- `[JsonColumn]` marks an owned reference navigation for JSON-column configuration
- The incremental source generator scans the full compilation for annotated properties
- One generated `ConfigureJsonColumns(ModelBuilder)` method groups properties by owning entity
- Each property becomes `entity.OwnsOne(e => e.PropertyName, b => b.ToJson())`

## Requirements

- .NET 8+
- EF Core 8+
- JSON-column properties must be reference types

Applying `[JsonColumn]` to a value type produces warning `JSCOL001`.


## Also by the same author

> 🌐 Full suite overview: **[swevo.github.io](https://swevo.github.io/)**

| Package | Description |
|---|---|
| [**AutoLog.Generator**](https://github.com/Swevo/AutoLog.Generator) | Compile-time high-performance logging — `[Log(Level, Message)]` generates `LoggerMessage.Define`. AOT-safe. |
| [**AutoHttpClient.Generator**](https://github.com/Swevo/AutoHttpClient.Generator) | Compile-time typed HTTP client — `[HttpClient]` on an interface generates a strongly-typed client. AOT-safe Refit alternative. |
| [**AutoDispatch.Generator**](https://github.com/Swevo/AutoDispatch.Generator) | Compile-time CQRS dispatcher — `[Handler]` generates a strongly-typed `IDispatcher`. No MediatR, no reflection. |
| [**AutoWire**](https://github.com/Swevo/AutoWire) | Compile-time DI auto-registration — `[Scoped]`/`[Singleton]`/`[Transient]` generates `IServiceCollection` registration code. |
| [**AutoMap.Generator**](https://github.com/Swevo/AutoMap.Generator) | Compile-time object mapping with generated extension methods. AOT-safe AutoMapper alternative. |

## Related Packages

| Package | Downloads | Description |
|---|---|---|
| [Swevo.EFCore.Outbox](https://www.nuget.org/packages/Swevo.EFCore.Outbox) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.Outbox.svg)](https://www.nuget.org/packages/Swevo.EFCore.Outbox) | Transactional outbox pattern for EF Core + AutoBus |
| [Swevo.EFCore.StronglyTyped](https://www.nuget.org/packages/Swevo.EFCore.StronglyTyped) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.StronglyTyped.svg)](https://www.nuget.org/packages/Swevo.EFCore.StronglyTyped) | Compile-time strongly-typed ID generation for  |
| [Swevo.EFCore.SoftDelete](https://www.nuget.org/packages/Swevo.EFCore.SoftDelete) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.SoftDelete.svg)](https://www.nuget.org/packages/Swevo.EFCore.SoftDelete) | Compile-time soft-delete generation for EF Core entities using Roslyn source generators |
| [Swevo.EFCore.Seeding](https://www.nuget.org/packages/Swevo.EFCore.Seeding) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.Seeding.svg)](https://www.nuget.org/packages/Swevo.EFCore.Seeding) | Fluent, idempotent, dependency-ordered seed data for EF Core |
| [Swevo.EFCore.Pagination](https://www.nuget.org/packages/Swevo.EFCore.Pagination) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.Pagination.svg)](https://www.nuget.org/packages/Swevo.EFCore.Pagination) | Offset and cursor-based pagination for EF Core |
| [Swevo.EFCore.BulkOperations](https://www.nuget.org/packages/Swevo.EFCore.BulkOperations) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.BulkOperations.svg)](https://www.nuget.org/packages/Swevo.EFCore.BulkOperations) | Free, MIT-licensed bulk insert/update/delete for EF Core |
| [Swevo.EFCore.MultiTenant](https://www.nuget.org/packages/Swevo.EFCore.MultiTenant) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.MultiTenant.svg)](https://www.nuget.org/packages/Swevo.EFCore.MultiTenant) | Compile-time multi-tenancy for EF Core |
| [Swevo.EFCore.RowVersion](https://www.nuget.org/packages/Swevo.EFCore.RowVersion) | [![Downloads](https://img.shields.io/nuget/dt/Swevo.EFCore.RowVersion.svg)](https://www.nuget.org/packages/Swevo.EFCore.RowVersion) | Compile-time optimistic concurrency for EF Core — [Optimistic] source generator adds RowVersion property, IOptimisticEntity, and SaveChangesClientWinsAsync / SaveChangesDatabaseWinsAsync retry extensions |

---

## License

MIT

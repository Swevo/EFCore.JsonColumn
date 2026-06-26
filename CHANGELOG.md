# Changelog

All notable changes to `Swevo.EFCore.JsonColumn` will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-06-26

### Added
- `[JsonColumn]` attribute for marking EF Core owned reference navigations for JSON-column configuration
- Incremental source generator that emits `ConfigureJsonColumns(ModelBuilder)` for every annotated property in the compilation
- Support for multiple JSON columns per entity and multiple entities per compilation
- Diagnostic `JSCOL001` when `[JsonColumn]` is applied to a value-type property
- SQLite integration tests proving `OwnsOne(..., b => b.ToJson())` round-trips JSON data through EF Core

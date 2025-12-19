# MapPiloteGeopackageHelper

Modern .NET library for creating, reading, and bulk-loading GeoPackage (GPKG) data using SQLite and NetTopologySuite.

- [MapPiloteGeopackageHelper](https://github.com/kartpiloten/MapPiloteGeopackageHelper) — The core library with tests
- [MapPiloteGeopackageHelperExamples](https://github.com/kartpiloten/MapPiloteGeopackageHelperExamples) — Example projects using the published NuGet package

## What This Library Does

* Creates GeoPackages with required core tables  
* Creates layers (tables) with geometry + custom attribute columns  
* Bulk writes features with validation and progress tracking  
* Streams features back with filtering, sorting, and paging  
* Modern async patterns with cancellation support  
* Schema inspection and validation  
* Optional WAL mode for improved concurrency and performance

## Quick Start - Modern Fluent API

```csharp
// Create/open GeoPackage with fluent API
using var geoPackage = await GeoPackage.OpenAsync("data.gpkg", defaultSrid: 3006);

// Create layer with schema
var layer = await geoPackage.EnsureLayerAsync("cities", new Dictionary<string, string>
{
    ["name"] = "TEXT",
    ["population"] = "INTEGER"
});

// Create features with geometry
var features = new[]
{
    new FeatureRecord(
        new Point(674188, 6580251),  // Stockholm (SWEREF99 TM)
        new Dictionary<string, string?> { ["name"] = "Stockholm", ["population"] = "975000" }),
    new FeatureRecord(
        new Point(319178, 6399617),  // Gothenburg
        new Dictionary<string, string?> { ["name"] = "Gothenburg", ["population"] = "583000" })
};

// Bulk insert with validation and progress
var progress = new Progress<BulkProgress>(p => 
    Console.WriteLine($"Progress: {p.PercentComplete:F1}%"));

await layer.BulkInsertAsync(features, 
    new BulkInsertOptions(BatchSize: 1000, ValidateGeometryType: true),
    progress);

// Query and access both geometry and attributes
await foreach (var city in layer.ReadFeaturesAsync(
    new ReadOptions(WhereClause: "population > 100000", OrderBy: "population DESC")))
{
    var point = (Point)city.Geometry!;
    Console.WriteLine($"{city.Attributes["name"]}: {city.Attributes["population"]} people at ({point.X}, {point.Y})");
}
```

## Modern Features

| Feature | Description | Example |
|---------|-------------|---------|
| **Async/Await** | Proper async support with CancellationToken | `await layer.BulkInsertAsync(...)` |
| **Fluent API** | Chain operations naturally | `GeoPackage.OpenAsync().EnsureLayerAsync()` |
| **Progress Reporting** | Track long-running operations | `IProgress<BulkProgress>` |
| **Options Objects** | Clean configuration, no parameter soup | `BulkInsertOptions(BatchSize: 1000)` |
| **Streaming** | `IAsyncEnumerable` for large datasets | `await foreach (var item in ...)` |
| **Rich Queries** | WHERE, LIMIT, ORDER BY support | `ReadOptions(OrderBy: "score DESC")` |
| **Conflict Handling** | Insert policies (Abort/Ignore/Replace) | `ConflictPolicy.Ignore` |
| **CRUD Operations** | Count, Delete with conditions | `await layer.DeleteAsync("status = 'old'")` |
| **WAL Mode** | Write-Ahead Logging for concurrency | `CreateGeoPackage(path, walMode: true)` |
| **Input Validation** | SQL injection protection and parameter validation | Automatic identifier sanitization |
| **Geometry Validation** | Optional strict geometry type checking | `new BulkInsertOptions(ValidateGeometryType: true)` |
| **QGIS Compatibility** | Auto-update `gpkg_contents` extents with buffer | Automatic after inserts |

## WAL Mode Support

Enable WAL (Write-Ahead Logging) mode for better concurrency and performance:

```csharp
// Create GeoPackage with WAL mode enabled
CMPGeopackageCreateHelper.CreateGeoPackage(
    "data.gpkg", 
    srid: 3006,
    walMode: true,
    onStatus: Console.WriteLine);

```

### WAL Mode Benefits
- **Better Concurrency**: Multiple readers can access the database while a writer is active
- **Improved Performance**: Better performance for write-heavy workloads  
- **Atomic Commits**: Better crash recovery and data integrity
- **No Manual PRAGMA**: No need to manually execute `PRAGMA journal_mode = WAL`

## Getting Started

1. **Install**: `dotnet add package MapPiloteGeopackageHelper`
2. **Explore**: Check out the [MapPiloteGeopackageHelperExamples](https://github.com/kartpiloten/MapPiloteGeopackageHelperExamples) repository for example projects
3. **Inspect**: Use `CMPGeopackageReadDataHelper.GetGeopackageInfo()` to inspect unknown GeoPackage files
4. **Learn**: Traditional API patterns are also available in the examples repository

Open the generated `.gpkg` files in QGIS, ArcGIS, or any GIS software!

## Reference Links (GeoPackage Specification)

- **GeoPackage Encoding Standard** - https://www.geopackage.org/spec/
- **OGC Standard page** - https://www.ogc.org/standard/geopackage/
- **Core tables (spec sections)**
  - gpkg_contents: https://www.geopackage.org/spec/#_contents
  - gpkg_spatial_ref_sys: https://www.geopackage.org/spec/#_spatial_ref_sys
  - gpkg_geometry_columns: https://www.geopackage.org/spec/#_geometry_columns
- **Binary geometry format** - https://www.geopackage.org/spec/#gpb_format

## Version History

### v1.4.1
- Improve geometry type validation and test output handling
- Add geometry type validation to bulk inserts and fluent API (optional strict mode)
- Auto-update layer extents in `gpkg_contents` for QGIS compatibility
- Add comprehensive geometry type tests (all OGC types, validation, QGIS behavior)


### v1.4.0
- **Security**: Added SQL injection protection via identifier validation for table and column names
- **Robustness**: Transaction rollback on failure in bulk insert operations
- **Robustness**: Input validation for BatchSize, SRID, Limit, and Offset parameters
- **Fixed**: `SetupSpatialReferenceSystem` now properly uses the `srid` parameter instead of always inserting default SRIDs
- **Fixed**: `CreateSpatialIndex` option in `BulkInsertOptions` is now implemented
- **Improved**: Centralized `CreateGpkgBlob` utility (removed duplicate implementations)
- **Improved**: Streaming support in `BulkInsertAsync` - only materializes features when progress reporting is requested
- **Improved**: Better exception handling with specific exception types instead of generic catch blocks
- **Documentation**: Added comprehensive XML documentation to all public types and members
- **Documentation**: XML documentation file now included in NuGet package

### v1.3.1
- **Updated**: Targeting .NET 10

### v1.2.2 
- **Fixed**: ORDER BY clause in `ReadFeaturesAsync` now works correctly with both ASC and DESC
- **Test Coverage**: Added comprehensive ORDER BY tests

### v1.2.1
- **Added**: WAL mode support for better concurrency
- **Split**: Repository separated from examples for clarity

### v1.2.0
- **Added**: Modern Fluent API with async/await support
- **Added**: Progress reporting for bulk operations
- **Added**: Rich query options (WHERE, LIMIT, OFFSET, ORDER BY)

## Support & Sustainability

**MapPiloteGeopackageHelper** is an open-source .NET library for working with GeoPackage data,
maintained by a single developer in spare time.

If you use this library in professional, commercial, or public-sector projects, please consider
supporting its continued maintenance and development.

### Ways to support

- ⭐ Star the repository to increase visibility
- 💬 Report issues and suggest improvements
- ❤️ Sponsor ongoing maintenance via GitHub Sponsors

Commercial support, consulting, and custom development are available for organizations using
MapPiloteGeopackageHelper in production environments.

> Sustainable open source enables better tools for everyone.

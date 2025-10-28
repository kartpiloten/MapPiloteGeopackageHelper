# MapPiloteGeopackageHelper

Modern .NET library for creating, reading, and bulk-loading GeoPackage (GPKG) data using SQLite and NetTopologySuite.

**Latest Release: v1.2.2** - Fixed ORDER BY clause in `ReadFeaturesAsync` (ASC/DESC sorting now works correctly)

Note that in version 1.2.1 I have split the repository on github into two, hope it simplifies understanding:
- [MapPiloteGeopackageHelper](https://github.com/kartpiloten/MapPiloteGeopackageHelper)
    The core library with tests.
- [MapPiloteGeopackageHelperExamples](https://github.com/kartpiloten/MapPiloteGeopackageHelperExamples)
Example projects that uses the latest published NuGet package.

## Quick Start - Modern Fluent API

```csharp
// Create/open GeoPackage with fluent API
using var geoPackage = await GeoPackage.OpenAsync("data.gpkg", srid: 3006);

// Create layer with schema
var layer = await geoPackage.EnsureLayerAsync("cities", new Dictionary<string, string>
{
    ["name"] = "TEXT",
    ["population"] = "INTEGER",
    ["area_km2"] = "REAL"
});

// Bulk insert with progress
var progress = new Progress<BulkProgress>(p => 
    Console.WriteLine($"Progress: {p.PercentComplete:F1}%"));

await layer.BulkInsertAsync(features, 
    new BulkInsertOptions(BatchSize: 1000, CreateSpatialIndex: true),
    progress);

// Query with async streaming and ORDER BY support
await foreach (var city in layer.ReadFeaturesAsync(
    new ReadOptions(
        WhereClause: "population > 100000", 
        OrderBy: "population DESC",  // ? Fixed in v1.2.2
        Limit: 10)))
{
    Console.WriteLine($"City: {city.Attributes["name"]} - {city.Attributes["population"]} people");
}

// Count and delete operations
var count = await layer.CountAsync("population < 50000");
var deleted = await layer.DeleteAsync("population < 10000");
```

## WAL Mode Support (New!)

Enable WAL (Write-Ahead Logging) mode for better concurrency and performance:

```csharp
// Create GeoPackage with WAL mode enabled
CMPGeopackageCreateHelper.CreateGeoPackage(
    "data.gpkg", 
    srid: 3006,
    walMode: true,
    onStatus: Console.WriteLine);

// WAL mode with default SRID (3006)
CMPGeopackageCreateHelper.CreateGeoPackage("data.gpkg", walMode: true);

// Backward compatible - existing code continues to work
CMPGeopackageCreateHelper.CreateGeoPackage("data.gpkg", 3006);
```

### WAL Mode Benefits:
- **Better Concurrency**: Multiple readers can access the database while a writer is active
- **Improved Performance**: Better performance for write-heavy workloads  
- **Atomic Commits**: Better crash recovery and data integrity
- **No Manual PRAGMA**: No need to manually execute `PRAGMA journal_mode = WAL`

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

## API Comparison

### Modern API (Recommended)
```csharp
// One-liner with progress and options
using var gp = await GeoPackage.OpenAsync("data.gpkg");
var layer = await gp.EnsureLayerAsync("places", schema);
await layer.BulkInsertAsync(features, options, progress);
```

### Traditional API (Still Supported)
```csharp
// Multi-step process with optional WAL mode
CMPGeopackageCreateHelper.CreateGeoPackage(path, srid, walMode: true);
GeopackageLayerCreateHelper.CreateGeopackageLayer(path, name, schema);
CGeopackageAddDataHelper.BulkInsertFeatures(path, name, features);
```

### Available CreateGeoPackage Overloads
```csharp
// Basic creation
CMPGeopackageCreateHelper.CreateGeoPackage("path.gpkg");

// With custom SRID
CMPGeopackageCreateHelper.CreateGeoPackage("path.gpkg", srid: 4326);

// With status callback
CMPGeopackageCreateHelper.CreateGeoPackage("path.gpkg", onStatus: Console.WriteLine);

// With SRID and callback
CMPGeopackageCreateHelper.CreateGeoPackage("path.gpkg", 4326, Console.WriteLine);

// With WAL mode (default SRID)
CMPGeopackageCreateHelper.CreateGeoPackage("path.gpkg", walMode: true);

// Full control: SRID + WAL + callback
CMPGeopackageCreateHelper.CreateGeoPackage("path.gpkg", 4326, true, Console.WriteLine);
```

## Version History

### v1.2.2 (Latest)
- **Fixed**: ORDER BY clause in `ReadFeaturesAsync` now works correctly with both ASC and DESC
- **Test Coverage**: Added comprehensive ORDER BY tests

### v1.2.1
- **Added**: WAL mode support for better concurrency
- **Split**: Repository separated from examples for clarity

### v1.2.0
- **Added**: Modern Fluent API with async/await support
- **Added**: Progress reporting for bulk operations
- **Added**: Rich query options (WHERE, LIMIT, OFFSET, ORDER BY)

## Reference Links (GeoPackage Specification)

- **GeoPackage Encoding Standard** - https://www.geopackage.org/spec/
- **OGC Standard page** - https://www.ogc.org/standard/geopackage/
- **Core tables (spec sections)**
  - gpkg_contents: https://www.geopackage.org/spec/#_contents
  - gpkg_spatial_ref_sys: https://www.geopackage.org/spec/#_spatial_ref_sys
  - gpkg_geometry_columns: https://www.geopackage.org/spec/#_geometry_columns
- **Binary geometry format** - https://www.geopackage.org/spec/#gpb_format

## What This Library Does

* Creates GeoPackages with required core tables  
* Creates layers (tables) with geometry + custom attribute columns  
* Bulk writes features with validation and progress tracking  
* Streams features back with filtering, sorting, and paging  
* Modern async patterns with cancellation support  
* Schema inspection and validation  
* Optional WAL mode for improved concurrency and performance

## Getting Started

1. **Install**: `dotnet add package MapPiloteGeopackageHelper`
2. **Explore**: Check out `FluentApiExample` project 
3. **Inspect**: Use `MapPiloteGeopackageHelperSchemaBrowser` for unknown files
4. **Learn**: Traditional patterns in `MapPiloteGeopackageHelperHelloWorld`

Open the generated `.gpkg` files in QGIS, ArcGIS, or any GIS software!


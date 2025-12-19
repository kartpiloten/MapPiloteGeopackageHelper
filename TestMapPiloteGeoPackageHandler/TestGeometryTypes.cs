/* Licence...
 * MIT License
 *
 * Copyright (c) 2025 Anders Dahlgren
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy 
 * of this software and associated documentation files (the "Software"), to deal 
 * in the Software without restriction, including without limitation the rights 
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
 * copies of the Software, and to permit persons to whom the Software is 
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE 
 * SOFTWARE.
 */
using MapPiloteGeopackageHelper;
using NetTopologySuite.Geometries;

namespace TestMapPiloteGeoPackageHandler;

/// <summary>
/// Tests for geometry type handling in GeoPackage layers.
/// Output files are saved to TestResults/GeoPackages folder for inspection in QGIS.
/// </summary>
[TestClass]
public class TestGeometryTypes
{
    private static readonly GeometryFactory _geometryFactory = new(new PrecisionModel(), 3006);

    /// <summary>
    /// Creates a GeoPackage with layers for each geometry type and inserts sample features.
    /// </summary>
    [TestMethod]
    public void CreateLayersWithAllGeometryTypes_ShouldStoreCorrectTypes()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        // Create the GeoPackage
        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);

        var schema = new Dictionary<string, string>
        {
            ["name"] = "TEXT",
            ["value"] = "INTEGER"
        };

        // Create layers for each geometry type and insert sample features
        var geometryTypes = new (string GeomType, Func<int, Geometry> CreateGeom)[]
        {
            ("POINT", i => new Point(500000 + i * 100, 6500000 + i * 100)),
            ("LINESTRING", i => _geometryFactory.CreateLineString(new[]
            {
                new Coordinate(500000 + i * 100, 6500000),
                new Coordinate(500000 + i * 100 + 50, 6500050),
                new Coordinate(500000 + i * 100 + 100, 6500000)
            })),
            ("POLYGON", i => _geometryFactory.CreatePolygon(new[]
            {
                new Coordinate(500000 + i * 200, 6500000),
                new Coordinate(500000 + i * 200 + 100, 6500000),
                new Coordinate(500000 + i * 200 + 100, 6500100),
                new Coordinate(500000 + i * 200, 6500100),
                new Coordinate(500000 + i * 200, 6500000)
            })),
            ("MULTIPOINT", i => _geometryFactory.CreateMultiPoint(new[]
            {
                new Point(500000 + i * 100, 6500000),
                new Point(500000 + i * 100 + 20, 6500020),
                new Point(500000 + i * 100 + 40, 6500000)
            })),
            ("MULTILINESTRING", i => _geometryFactory.CreateMultiLineString(new[]
            {
                _geometryFactory.CreateLineString(new[] { new Coordinate(500000 + i * 100, 6500000), new Coordinate(500000 + i * 100 + 50, 6500050) }),
                _geometryFactory.CreateLineString(new[] { new Coordinate(500000 + i * 100, 6500100), new Coordinate(500000 + i * 100 + 50, 6500150) })
            })),
            ("MULTIPOLYGON", i => _geometryFactory.CreateMultiPolygon(new[]
            {
                _geometryFactory.CreatePolygon(new[] { new Coordinate(500000 + i * 200, 6500000), new Coordinate(500000 + i * 200 + 50, 6500000), new Coordinate(500000 + i * 200 + 50, 6500050), new Coordinate(500000 + i * 200, 6500050), new Coordinate(500000 + i * 200, 6500000) }),
                _geometryFactory.CreatePolygon(new[] { new Coordinate(500000 + i * 200 + 100, 6500000), new Coordinate(500000 + i * 200 + 150, 6500000), new Coordinate(500000 + i * 200 + 150, 6500050), new Coordinate(500000 + i * 200 + 100, 6500050), new Coordinate(500000 + i * 200 + 100, 6500000) })
            })),
            ("GEOMETRY", i => i % 3 == 0 
                ? new Point(500000 + i * 100, 6500000) 
                : i % 3 == 1 
                    ? _geometryFactory.CreateLineString(new[] { new Coordinate(500000 + i * 100, 6500000), new Coordinate(500000 + i * 100 + 50, 6500050) })
                    : _geometryFactory.CreatePolygon(new[] { new Coordinate(500000 + i * 200, 6500000), new Coordinate(500000 + i * 200 + 50, 6500000), new Coordinate(500000 + i * 200 + 50, 6500050), new Coordinate(500000 + i * 200, 6500050), new Coordinate(500000 + i * 200, 6500000) }))
        };

        foreach (var (geomType, createGeom) in geometryTypes)
        {
            string layerName = $"layer_{geomType.ToLower()}";
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, layerName, schema, geomType, 3006);

            // Insert 5 sample features into each layer
            var features = Enumerable.Range(1, 5).Select(i => new FeatureRecord(
                createGeom(i),
                new Dictionary<string, string?> { ["name"] = $"{geomType}_{i}", ["value"] = (i * 10).ToString() }
            )).ToList();

            CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, layerName, features, 3006);
        }

        // Verify geometry types in gpkg_geometry_columns
        var info = CMPGeopackageReadDataHelper.GetGeopackageInfo(gpkg);

        foreach (var (geomType, _) in geometryTypes)
        {
            string layerName = $"layer_{geomType.ToLower()}";
            var layer = info.Layers.FirstOrDefault(l => l.TableName == layerName);
            
            Assert.IsNotNull(layer, $"Layer '{layerName}' should exist");
            Assert.AreEqual(geomType, layer.GeometryType, $"Layer '{layerName}' should have geometry type '{geomType}'");

            // Verify features were inserted
            var features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, layerName).ToList();
            Assert.AreEqual(5, features.Count, $"Layer '{layerName}' should have 5 features");
        }

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// Inserts 5 Point geometries into a POINT layer
    /// </summary>
    [TestMethod]
    public void InsertPointsIntoPointLayer_ShouldSucceed()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);
        var schema = new Dictionary<string, string> { ["name"] = "TEXT" };
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "points", schema, "POINT", 3006);

        var features = Enumerable.Range(1, 5).Select(i => new FeatureRecord(
            new Point(500000 + i * 100, 6500000 + i * 100),
            new Dictionary<string, string?> { ["name"] = $"Point_{i}" }
        )).ToList();

        CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "points", features, 3006);

        // Verify all 5 features were inserted
        var readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "points").ToList();
        Assert.AreEqual(5, readBack.Count);

        // Verify all geometries are Points
        foreach (var feature in readBack)
        {
            Assert.IsInstanceOfType(feature.Geometry, typeof(Point), "All geometries should be Points");
        }

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// Inserts 5 LineString geometries into a LINESTRING layer
    /// </summary>
    [TestMethod]
    public void InsertLinestringIntoLinestringLayer_ShouldSucceed()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);
        var schema = new Dictionary<string, string> { ["name"] = "TEXT", ["length_m"] = "REAL" };
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "lines", schema, "LINESTRING", 3006);

        var features = Enumerable.Range(1, 5).Select(i => 
        {
            var coords = new[]
            {
                new Coordinate(500000 + i * 100, 6500000),
                new Coordinate(500000 + i * 100 + 50, 6500050),
                new Coordinate(500000 + i * 100 + 100, 6500000)
            };
            var line = _geometryFactory.CreateLineString(coords);
            return new FeatureRecord(
                line,
                new Dictionary<string, string?> { ["name"] = $"Line_{i}", ["length_m"] = line.Length.ToString("F2") }
            );
        }).ToList();

        CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "lines", features, 3006);

        var readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "lines").ToList();
        Assert.AreEqual(5, readBack.Count);

        foreach (var feature in readBack)
        {
            Assert.IsInstanceOfType(feature.Geometry, typeof(LineString), "All geometries should be LineStrings");
        }

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// Inserts 5 Polygon geometries into a POLYGON layer
    /// </summary>
    [TestMethod]
    public void InsertPolygonsIntoPolygonLayer_ShouldSucceed()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);
        var schema = new Dictionary<string, string> { ["name"] = "TEXT", ["area_m2"] = "REAL" };
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "polygons", schema, "POLYGON", 3006);

        var features = Enumerable.Range(1, 5).Select(i => 
        {
            double baseX = 500000 + i * 200;
            double baseY = 6500000;
            var coords = new[]
            {
                new Coordinate(baseX, baseY),
                new Coordinate(baseX + 100, baseY),
                new Coordinate(baseX + 100, baseY + 100),
                new Coordinate(baseX, baseY + 100),
                new Coordinate(baseX, baseY)
            };
            var polygon = _geometryFactory.CreatePolygon(coords);
            return new FeatureRecord(
                polygon,
                new Dictionary<string, string?> { ["name"] = $"Polygon_{i}", ["area_m2"] = polygon.Area.ToString("F2") }
            );
        }).ToList();

        CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "polygons", features, 3006);

        var readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "polygons").ToList();
        Assert.AreEqual(5, readBack.Count);

        foreach (var feature in readBack)
        {
            Assert.IsInstanceOfType(feature.Geometry, typeof(Polygon), "All geometries should be Polygons");
        }

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// Inserts 5 MultiPoint geometries into a MULTIPOINT layer
    /// </summary>
    [TestMethod]
    public void InsertMultiPointsIntoMultiPointLayer_ShouldSucceed()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);
        var schema = new Dictionary<string, string> { ["name"] = "TEXT", ["point_count"] = "INTEGER" };
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "multipoints", schema, "MULTIPOINT", 3006);

        var features = Enumerable.Range(1, 5).Select(i => 
        {
            var points = Enumerable.Range(1, 3).Select(j => 
                new Point(500000 + i * 100 + j * 10, 6500000 + j * 10)).ToArray();
            var multiPoint = _geometryFactory.CreateMultiPoint(points);
            return new FeatureRecord(
                multiPoint,
                new Dictionary<string, string?> { ["name"] = $"MultiPoint_{i}", ["point_count"] = "3" }
            );
        }).ToList();

        CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "multipoints", features, 3006);

        var readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "multipoints").ToList();
        Assert.AreEqual(5, readBack.Count);

        foreach (var feature in readBack)
        {
            Assert.IsInstanceOfType(feature.Geometry, typeof(MultiPoint), "All geometries should be MultiPoints");
        }

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// Inserts 5 MultiLineString geometries into a MULTILINESTRING layer
    /// </summary>
    [TestMethod]
    public void InsertMultiLinestringsIntoMultiLinestringLayer_ShouldSucceed()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);
        var schema = new Dictionary<string, string> { ["name"] = "TEXT" };
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "multilines", schema, "MULTILINESTRING", 3006);

        var features = Enumerable.Range(1, 5).Select(i => 
        {
            var lines = new[]
            {
                _geometryFactory.CreateLineString(new[]
                {
                    new Coordinate(500000 + i * 100, 6500000),
                    new Coordinate(500000 + i * 100 + 50, 6500050)
                }),
                _geometryFactory.CreateLineString(new[]
                {
                    new Coordinate(500000 + i * 100, 6500100),
                    new Coordinate(500000 + i * 100 + 50, 6500150)
                })
            };
            var multiLine = _geometryFactory.CreateMultiLineString(lines);
            return new FeatureRecord(
                multiLine,
                new Dictionary<string, string?> { ["name"] = $"MultiLine_{i}" }
            );
        }).ToList();

        CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "multilines", features, 3006);

        var readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "multilines").ToList();
        Assert.AreEqual(5, readBack.Count);

        foreach (var feature in readBack)
        {
            Assert.IsInstanceOfType(feature.Geometry, typeof(MultiLineString), "All geometries should be MultiLineStrings");
        }

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// Inserts 5 MultiPolygon geometries into a MULTIPOLYGON layer
    /// </summary>
    [TestMethod]
    public void InsertMultiPolygonsIntoMultiPolygonLayer_ShouldSucceed()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);
        var schema = new Dictionary<string, string> { ["name"] = "TEXT" };
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "multipolygons", schema, "MULTIPOLYGON", 3006);

        var features = Enumerable.Range(1, 5).Select(i => 
        {
            var polygons = new[]
            {
                _geometryFactory.CreatePolygon(new[]
                {
                    new Coordinate(500000 + i * 200, 6500000),
                    new Coordinate(500000 + i * 200 + 50, 6500000),
                    new Coordinate(500000 + i * 200 + 50, 6500050),
                    new Coordinate(500000 + i * 200, 6500050),
                    new Coordinate(500000 + i * 200, 6500000)
                }),
                _geometryFactory.CreatePolygon(new[]
                {
                    new Coordinate(500000 + i * 200 + 100, 6500000),
                    new Coordinate(500000 + i * 200 + 150, 6500000),
                    new Coordinate(500000 + i * 200 + 150, 6500050),
                    new Coordinate(500000 + i * 200 + 100, 6500050),
                    new Coordinate(500000 + i * 200 + 100, 6500000)
                })
            };
            var multiPolygon = _geometryFactory.CreateMultiPolygon(polygons);
            return new FeatureRecord(
                multiPolygon,
                new Dictionary<string, string?> { ["name"] = $"MultiPolygon_{i}" }
            );
        }).ToList();

        CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "multipolygons", features, 3006);

        var readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "multipolygons").ToList();
        Assert.AreEqual(5, readBack.Count);

        foreach (var feature in readBack)
        {
            Assert.IsInstanceOfType(feature.Geometry, typeof(MultiPolygon), "All geometries should be MultiPolygons");
        }

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// GEOMETRY type layer should accept any geometry type (flexible layer)
    /// </summary>
    [TestMethod]
    public void InsertMixedGeometriesIntoGeometryLayer_ShouldSucceed()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);
        var schema = new Dictionary<string, string> { ["name"] = "TEXT", ["geom_type"] = "TEXT" };
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "mixed", schema, "GEOMETRY", 3006);

        var features = new List<FeatureRecord>
        {
            new FeatureRecord(
                new Point(500000, 6500000),
                new Dictionary<string, string?> { ["name"] = "Point", ["geom_type"] = "Point" }
            ),
            new FeatureRecord(
                _geometryFactory.CreateLineString(new[] { new Coordinate(500100, 6500000), new Coordinate(500200, 6500100) }),
                new Dictionary<string, string?> { ["name"] = "Line", ["geom_type"] = "LineString" }
            ),
            new FeatureRecord(
                _geometryFactory.CreatePolygon(new[] 
                { 
                    new Coordinate(500300, 6500000), 
                    new Coordinate(500400, 6500000), 
                    new Coordinate(500400, 6500100), 
                    new Coordinate(500300, 6500100), 
                    new Coordinate(500300, 6500000) 
                }),
                new Dictionary<string, string?> { ["name"] = "Polygon", ["geom_type"] = "Polygon" }
            )
        };

        CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "mixed", features, 3006);

        var readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "mixed").ToList();
        Assert.AreEqual(3, readBack.Count);

        var geomTypes = readBack.Select(f => f.Geometry?.GeometryType).Distinct().ToList();
        Assert.IsTrue(geomTypes.Count >= 3, "Should have at least 3 different geometry types");

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// Demonstrates that QGIS silently accepts wrong geometry types but doesn't display them.
    /// </summary>
    [TestMethod]
    public void InsertLinestringIntoPointLayer_CurrentBehavior_NoValidation()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);
        var schema = new Dictionary<string, string> { ["name"] = "TEXT" };
        
        GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "points", schema, "POINT", 3006);

        var line = _geometryFactory.CreateLineString(new[]
        {
            new Coordinate(500000, 6500000),
            new Coordinate(500100, 6500100)
        });

        var features = new List<FeatureRecord>
        {
            new FeatureRecord(line, new Dictionary<string, string?> { ["name"] = "WrongType" })
        };

        CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "points", features, 3006);

        var readBack = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "points").ToList();
        Assert.AreEqual(1, readBack.Count);

        Assert.IsInstanceOfType(readBack[0].Geometry, typeof(LineString),
            "Currently, wrong geometry types are accepted without validation");

        var info = CMPGeopackageReadDataHelper.GetGeopackageInfo(gpkg);
        var layer = info.Layers.First(l => l.TableName == "points");
        Assert.AreEqual("POINT", layer.GeometryType, 
            "Layer metadata says POINT, but contains LineString - this causes display issues in QGIS!");

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// Tests that geometry type validation can be enabled via the Fluent API.
    /// </summary>
    [TestMethod]
    public async Task FluentApi_WithGeometryTypeValidation_ShouldRejectWrongTypes()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        using var geoPackage = await GeoPackage.OpenAsync(gpkg, 3006);
        var schema = new Dictionary<string, string> { ["name"] = "TEXT" };
        
        var layer = await geoPackage.EnsureLayerAsync("points", schema, 3006, "POINT");

        var line = _geometryFactory.CreateLineString(new[]
        {
            new Coordinate(500000, 6500000),
            new Coordinate(500100, 6500100)
        });

        var features = new List<FeatureRecord>
        {
            new FeatureRecord(line, new Dictionary<string, string?> { ["name"] = "WrongType" })
        };

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(async () =>
        {
            await layer.BulkInsertAsync(
                features, 
                new BulkInsertOptions(ValidateGeometryType: true));
        });

        Assert.IsTrue(exception.Message.Contains("Geometry type mismatch"));

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// Tests that geometry type validation accepts correct types
    /// </summary>
    [TestMethod]
    public async Task FluentApi_WithGeometryTypeValidation_ShouldAcceptCorrectTypes()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        using var geoPackage = await GeoPackage.OpenAsync(gpkg, 3006);
        var schema = new Dictionary<string, string> { ["name"] = "TEXT" };
        
        var layer = await geoPackage.EnsureLayerAsync("lines", schema, 3006, "LINESTRING");

        var line = _geometryFactory.CreateLineString(new[]
        {
            new Coordinate(500000, 6500000),
            new Coordinate(500100, 6500100)
        });

        var features = new List<FeatureRecord>
        {
            new FeatureRecord(line, new Dictionary<string, string?> { ["name"] = "CorrectType" })
        };

        await layer.BulkInsertAsync(
            features, 
            new BulkInsertOptions(ValidateGeometryType: true));

        var count = await layer.CountAsync();
        Assert.AreEqual(1, count);

        TestOutputHelper.LogOutputLocation(gpkg);
    }

    /// <summary>
    /// Comprehensive test that creates a GeoPackage with all geometry types
    /// </summary>
    [TestMethod]
    public void CreateComprehensiveGeoPackageWithAllTypes_ShouldSucceed()
    {
        string gpkg = TestOutputHelper.GetTestOutputPath();

        CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);

        var schema = new Dictionary<string, string>
        {
            ["name"] = "TEXT",
            ["description"] = "TEXT",
            ["value"] = "REAL"
        };

        var testCases = new (string LayerName, string GeomType, Func<int, Geometry> CreateGeom)[]
        {
            ("cities", "POINT", i => new Point(500000 + i * 1000, 6500000 + i * 1000)),
            ("roads", "LINESTRING", i => _geometryFactory.CreateLineString(new[]
            {
                new Coordinate(500000 + i * 1000, 6500000),
                new Coordinate(500000 + i * 1000 + 500, 6500500),
                new Coordinate(500000 + i * 1000 + 1000, 6500000)
            })),
            ("parcels", "POLYGON", i => _geometryFactory.CreatePolygon(new[]
            {
                new Coordinate(500000 + i * 1000, 6500000),
                new Coordinate(500000 + i * 1000 + 800, 6500000),
                new Coordinate(500000 + i * 1000 + 800, 6500800),
                new Coordinate(500000 + i * 1000, 6500800),
                new Coordinate(500000 + i * 1000, 6500000)
            }))
        };

        foreach (var (layerName, geomType, createGeom) in testCases)
        {
            GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, layerName, schema, geomType, 3006);

            var features = Enumerable.Range(1, 5).Select(i => new FeatureRecord(
                createGeom(i),
                new Dictionary<string, string?>
                {
                    ["name"] = $"{geomType}_{i}",
                    ["description"] = $"Test {geomType} feature number {i}",
                    ["value"] = (i * 100.5).ToString("F2")
                }
            )).ToList();

            CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, layerName, features, 3006);
        }

        var info = CMPGeopackageReadDataHelper.GetGeopackageInfo(gpkg);
        Assert.AreEqual(3, info.Layers.Count);

        foreach (var (layerName, geomType, _) in testCases)
        {
            var layer = info.Layers.FirstOrDefault(l => l.TableName == layerName);
            Assert.IsNotNull(layer, $"Layer '{layerName}' should exist");
            Assert.AreEqual(geomType, layer.GeometryType, $"Layer '{layerName}' should have geometry type '{geomType}'");

            var features = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, layerName).ToList();
            Assert.AreEqual(5, features.Count, $"Layer '{layerName}' should have 5 features");
        }

        TestOutputHelper.LogOutputLocation(gpkg);
    }
}

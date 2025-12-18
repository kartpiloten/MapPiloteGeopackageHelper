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

[TestClass]
public class TestFluentApi
{
    private static string CreateTempGpkgPath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"fluent_test_{Guid.NewGuid():N}.gpkg");
        return path;
    }

    [TestMethod]
    public async Task FluentApi_CreateAndReadFeatures_ShouldWork()
    {
        string gpkg = CreateTempGpkgPath();
        try
        {
            Dictionary<string, string> schema = new()
            {
                ["name"] = "TEXT",
                ["value"] = "INTEGER"
            };

            FeatureRecord[] features = new[]
            {
                new FeatureRecord(
                    new Point(100, 200),
                    new Dictionary<string, string?> { ["name"] = "Test1", ["value"] = "42" }
                ),
                new FeatureRecord(
                    new Point(300, 400),
                    new Dictionary<string, string?> { ["name"] = "Test2", ["value"] = "84" }
                )
            };

            using GeoPackage geoPackage = await GeoPackage.OpenAsync(gpkg);
            GeoPackageLayer layer = await geoPackage.EnsureLayerAsync("test_layer", schema);
            
            await layer.BulkInsertAsync(features, new BulkInsertOptions(BatchSize: 1));
            
            List<FeatureRecord> readBack = [];
            await foreach (FeatureRecord feature in layer.ReadFeaturesAsync())
            {
                readBack.Add(feature);
            }

            Assert.AreEqual(2, readBack.Count);
            
            FeatureRecord first = readBack.First();
            Assert.IsNotNull(first.Geometry);
            Assert.AreEqual("Test1", first.Attributes["name"]);
            Assert.AreEqual("42", first.Attributes["value"]);
            
            Point point = (Point)first.Geometry!;
            Assert.AreEqual(100.0, point.X, 1e-9);
            Assert.AreEqual(200.0, point.Y, 1e-9);
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public async Task FluentApi_CountAndDelete_ShouldWork()
    {
        string gpkg = CreateTempGpkgPath();
        try
        {
            Dictionary<string, string> schema = new() { ["status"] = "TEXT" };
            List<FeatureRecord> features = Enumerable.Range(1, 10).Select(i => 
                new FeatureRecord(
                    new Point(i, i),
                    new Dictionary<string, string?> { ["status"] = i <= 5 ? "active" : "inactive" }
                )).ToList();

            using GeoPackage geoPackage = await GeoPackage.OpenAsync(gpkg);
            GeoPackageLayer layer = await geoPackage.EnsureLayerAsync("test_layer", schema);
            
            await layer.BulkInsertAsync(features);
            
            long totalCount = await layer.CountAsync();
            long activeCount = await layer.CountAsync("status = 'active'");
            int deletedCount = await layer.DeleteAsync("status = 'inactive'");
            long remainingCount = await layer.CountAsync();

            Assert.AreEqual(10, totalCount);
            Assert.AreEqual(5, activeCount);
            Assert.AreEqual(5, deletedCount);
            Assert.AreEqual(5, remainingCount);
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public async Task FluentApi_ReadWithOptions_ShouldWork()
    {
        string gpkg = CreateTempGpkgPath();
        try
        {
            Dictionary<string, string> schema = new() { ["rank"] = "INTEGER" };
            List<FeatureRecord> features = Enumerable.Range(1, 20).Select(i => 
                new FeatureRecord(
                    new Point(i * 10, i * 20),
                    new Dictionary<string, string?> { ["rank"] = i.ToString() }
                )).ToList();

            using GeoPackage geoPackage = await GeoPackage.OpenAsync(gpkg);
            GeoPackageLayer layer = await geoPackage.EnsureLayerAsync("test_layer", schema);
            
            await layer.BulkInsertAsync(features);
            
            List<FeatureRecord> limited = [];
            await foreach (FeatureRecord feature in layer.ReadFeaturesAsync(new ReadOptions(Limit: 5)))
            {
                limited.Add(feature);
            }

            List<FeatureRecord> filtered = [];
            await foreach (FeatureRecord feature in layer.ReadFeaturesAsync(new ReadOptions(WhereClause: "rank > 15")))
            {
                filtered.Add(feature);
            }

            Assert.AreEqual(5, limited.Count);
            Assert.AreEqual(5, filtered.Count);
            
            foreach (FeatureRecord feature in filtered)
            {
                int rank = int.Parse(feature.Attributes["rank"]!);
                Assert.IsTrue(rank > 15);
            }
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public async Task FluentApi_ConflictPolicies_ShouldWork()
    {
        string gpkg = CreateTempGpkgPath();
        try
        {
            Dictionary<string, string> schema = new() { ["unique_id"] = "INTEGER", ["data"] = "TEXT" };
            
            FeatureRecord initialFeature = new(
                new Point(100, 100),
                new Dictionary<string, string?> { ["unique_id"] = "1", ["data"] = "original" }
            );

            FeatureRecord conflictFeature = new(
                new Point(200, 200),
                new Dictionary<string, string?> { ["unique_id"] = "1", ["data"] = "updated" }
            );

            using GeoPackage geoPackage = await GeoPackage.OpenAsync(gpkg);
            GeoPackageLayer layer = await geoPackage.EnsureLayerAsync("test_layer", schema);
            
            await layer.BulkInsertAsync(new[] { initialFeature });
            long countAfterFirst = await layer.CountAsync();
            
            await layer.BulkInsertAsync(
                new[] { conflictFeature }, 
                new BulkInsertOptions(ConflictPolicy: ConflictPolicy.Ignore));
            long countAfterIgnore = await layer.CountAsync();

            Assert.AreEqual(1, countAfterFirst);
            Assert.AreEqual(2, countAfterIgnore);
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    [TestMethod]
    public async Task FluentApi_OrderBy_ShouldWork()
    {
        string gpkg = CreateTempGpkgPath();
        try
        {
            Dictionary<string, string> schema = new() { ["score"] = "INTEGER", ["name"] = "TEXT" };
            FeatureRecord[] features = new[]
            {
                new FeatureRecord(new Point(1, 1), new Dictionary<string, string?> { ["score"] = "50", ["name"] = "Bob" }),
                new FeatureRecord(new Point(2, 2), new Dictionary<string, string?> { ["score"] = "90", ["name"] = "Alice" }),
                new FeatureRecord(new Point(3, 3), new Dictionary<string, string?> { ["score"] = "70", ["name"] = "Charlie" }),
                new FeatureRecord(new Point(4, 4), new Dictionary<string, string?> { ["score"] = "60", ["name"] = "Diana" }),
            };

            using GeoPackage geoPackage = await GeoPackage.OpenAsync(gpkg);
            GeoPackageLayer layer = await geoPackage.EnsureLayerAsync("test_layer", schema);
            await layer.BulkInsertAsync(features);

            List<FeatureRecord> ascResults = [];
            await foreach (FeatureRecord feature in layer.ReadFeaturesAsync(new ReadOptions(OrderBy: "score ASC")))
            {
                ascResults.Add(feature);
            }

            List<FeatureRecord> descResults = [];
            await foreach (FeatureRecord feature in layer.ReadFeaturesAsync(new ReadOptions(OrderBy: "score DESC")))
            {
                descResults.Add(feature);
            }

            Assert.AreEqual("Bob", ascResults[0].Attributes["name"]);
            Assert.AreEqual("Diana", ascResults[1].Attributes["name"]);
            Assert.AreEqual("Charlie", ascResults[2].Attributes["name"]);
            Assert.AreEqual("Alice", ascResults[3].Attributes["name"]);

            Assert.AreEqual("Alice", descResults[0].Attributes["name"]);
            Assert.AreEqual("Charlie", descResults[1].Attributes["name"]);
            Assert.AreEqual("Diana", descResults[2].Attributes["name"]);
            Assert.AreEqual("Bob", descResults[3].Attributes["name"]);
        }
        finally
        {
            TryDeleteFile(gpkg);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
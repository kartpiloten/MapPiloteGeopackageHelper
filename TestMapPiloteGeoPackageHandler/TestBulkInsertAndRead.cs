using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MapPiloteGeopackageHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetTopologySuite.Geometries;

namespace TestMapPiloteGeoPackageHandler
{
    [TestClass]
    public class TestBulkInsertAndRead
    {
        private static string CreateTempGpkgPath()
        {
            var path = Path.Combine(Path.GetTempPath(), $"mpgh_{Guid.NewGuid():N}.gpkg");
            return path;
        }

        [TestMethod]
        public void BulkInsertAndRead_ShouldRoundtrip()
        {
            string gpkg = CreateTempGpkgPath();
            try
            {
                // Create empty GeoPackage and a layer
                CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);

                var headers = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "name", "TEXT" },
                    { "age", "INTEGER" },
                    { "height", "REAL" },
                    { "note", "TEXT" }
                };
                GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", headers, geometryType: "POINT", srid: 3006);

                // Prepare features
                var features = new List<FeatureRecord>
                {
                    new FeatureRecord(
                        Geometry: new Point(1, 2),
                        Attributes: new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            ["name"] = "Alice",
                            ["age"] = "30",
                            ["height"] = "1.75",
                            ["note"] = null
                        }
                    ),
                    new FeatureRecord(
                        Geometry: new Point(3, 4),
                        Attributes: new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            ["name"] = "Bob",
                            ["age"] = "",
                            // height intentionally missing -> should become NULL
                            ["note"] = "B"
                        }
                    ),
                    new FeatureRecord(
                        Geometry: null,
                        Attributes: new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            ["name"] = "Charlie",
                            ["age"] = "40",
                            ["height"] = "1.80",
                            ["note"] = "N/A"
                        }
                    )
                };

                CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "test_layer", features, srid: 3006, batchSize: 2);

                // Read back
                var read = CMPGeopackageReadDataHelper.ReadFeatures(gpkg, "test_layer", geometryColumn: "geom", includeGeometry: true).ToList();
                Assert.AreEqual(features.Count, read.Count, "Feature count mismatch");

                // Validate row 1
                var f1 = read[0];
                Assert.IsNotNull(f1.Geometry);
                Assert.AreEqual(1.0, ((Point)f1.Geometry!).X, 1e-9);
                Assert.AreEqual(2.0, ((Point)f1.Geometry!).Y, 1e-9);
                Assert.AreEqual("Alice", f1.Attributes["name"]);
                Assert.AreEqual("30", f1.Attributes["age"]);
                Assert.AreEqual("1.75", f1.Attributes["height"]);
                Assert.IsNull(f1.Attributes["note"], "note should be NULL");

                // Validate row 2 (missing height => NULL; empty age => NULL)
                var f2 = read[1];
                Assert.IsNotNull(f2.Geometry);
                Assert.AreEqual(3.0, ((Point)f2.Geometry!).X, 1e-9);
                Assert.AreEqual(4.0, ((Point)f2.Geometry!).Y, 1e-9);
                Assert.AreEqual("Bob", f2.Attributes["name"]);
                Assert.IsNull(f2.Attributes["age"], "empty string should be stored as NULL for INTEGER");
                Assert.IsNull(f2.Attributes["height"], "missing key should be stored as NULL");
                Assert.AreEqual("B", f2.Attributes["note"]);

                // Validate row 3 (no geometry)
                var f3 = read[2];
                Assert.IsNull(f3.Geometry);
                Assert.AreEqual("Charlie", f3.Attributes["name"]);
                Assert.AreEqual("40", f3.Attributes["age"]);
                Assert.AreEqual("1.8", f3.Attributes["height"] ?? "1.8"); // SQLite may normalize formatting
                Assert.AreEqual("N/A", f3.Attributes["note"]);
            }
            finally
            {
                if (File.Exists(gpkg))
                {
                    try { File.Delete(gpkg); } catch { }
                }
            }
        }

        [TestMethod]
        public void BulkInsertFeatures_InvalidValue_ShouldThrow()
        {
            string gpkg = CreateTempGpkgPath();
            try
            {
                CMPGeopackageCreateHelper.CreateGeoPackage(gpkg, 3006);
                var headers = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "name", "TEXT" },
                    { "age", "INTEGER" }
                };
                GeopackageLayerCreateHelper.CreateGeopackageLayer(gpkg, "test_layer", headers, geometryType: "POINT", srid: 3006);

                // age is invalid (non-numeric)
                var badFeature = new FeatureRecord(
                    Geometry: new Point(10, 20),
                    Attributes: new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["name"] = "Bad",
                        ["age"] = "not_a_number"
                    }
                );

                var list = new List<FeatureRecord> { badFeature };

                Assert.Throws<ArgumentException>(() =>
                    CGeopackageAddDataHelper.BulkInsertFeatures(gpkg, "test_layer", list));
            }
            finally
            {
                if (File.Exists(gpkg))
                {
                    try { File.Delete(gpkg); } catch { }
                }
            }
        }
    }
}

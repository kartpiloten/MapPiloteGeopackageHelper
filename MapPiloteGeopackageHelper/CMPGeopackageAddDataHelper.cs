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
using Microsoft.Data.Sqlite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SQLitePCL;
using System.Globalization;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestMapPiloteGeoPackageHandler")]

namespace MapPiloteGeopackageHelper;

public class CGeopackageAddDataHelper
{
    static CGeopackageAddDataHelper()
    {
        Batteries.Init();
    }

    /// <summary>
    /// Adds a point to a GeoPackage layer with attribute data and enhanced validation.
    /// Automatically updates the layer extent in gpkg_contents after insertion.
    /// </summary>
    public static void AddPointToGeoPackage(string geoPackagePath, string layerName, Point point, string[] attributeData, Action<string>? onWarning = null)
    {
        // Ensure the GeoPackage file exists
        if (!File.Exists(geoPackagePath))
        {
            throw new FileNotFoundException($"GeoPackage file not found: {geoPackagePath}");
        }

        // Validate layer name to prevent SQL injection
        CMPGeopackageUtils.ValidateIdentifier(layerName, "layer name");

        const int defaultSrid = 3006; // SWEREF99 TM
        const string geometryColumn = "geom";

        using SqliteConnection connection = new($"Data Source={geoPackagePath}");
        connection.Open();

        List<ColumnInfo> columnInfo = GetColumnInfoWithTypes(connection, layerName);

        if (attributeData.Length != columnInfo.Count)
        {
            string expectedColumns = string.Join(", ", columnInfo.Select(c => $"{c.Name}({c.Type})"));
            throw new ArgumentException(
                $"Column count mismatch for table '{layerName}'. " +
                $"Expected {columnInfo.Count} attribute values for columns: {expectedColumns}, " +
                $"but received {attributeData.Length} values.");
        }

        for (int i = 0; i < attributeData.Length; i++)
        {
            ValidateDataTypeCompatibility(columnInfo[i], attributeData[i], i, onWarning);
        }

        List<string> columnNames = columnInfo.Select(c => c.Name).ToList();
        string columnList = string.Join(", ", columnNames.Concat([geometryColumn]));
        string parameterList = string.Join(", ", columnNames.Select(c => $"@{c}").Concat(["@geom"]));
        
        string insertQuery = $"INSERT INTO {layerName} ({columnList}) VALUES ({parameterList})";

        using SqliteCommand command = new(insertQuery, connection);
        
        for (int i = 0; i < columnNames.Count; i++)
        {
            object convertedValue = ConvertValueToSqliteType(columnInfo[i], attributeData[i]);
            command.Parameters.AddWithValue($"@{columnNames[i]}", convertedValue);
        }

        WKBWriter wkbWriter = new();
        byte[] wkb = wkbWriter.Write(point);
        byte[] gpkgBlob = CMPGeopackageUtils.CreateGpkgBlob(wkb, defaultSrid);
        command.Parameters.AddWithValue("@geom", gpkgBlob);

        command.ExecuteNonQuery();
        
        // Update layer extent after successful insert so QGIS "Zoom to Layer" works correctly
        CMPGeopackageUtils.UpdateLayerExtent(connection, layerName, geometryColumn);
    }

    /// <summary>
    /// Bulk insert features (attributes + optional geometry) with transactional batching.
    /// Automatically updates the layer extent in gpkg_contents after insertion.
    /// </summary>
    public static void BulkInsertFeatures(
        string geoPackagePath,
        string layerName,
        IEnumerable<FeatureRecord> features,
        int srid = 3006,
        int batchSize = 1000,
        string geometryColumn = "geom",
        Action<string>? onWarning = null)
    {
        if (!File.Exists(geoPackagePath))
            throw new FileNotFoundException($"GeoPackage file not found: {geoPackagePath}");

        // Validate inputs
        CMPGeopackageUtils.ValidateIdentifier(layerName, "layer name");
        CMPGeopackageUtils.ValidateIdentifier(geometryColumn, "geometry column name");
        CMPGeopackageUtils.ValidateSrid(srid);
        CMPGeopackageUtils.ValidateBatchSize(batchSize);

        using SqliteConnection connection = new($"Data Source={geoPackagePath}");
        connection.Open();

        List<ColumnInfo> columnInfo = GetColumnInfoWithTypes(connection, layerName);
        List<string> columnNames = columnInfo.Select(c => c.Name).ToList();

        string columnList = string.Join(", ", columnNames.Concat([geometryColumn]));
        string parameterList = string.Join(", ", columnNames.Select(c => $"@{c}").Concat(["@geom"]));
        string insertSql = $"INSERT INTO {layerName} ({columnList}) VALUES ({parameterList})";

        using SqliteCommand command = new(insertSql, connection);

        foreach (string name in columnNames)
            command.Parameters.AddWithValue($"@{name}", DBNull.Value);
        command.Parameters.AddWithValue("@geom", DBNull.Value);

        SqliteTransaction? txn = null;
        WKBWriter wkbWriter = new();
        int i = 0;

        try
        {
            txn = connection.BeginTransaction();
            command.Transaction = txn;

            foreach (FeatureRecord feature in features)
            {
                for (int idx = 0; idx < columnNames.Count; idx++)
                {
                    ColumnInfo col = columnInfo[idx];
                    feature.Attributes.TryGetValue(col.Name, out string? raw);
                    string valForValidation = raw ?? string.Empty;
                    ValidateDataTypeCompatibility(col, valForValidation, idx, onWarning);

                    object converted = ConvertValueToSqliteType(col, valForValidation);
                    command.Parameters[$"@{col.Name}"].Value = converted ?? DBNull.Value;
                }

                if (feature.Geometry is null)
                {
                    command.Parameters["@geom"].Value = DBNull.Value;
                }
                else
                {
                    byte[] wkb = wkbWriter.Write(feature.Geometry);
                    byte[] gpkgBlob = CMPGeopackageUtils.CreateGpkgBlob(wkb, srid);
                    command.Parameters["@geom"].Value = gpkgBlob;
                }

                command.ExecuteNonQuery();

                i++;
                if (i % batchSize == 0)
                {
                    txn.Commit();
                    txn.Dispose();
                    txn = connection.BeginTransaction();
                    command.Transaction = txn;
                }
            }

            txn?.Commit();
            
            // Update layer extent after successful insert so QGIS "Zoom to Layer" works correctly
            CMPGeopackageUtils.UpdateLayerExtent(connection, layerName, geometryColumn);
        }
        catch
        {
            txn?.Rollback();
            throw;
        }
        finally
        {
            txn?.Dispose();
        }
    }

    /// <summary>
    /// Enhanced method to get column information including data types
    /// </summary>
    private static List<ColumnInfo> GetColumnInfoWithTypes(SqliteConnection connection, string tableName)
    {
        List<ColumnInfo> columnInfo = [];
        
        // tableName is already validated before calling this method
        string query = $"PRAGMA table_info({tableName})";
        using SqliteCommand command = new(query, connection);
        using SqliteDataReader reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            string columnName = reader.GetString(1);
            string columnType = reader.GetString(2);
            
            if (!columnName.Equals("id", StringComparison.OrdinalIgnoreCase) && 
                !columnName.Equals("geom", StringComparison.OrdinalIgnoreCase))
            {
                columnInfo.Add(new ColumnInfo(columnName, columnType));
            }
        }

        return columnInfo;
    }

    /// <summary>
    /// Validates that the provided data value is compatible with the column type
    /// </summary>
    internal static void ValidateDataTypeCompatibility(ColumnInfo columnInfo, string value, int index, Action<string>? onWarning = null)
    {
        if (string.IsNullOrEmpty(value))
            return;

        string columnType = columnInfo.Type.ToUpperInvariant();
        
        try
        {
            switch (columnType)
            {
                case "INTEGER":
                case "INT":
                    if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        throw new ArgumentException(
                            $"Data type mismatch at index {index}: Column '{columnInfo.Name}' expects INTEGER, " +
                            $"but received '{value}' which cannot be converted to an integer.");
                    }
                    break;

                case "REAL":
                case "FLOAT":
                case "DOUBLE":
                    if (!double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
                    {
                        throw new ArgumentException(
                            $"Data type mismatch at index {index}: Column '{columnInfo.Name}' expects REAL/FLOAT, " +
                            $"but received '{value}' which cannot be converted to a number.");
                    }
                    break;

                case "TEXT":
                case "VARCHAR":
                case "CHAR":
                    break;

                case "BLOB":
                    throw new ArgumentException(
                        $"Column '{columnInfo.Name}' is of type BLOB and cannot be inserted via string array. " +
                        "BLOB columns require special handling.");

                default:
                    onWarning?.Invoke($"Warning: Unknown column type '{columnType}' for column '{columnInfo.Name}'. Proceeding with string value.");
                    break;
            }
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Error validating data for column '{columnInfo.Name}' at index {index}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Converts string value to appropriate SQLite type
    /// </summary>
    internal static object ConvertValueToSqliteType(ColumnInfo columnInfo, string value)
    {
        if (string.IsNullOrEmpty(value))
            return DBNull.Value;

        string columnType = columnInfo.Type.ToUpperInvariant();
        
        return columnType switch
        {
            "INTEGER" or "INT" => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
            "REAL" or "FLOAT" or "DOUBLE" => double.Parse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture),
            "TEXT" or "VARCHAR" or "CHAR" => value,
            _ => value
        };
    }

    /// <summary>
    /// Helper class to store column information
    /// </summary>
    internal class ColumnInfo(string name, string type)
    {
        public string Name { get; } = name;
        public string Type { get; } = type;
    }
}

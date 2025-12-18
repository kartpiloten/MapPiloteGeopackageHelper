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
using System.Runtime.CompilerServices;
using System.Globalization;

namespace MapPiloteGeopackageHelper;

/// <summary>
/// Modern fluent API for GeoPackage operations.
/// Implements IDisposable and IAsyncDisposable for proper resource management.
/// </summary>
public sealed class GeoPackage : IDisposable, IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _path;
    private bool _disposed;

    private GeoPackage(string path, SqliteConnection connection)
    {
        _path = path;
        _connection = connection;
    }

    /// <summary>
    /// Opens or creates a GeoPackage file.
    /// </summary>
    /// <param name="path">Path to the GeoPackage file.</param>
    /// <param name="defaultSrid">Default SRID for new GeoPackages. Default is 3006 (SWEREF99 TM).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="onStatus">Optional callback for status messages.</param>
    /// <returns>A GeoPackage instance.</returns>
    public static async Task<GeoPackage> OpenAsync(string path, int defaultSrid = 3006, CancellationToken ct = default, Action<string>? onStatus = null)
    {
        CMPGeopackageUtils.ValidateSrid(defaultSrid);
        
        bool exists = File.Exists(path);
        
        SqliteConnection connection = new($"Data Source={path}");
        await connection.OpenAsync(ct);

        GeoPackage geoPackage = new(path, connection);

        if (!exists)
        {
            await geoPackage.InitializeAsync(defaultSrid, ct, onStatus);
        }

        return geoPackage;
    }

    /// <summary>
    /// Ensures a layer exists with the given schema, creating it if needed.
    /// </summary>
    /// <param name="layerName">Name of the layer (must be a valid SQL identifier).</param>
    /// <param name="attributeColumns">Dictionary of column names to SQL types.</param>
    /// <param name="srid">Spatial Reference System Identifier. Default is 3006.</param>
    /// <param name="geometryType">Geometry type (POINT, LINESTRING, POLYGON, etc.). Default is "POINT".</param>
    /// <param name="geometryColumn">Name of the geometry column. Default is "geom".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A GeoPackageLayer for the specified layer.</returns>
    public async Task<GeoPackageLayer> EnsureLayerAsync(
        string layerName,
        Dictionary<string, string> attributeColumns,
        int srid = 3006,
        string geometryType = "POINT",
        string geometryColumn = "geom",
        CancellationToken ct = default)
    {
        // Validate inputs
        CMPGeopackageUtils.ValidateIdentifier(layerName, "layer name");
        CMPGeopackageUtils.ValidateIdentifier(geometryColumn, "geometry column name");
        CMPGeopackageUtils.ValidateSrid(srid);
        
        foreach (string colName in attributeColumns.Keys)
        {
            CMPGeopackageUtils.ValidateIdentifier(colName, "column name");
        }

        bool exists = await LayerExistsAsync(layerName, ct);
        
        if (!exists)
        {
            await CreateLayerAsync(layerName, attributeColumns, srid, geometryType, geometryColumn, ct);
        }

        return new GeoPackageLayer(this, layerName, geometryColumn);
    }

    /// <summary>
    /// Get comprehensive metadata about this GeoPackage.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>GeopackageInfo containing layer and SRS information.</returns>
    public async Task<CMPGeopackageReadDataHelper.GeopackageInfo> GetInfoAsync(CancellationToken ct = default)
    {
        return await Task.Run(() => CMPGeopackageReadDataHelper.GetGeopackageInfo(_path), ct);
    }

    internal SqliteConnection Connection => _connection;
    internal string Path => _path;

    private async Task InitializeAsync(int srid, CancellationToken ct, Action<string>? onStatus = null)
    {
        await Task.Run(() =>
        {
            CMPGeopackageUtils.CreateGeoPackageMetadataTables(_connection);
            CMPGeopackageUtils.SetupSpatialReferenceSystem(_connection, srid);
            onStatus?.Invoke($"Successfully initialized GeoPackage: {_path}");
        }, ct);
    }

    private async Task<bool> LayerExistsAsync(string layerName, CancellationToken ct)
    {
        const string sql = "SELECT COUNT(*) FROM gpkg_contents WHERE table_name = @name";
        using SqliteCommand cmd = new(sql, _connection);
        cmd.Parameters.AddWithValue("@name", layerName);
        
        long count = (long)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        return count > 0;
    }

    private async Task CreateLayerAsync(
        string layerName,
        Dictionary<string, string> attributeColumns,
        int srid,
        string geometryType,
        string geometryColumn,
        CancellationToken ct)
    {
        await Task.Run(() =>
        {
            GeopackageLayerCreateHelper.CreateGeopackageLayer(
                _path, layerName, attributeColumns, geometryType, srid,
                onStatus: _ => { },
                onError: _ => { });
        }, ct);
    }

    /// <summary>
    /// Disposes the GeoPackage and releases the database connection.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _disposed = true;
        }
    }

    /// <summary>
    /// Asynchronously disposes the GeoPackage and releases the database connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }
            _disposed = true;
        }
    }
}

/// <summary>
/// Represents a layer in a GeoPackage for fluent operations.
/// </summary>
public sealed class GeoPackageLayer
{
    private readonly GeoPackage _geoPackage;
    private readonly string _layerName;
    private readonly string _geometryColumn;

    internal GeoPackageLayer(GeoPackage geoPackage, string layerName, string geometryColumn)
    {
        _geoPackage = geoPackage;
        _layerName = layerName;
        _geometryColumn = geometryColumn;
    }

    /// <summary>
    /// Bulk insert features with progress reporting.
    /// Uses streaming to avoid loading all features into memory.
    /// </summary>
    /// <param name="features">Features to insert.</param>
    /// <param name="options">Bulk insert options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task BulkInsertAsync(
        IEnumerable<FeatureRecord> features,
        BulkInsertOptions? options = null,
        IProgress<BulkProgress>? progress = null,
        CancellationToken ct = default)
    {
        options ??= new BulkInsertOptions();
        options.Validate();

        // Don't call ToList() - process in streaming fashion
        // For progress, we need to count if progress is requested
        int total = 0;
        IEnumerable<FeatureRecord> featureSource = features;
        
        if (progress is not null)
        {
            // Only materialize if progress is requested
            List<FeatureRecord> featureList = features.ToList();
            total = featureList.Count;
            featureSource = featureList;
        }

        int processed = 0;

        List<CGeopackageAddDataHelper.ColumnInfo> columnInfo = await GetColumnInfoAsync(ct);
        
        string insertSql = GetInsertSql(options.ConflictPolicy, columnInfo);
        using SqliteCommand command = new(insertSql, _geoPackage.Connection);
        
        foreach (CGeopackageAddDataHelper.ColumnInfo col in columnInfo)
            command.Parameters.AddWithValue($"@{col.Name}", DBNull.Value);
        command.Parameters.AddWithValue("@geom", DBNull.Value);

        WKBWriter wkbWriter = new();
        SqliteTransaction? transaction = null;

        try
        {
            transaction = _geoPackage.Connection.BeginTransaction();
            command.Transaction = transaction;

            foreach (FeatureRecord feature in featureSource)
            {
                ct.ThrowIfCancellationRequested();

                BindFeature(command, feature, columnInfo, wkbWriter, options.Srid);
                await command.ExecuteNonQueryAsync(ct);

                processed++;
                
                if (processed % options.BatchSize == 0)
                {
                    transaction.Commit();
                    transaction.Dispose();
                    transaction = _geoPackage.Connection.BeginTransaction();
                    command.Transaction = transaction;
                    
                    if (progress is not null && total > 0)
                    {
                        progress.Report(new BulkProgress(processed, total));
                    }
                }
            }

            transaction.Commit();
            
            // Create spatial index if requested
            if (options.CreateSpatialIndex)
            {
                await CreateSpatialIndexAsync(ct);
            }
            
            if (progress is not null)
            {
                progress.Report(new BulkProgress(processed, total > 0 ? total : processed));
            }
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    /// <summary>
    /// Read features as async enumerable with options.
    /// </summary>
    /// <param name="options">Read options for filtering, sorting, and paging.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Async enumerable of features.</returns>
    public async IAsyncEnumerable<FeatureRecord> ReadFeaturesAsync(
        ReadOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        options ??= new ReadOptions();
        options.Validate();

        string sql = BuildSelectSql(options);
        using SqliteCommand command = new(sql, _geoPackage.Connection);
        using SqliteDataReader reader = await command.ExecuteReaderAsync(ct);
        
        List<string> attributeColumns = await GetAttributeColumnNamesAsync(ct);
        WKBReader? wkbReader = options.IncludeGeometry ? new WKBReader() : null;
        int geomOrdinal = -1;
        
        if (options.IncludeGeometry)
        {
            try { geomOrdinal = reader.GetOrdinal(_geometryColumn); } 
            catch (IndexOutOfRangeException) { geomOrdinal = -1; }
        }
        
        while (await reader.ReadAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            
            Dictionary<string, string?> attrs = new(StringComparer.Ordinal);
            foreach (string colName in attributeColumns)
            {
                try
                {
                    int ord = reader.GetOrdinal(colName);
                    attrs[colName] = reader.IsDBNull(ord) ? null : 
                        Convert.ToString(reader.GetValue(ord), CultureInfo.InvariantCulture);
                }
                catch (IndexOutOfRangeException) 
                { 
                    // Column doesn't exist in result set, skip it
                }
            }

            Geometry? geometry = null;
            if (options.IncludeGeometry && geomOrdinal >= 0 && !reader.IsDBNull(geomOrdinal))
            {
                try
                {
                    byte[] gpkgBlob = (byte[])reader.GetValue(geomOrdinal);
                    byte[] wkb = CMPGeopackageReadDataHelper.StripGpkgHeader(gpkgBlob);
                    geometry = wkbReader!.Read(wkb);
                }
                catch (ArgumentException)
                {
                    // Invalid geometry data, leave as null
                }
            }

            yield return new FeatureRecord(geometry, attrs);
        }
    }

    /// <summary>
    /// Delete features matching a condition.
    /// </summary>
    /// <param name="whereClause">Optional WHERE clause (without the WHERE keyword). If null, all features are deleted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of deleted features.</returns>
    public async Task<int> DeleteAsync(string? whereClause = null, CancellationToken ct = default)
    {
        // Layer name is already validated when layer is created
        string sql = string.IsNullOrEmpty(whereClause) 
            ? $"DELETE FROM {_layerName}"
            : $"DELETE FROM {_layerName} WHERE {whereClause}";
            
        using SqliteCommand command = new(sql, _geoPackage.Connection);
        return await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Get feature count.
    /// </summary>
    /// <param name="whereClause">Optional WHERE clause (without the WHERE keyword).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of features matching the condition.</returns>
    public async Task<long> CountAsync(string? whereClause = null, CancellationToken ct = default)
    {
        string sql = string.IsNullOrEmpty(whereClause)
            ? $"SELECT COUNT(*) FROM {_layerName}"
            : $"SELECT COUNT(*) FROM {_layerName} WHERE {whereClause}";
            
        using SqliteCommand command = new(sql, _geoPackage.Connection);
        return (long)(await command.ExecuteScalarAsync(ct) ?? 0);
    }

    /// <summary>
    /// Create an index on the geometry column.
    /// Note: This creates a standard B-tree index, not a true spatial index (R-tree).
    /// For R-tree spatial indexing, use the GeoPackage extensions.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task CreateSpatialIndexAsync(CancellationToken ct = default)
    {
        string sql = $"CREATE INDEX IF NOT EXISTS idx_{_layerName}_{_geometryColumn} ON {_layerName}({_geometryColumn})";
        using SqliteCommand command = new(sql, _geoPackage.Connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    private string GetInsertSql(ConflictPolicy policy, List<CGeopackageAddDataHelper.ColumnInfo> columnInfo)
    {
        string verb = policy switch
        {
            ConflictPolicy.Ignore => "INSERT OR IGNORE",
            ConflictPolicy.Replace => "INSERT OR REPLACE", 
            _ => "INSERT"
        };

        List<string> columnNames = columnInfo.Select(c => c.Name).ToList();
        string columnList = string.Join(", ", columnNames.Concat([_geometryColumn]));
        string parameterList = string.Join(", ", columnNames.Select(c => $"@{c}").Concat(["@geom"]));
        
        return $"{verb} INTO {_layerName} ({columnList}) VALUES ({parameterList})";
    }

    private string BuildSelectSql(ReadOptions options)
    {
        // Layer name is validated when layer is created
        string sql = $"SELECT * FROM {_layerName}";
        
        if (!string.IsNullOrEmpty(options.WhereClause))
            sql += $" WHERE {options.WhereClause}";
        
        if (!string.IsNullOrEmpty(options.OrderBy))
            sql += $" ORDER BY {options.OrderBy}";
            
        if (options.Limit.HasValue)
            sql += $" LIMIT {options.Limit}";
            
        if (options.Offset.HasValue)
            sql += $" OFFSET {options.Offset}";
            
        return sql;
    }

    private async Task<List<CGeopackageAddDataHelper.ColumnInfo>> GetColumnInfoAsync(CancellationToken ct)
    {
        return await Task.Run(GetColumnInfoSync, ct);
    }

    private List<CGeopackageAddDataHelper.ColumnInfo> GetColumnInfoSync()
    {
        List<CGeopackageAddDataHelper.ColumnInfo> columnInfo = [];
        
        // Layer name is validated when layer is created
        string query = $"PRAGMA table_info({_layerName})";
        using SqliteCommand command = new(query, _geoPackage.Connection);
        using SqliteDataReader reader = command.ExecuteReader();
        
        while (reader.Read())
        {
            string columnName = reader.GetString(1);
            string columnType = reader.GetString(2);
            
            if (!columnName.Equals("id", StringComparison.OrdinalIgnoreCase) && 
                !columnName.Equals(_geometryColumn, StringComparison.OrdinalIgnoreCase))
            {
                columnInfo.Add(new CGeopackageAddDataHelper.ColumnInfo(columnName, columnType));
            }
        }
        
        return columnInfo;
    }

    private async Task<List<string>> GetAttributeColumnNamesAsync(CancellationToken ct)
    {
        List<CGeopackageAddDataHelper.ColumnInfo> columnInfo = await GetColumnInfoAsync(ct);
        return columnInfo.Select(c => c.Name).ToList();
    }

    private void BindFeature(
        SqliteCommand command, 
        FeatureRecord feature, 
        List<CGeopackageAddDataHelper.ColumnInfo> columnInfo, 
        WKBWriter wkbWriter, 
        int srid)
    {
        for (int idx = 0; idx < columnInfo.Count; idx++)
        {
            CGeopackageAddDataHelper.ColumnInfo col = columnInfo[idx];
            feature.Attributes.TryGetValue(col.Name, out string? raw);
            string valForValidation = raw ?? string.Empty;
            
            CGeopackageAddDataHelper.ValidateDataTypeCompatibility(col, valForValidation, idx);

            object converted = CGeopackageAddDataHelper.ConvertValueToSqliteType(col, valForValidation);
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
    }
}
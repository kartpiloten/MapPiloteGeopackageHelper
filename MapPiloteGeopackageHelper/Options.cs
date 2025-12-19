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
namespace MapPiloteGeopackageHelper;

/// <summary>
/// Options for bulk insert operations.
/// </summary>
/// <param name="BatchSize">Number of features to insert per transaction batch. Must be between 1 and 100,000. Default is 1000.</param>
/// <param name="Srid">Spatial Reference System Identifier for the geometry data. Default is 3006 (SWEREF99 TM).</param>
/// <param name="CreateSpatialIndex">Whether to create a spatial index after bulk insert. Default is false.</param>
/// <param name="ConflictPolicy">How to handle conflicts during insert. Default is Abort.</param>
/// <param name="ValidateGeometryType">Whether to validate geometry type against the specified GeometryType option. Default is false.</param>
public sealed record BulkInsertOptions(
    int BatchSize = 1000,
    int Srid = 3006,
    bool CreateSpatialIndex = false,
    ConflictPolicy ConflictPolicy = ConflictPolicy.Abort,
    bool ValidateGeometryType = false
)
{
    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when BatchSize or Srid is invalid.</exception>
    internal void Validate()
    {
        CMPGeopackageUtils.ValidateBatchSize(BatchSize);
        CMPGeopackageUtils.ValidateSrid(Srid);
    }
}

/// <summary>
/// Progress information for bulk operations.
/// </summary>
/// <param name="Processed">Number of features processed so far.</param>
/// <param name="Total">Total number of features to process.</param>
public sealed record BulkProgress(
    int Processed,
    int Total
)
{
    /// <summary>
    /// Gets the percentage of completion (0-100).
    /// </summary>
    public double PercentComplete => Total > 0 ? (double)Processed / Total * 100.0 : 0.0;

    /// <summary>
    /// Gets whether the operation is complete.
    /// </summary>
    public bool IsComplete => Processed >= Total;

    /// <summary>
    /// Gets the number of features remaining to process.
    /// </summary>
    public int Remaining => Math.Max(0, Total - Processed);
}

/// <summary>
/// How to handle conflicts during insert operations.
/// </summary>
public enum ConflictPolicy
{
    /// <summary>Abort the entire operation on conflict (default). The transaction is rolled back.</summary>
    Abort,
    /// <summary>Ignore conflicting rows and continue with the next feature.</summary>
    Ignore,
    /// <summary>Replace existing rows with the new data.</summary>
    Replace
}

/// <summary>
/// Options for reading features from a layer.
/// </summary>
/// <param name="IncludeGeometry">Whether to include geometry data in results. Default is true.</param>
/// <param name="WhereClause">Optional SQL WHERE clause to filter results (without the WHERE keyword).</param>
/// <param name="Limit">Maximum number of features to return. Null means no limit.</param>
/// <param name="Offset">Number of features to skip. Null means start from the beginning.</param>
/// <param name="OrderBy">Optional ORDER BY clause (without the ORDER BY keywords). Example: "population DESC".</param>
public sealed record ReadOptions(
    bool IncludeGeometry = true,
    string? WhereClause = null,
    int? Limit = null,
    int? Offset = null,
    string? OrderBy = null
)
{
    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when Limit or Offset is negative.</exception>
    internal void Validate()
    {
        if (Limit.HasValue && Limit.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(Limit), Limit, "Limit cannot be negative.");
        if (Offset.HasValue && Offset.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(Offset), Offset, "Offset cannot be negative.");
    }
}

/// <summary>
/// Options for layer creation.
/// </summary>
/// <param name="Srid">Spatial Reference System Identifier. Default is 3006 (SWEREF99 TM).</param>
/// <param name="GeometryType">Type of geometry (POINT, LINESTRING, POLYGON, GEOMETRY, etc.). Default is "POINT".</param>
/// <param name="GeometryColumn">Name of the geometry column. Default is "geom".</param>
/// <param name="CreateSpatialIndex">Whether to create a spatial index on the geometry column. Default is false.</param>
/// <param name="Constraints">Optional dictionary of column constraints (column name to constraint string).</param>
public sealed record LayerCreateOptions(
    int Srid = 3006,
    string GeometryType = "POINT",
    string GeometryColumn = "geom",
    bool CreateSpatialIndex = false,
    Dictionary<string, string>? Constraints = null
)
{
    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when GeometryColumn is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when Srid is invalid.</exception>
    internal void Validate()
    {
        CMPGeopackageUtils.ValidateSrid(Srid);
        CMPGeopackageUtils.ValidateIdentifier(GeometryColumn, "geometry column name");
    }
}

/// <summary>
/// Result of a schema validation operation.
/// </summary>
/// <param name="IsValid">Whether the schema is valid.</param>
/// <param name="Errors">List of validation errors (empty if valid).</param>
public sealed record ValidationResult(
    bool IsValid,
    List<string> Errors
)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new(true, []);

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public static ValidationResult Failure(params string[] errors) => new(false, [.. errors]);
}
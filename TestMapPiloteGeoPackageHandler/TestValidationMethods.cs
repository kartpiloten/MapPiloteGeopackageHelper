using MapPiloteGeopackageHelper;

namespace TestMapPiloteGeoPackageHandler;

[TestClass]
public class TestValidationMethods
{
    [TestMethod]
    public void TestValidateDataTypeCompatibility_ValidInteger_ShouldNotThrow()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo columnInfo = new("test_col", "INTEGER");

        // Act & Assert - Should not throw
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "123", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "-456", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "0", 0);
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_InvalidInteger_ShouldThrow()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo columnInfo = new("test_col", "INTEGER");

        // Act & Assert
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "not_a_number", 5));

        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 5"));
        Assert.IsTrue(exception.Message.Contains("Column 'test_col' expects INTEGER"));
        Assert.IsTrue(exception.Message.Contains("cannot be converted to an integer"));
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_ValidReal_ShouldNotThrow()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo columnInfo = new("real_col", "REAL");

        // Act & Assert - Should not throw
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "123.45", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "-67.89", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "1.23E+10", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "0.0", 0);
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_InvalidReal_ShouldThrow()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo columnInfo = new("real_col", "REAL");

        // Act & Assert
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "invalid_real", 3));

        Assert.IsTrue(exception.Message.Contains("Data type mismatch at index 3"));
        Assert.IsTrue(exception.Message.Contains("Column 'real_col' expects REAL/FLOAT"));
        Assert.IsTrue(exception.Message.Contains("cannot be converted to a number"));
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_ValidFloat_ShouldNotThrow()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo columnInfo = new("float_col", "FLOAT");

        // Act & Assert - Should not throw
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "3.14159", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "2.718E-5", 0);
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_ValidDouble_ShouldNotThrow()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo columnInfo = new("double_col", "DOUBLE");

        // Act & Assert - Should not throw
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "3.141592653589793", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "1.7976931348623157E+308", 0);
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_ValidText_ShouldNotThrow()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo textColumn = new("text_col", "TEXT");
        CGeopackageAddDataHelper.ColumnInfo varcharColumn = new("varchar_col", "VARCHAR");
        CGeopackageAddDataHelper.ColumnInfo charColumn = new("char_col", "CHAR");

        // Act & Assert - Should not throw for any text value
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(textColumn, "any text", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(textColumn, "123", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(textColumn, "!@#$%^&*()", 0);
        
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(varcharColumn, "varchar text", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(charColumn, "c", 0);
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_BlobColumn_ShouldThrow()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo columnInfo = new("blob_col", "BLOB");

        // Act & Assert
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "any_value", 2));

        Assert.IsTrue(exception.Message.Contains("Column 'blob_col' is of type BLOB"));
        Assert.IsTrue(exception.Message.Contains("cannot be inserted via string array"));
        Assert.IsTrue(exception.Message.Contains("BLOB columns require special handling"));
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_EmptyValue_ShouldNotThrow()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo intColumn = new("int_col", "INTEGER");
        CGeopackageAddDataHelper.ColumnInfo realColumn = new("real_col", "REAL");
        CGeopackageAddDataHelper.ColumnInfo textColumn = new("text_col", "TEXT");

        // Act & Assert - Should not throw for empty/null values
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(intColumn, "", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(intColumn, null!, 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(realColumn, "", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(realColumn, null!, 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(textColumn, "", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(textColumn, null!, 0);
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_UnknownType_ShouldNotThrow()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo columnInfo = new("custom_col", "CUSTOM_TYPE");

        // Act & Assert - Should not throw but should log warning
        // (We can't easily test console output in unit tests, but the method should not throw)
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(columnInfo, "some_value", 0);
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_CaseInsensitive_ShouldWork()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo intColumnLower = new("int_col", "integer");
        CGeopackageAddDataHelper.ColumnInfo realColumnMixed = new("real_col", "Real");
        CGeopackageAddDataHelper.ColumnInfo textColumnUpper = new("text_col", "TEXT");

        // Act & Assert - Should work regardless of case
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(intColumnLower, "123", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(realColumnMixed, "45.67", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(textColumnUpper, "text", 0);
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_AllIntegerVariants_ShouldWork()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo integerColumn = new("col1", "INTEGER");
        CGeopackageAddDataHelper.ColumnInfo intColumn = new("col2", "INT");

        // Act & Assert - Both INTEGER and INT should work the same way
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(integerColumn, "123", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(intColumn, "456", 0);

        // Both should fail for non-numeric values
        Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.ValidateDataTypeCompatibility(integerColumn, "not_a_number", 0));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.ValidateDataTypeCompatibility(intColumn, "not_a_number", 0));
    }

    [TestMethod]
    public void TestValidateDataTypeCompatibility_AllRealVariants_ShouldWork()
    {
        // Arrange
        CGeopackageAddDataHelper.ColumnInfo realColumn = new("col1", "REAL");
        CGeopackageAddDataHelper.ColumnInfo floatColumn = new("col2", "FLOAT");
        CGeopackageAddDataHelper.ColumnInfo doubleColumn = new("col3", "DOUBLE");

        // Act & Assert - All real number types should work the same way
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(realColumn, "123.45", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(floatColumn, "67.89", 0);
        CGeopackageAddDataHelper.ValidateDataTypeCompatibility(doubleColumn, "1.23E+10", 0);

        // All should fail for non-numeric values
        Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.ValidateDataTypeCompatibility(realColumn, "not_a_number", 0));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.ValidateDataTypeCompatibility(floatColumn, "not_a_number", 0));
        Assert.ThrowsExactly<ArgumentException>(() =>
            CGeopackageAddDataHelper.ValidateDataTypeCompatibility(doubleColumn, "not_a_number", 0));
    }

    [TestMethod]
    public void TestColumnInfo_Properties_ShouldWork()
    {
        // Arrange & Act
        CGeopackageAddDataHelper.ColumnInfo columnInfo = new("test_column", "INTEGER");

        // Assert
        Assert.AreEqual("test_column", columnInfo.Name);
        Assert.AreEqual("INTEGER", columnInfo.Type);
    }
}
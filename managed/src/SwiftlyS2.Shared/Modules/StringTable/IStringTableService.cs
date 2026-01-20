namespace SwiftlyS2.Shared.StringTable;

public interface IStringTableService
{
    /// <summary>
    /// Finds a string table by name.
    /// </summary>
    /// <param name="tableName">The name of the string table to find.</param>
    /// <returns>The string table, or null if the string table is not found.</returns>
    public IStringTable? FindTable(string tableName);

    /// <summary>
    /// Finds a string table by ID.
    /// </summary>
    /// <param name="tableId">The ID of the string table to find.</param>
    /// <returns>The string table, or null if the string table is not found.</returns>
    /// <returns></returns>
    public IStringTable? FindTableById(int tableId);
}   
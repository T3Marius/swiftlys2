using SwiftlyS2.Shared.Natives;

namespace SwiftlyS2.Shared.StringTable;

public interface IStringTable
{

    /// <summary>
    /// Gets the name of the string table.
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// Gets the ID of the string table.
    /// </summary>
    int TableId { get; }

    /// <summary>
    /// Gets the number of strings in the string table.
    /// </summary>
    int NumStrings { get; }

    /// <summary>
    /// Finds the index of the specified string in the string table.
    /// </summary>
    /// <param name="str">The string to find the index of.</param>
    /// <returns>The index of the string, or null if the string is not found.</returns>
    int? FindStringIndex(string str);

    /// <summary>
    /// Checks if the specified index is valid.
    /// </summary>
    /// <param name="index">The index to check.</param>
    /// <returns>True if the index is valid, false otherwise.</returns>
    bool IsStringIndexValid(int index);

    /// <summary>
    /// Gets the string at the specified index.
    /// </summary>
    /// <param name="index">The index to get the string from.</param>
    /// <returns>The string at the specified index, or null if the index is invalid.</returns>
    string? GetString(int index);

    /// <summary>
    /// Tries to get the user data for the specified index.
    /// </summary>
    /// <param name="index">The index to get the user data from.</param>
    /// <param name="result">The user data, or invalid if the index is invalid.</param>
    /// <returns>True if the user data was successfully retrieved, false otherwise.</returns>
    bool TryGetStringUserData(int index, out StringTableOutUserData result);

    /// <summary>
    /// Gets the user data for the specified index.
    /// </summary>
    /// <param name="index">The index to get the user data from.</param>
    /// <returns>The user data, or invalid if the index is invalid.</returns>
    StringTableOutUserData GetStringUserData(int index);

    /// <summary>
    /// Tries to get the user data for the specified string.
    /// </summary>
    /// <param name="str">The string to get the user data for.</param>
    /// <param name="result">The user data, or invalid if the string is not found.</param>
    /// <returns>True if the user data was successfully retrieved, false otherwise.</returns>
    bool TryGetStringUserData(string str, out StringTableOutUserData result);

    /// <summary>
    /// Gets the user data for the specified string.
    /// </summary>
    /// <param name="str">The string to get the user data for.</param>
    /// <returns>The user data, or invalid if the string is not found.</returns>
    StringTableOutUserData GetStringUserData(string str);

    /// <summary>
    /// Sets the user data for the specified index.
    /// </summary>
    /// <param name="index">The index to set the user data for.</param>
    /// <param name="userData">The user data to set.</param>
    /// <param name="forceOverride">Whether to override the user data if it already exists.</param>
    /// <exception cref="ArgumentException">Thrown if the index is invalid.</exception>
    /// <returns>True if the user data was successfully set, false otherwise.</returns>
    bool SetStringUserData(int index, StringTableUserData userData, bool forceOverride = true);

    /// <summary>
    /// Sets the user data for the specified string.    
    /// </summary>
    /// <param name="str">The string to set the user data for.</param>
    /// <param name="userData">The user data to set.</param>
    /// <param name="forceOverride">Whether to override the user data if it already exists.</param>
    /// <exception cref="ArgumentException">Thrown if the string is not found in the string table.</exception>
    /// <returns>True if the user data was successfully set, false otherwise.</returns>
    bool SetStringUserData(string str, StringTableUserData userData, bool forceOverride = true);


    /// <summary>
    /// Sets the user data for the specified string, or adds the string and set if it does not exist.
    /// </summary>
    /// <param name="str">The string to set the user data for.</param>
    /// <param name="userData">The user data to set.</param>
    /// <param name="forceOverride">Whether to override the user data if it already exists.</param>
    /// <returns>True if the user data was successfully set or added, false otherwise.</returns>
    bool SetOrAddStringUserData(string str, StringTableUserData userData, bool forceOverride = true);

    /// <summary>
    /// Gets the index of the specified string in the string table, or adds it if it does not exist.
    /// </summary>
    /// <param name="str">The string to get or add.</param>
    /// <returns>The index of the string, or new index if the string is not found and added.</returns>
    int GetOrAddString(string str);

    /// <summary>
    /// Replicates the user data for the specified string to the specified recipients.
    /// </summary>
    /// <param name="filter">The recipients to replicate the user data to.</param>
    /// <param name="stringIndex">The index of the string to replicate the user data for.</param>
    /// <param name="userData">The user data to replicate.</param>
    void ReplicateUserData(int stringIndex, StringTableUserData userData, in CRecipientFilter filter);

    /// <summary>
    /// Replicates the user data for the specified string to the specified recipients.
    /// Notice that the string MUST be already added to the string table BEFORE A FEW TICKS.
    /// If the string is not added, the replication will fail.
    /// If the string is added only 1-2 ticks ago before this call, the server will overwrites the replicated value.
    /// 
    /// </summary>
    /// <param name="str">The string to replicate the user data for.</param>
    /// <param name="userData">The user data to replicate.</param>
    /// <param name="filter">The recipients to replicate the user data to.</param>
    /// <exception cref="ArgumentException">Thrown if the string is not found in the string table.</exception>
    void ReplicateUserData(string str, StringTableUserData userData, in CRecipientFilter filter);   
}
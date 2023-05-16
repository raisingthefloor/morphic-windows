namespace Morphic.Core.Legacy;

/// <summary>
/// A Storable record with a unique identifier
/// </summary>
public interface IRecord
{
    /// <summary>
    /// The record's unique identifier
    /// </summary>
    public string Id { get; set; }
}

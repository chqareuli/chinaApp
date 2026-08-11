namespace Chiniseapp.Domain.Enums;

/// <summary>
/// Main = main_author, once per entry, when it first leaves NewEntry.
/// Additional = any qualifying contribution, once per editor and entry.
/// </summary>
public enum ScoreType
{
    Main,
    Additional,
}

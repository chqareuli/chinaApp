namespace Chiniseapp.Domain.Enums;

/// <summary>
/// Main = main_author, once per entry, when it first leaves NewEntry.
/// Additional = any other contributing editor, once per entry.
/// KaEditor = Georgian-editor score for ka_review work, once per entry.
/// </summary>
public enum ScoreType
{
    Main,
    Additional,
    KaEditor,
}

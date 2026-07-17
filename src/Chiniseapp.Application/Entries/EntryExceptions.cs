namespace Chiniseapp.Application.Entries;

/// <summary>Thrown when a save's RowVersion no longer matches the stored entry (concurrent edit).</summary>
public class EntryConcurrencyException : Exception;

/// <summary>Thrown for request-shape problems the controller should map to 400 Bad Request.</summary>
public class EntryValidationException(string message) : Exception(message);

/// <summary>Thrown when the actor's role isn't allowed to move an entry from its current status to the requested one.</summary>
public class EntryStatusTransitionForbiddenException(string fromStatus, string toStatus)
    : Exception($"Not allowed to move this entry from '{fromStatus}' to '{toStatus}'.");

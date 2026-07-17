namespace Chiniseapp.Application.Comments;

/// <summary>Thrown for request-shape problems the controller should map to 400 Bad Request.</summary>
public class CommentValidationException(string message) : Exception(message);

/// <summary>Thrown when the requesting editor is neither the comment's author nor a privileged role.</summary>
public class CommentAccessDeniedException : Exception;

namespace Chiniseapp.Application.Notifications;

/// <summary>Thrown for request-shape problems the controller should map to 400 Bad Request.</summary>
public class NotificationValidationException(string message) : Exception(message);

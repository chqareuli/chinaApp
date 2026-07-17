namespace Chiniseapp.Application.ReferenceMaterials;

/// <summary>Thrown for request-shape problems the controller should map to 400 Bad Request.</summary>
public class ReferenceMaterialValidationException(string message) : Exception(message);

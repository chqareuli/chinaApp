namespace Chiniseapp.Application.SupportMaterials;

public class SupportMaterialValidationException(string message) : Exception(message);

public class SupportMaterialAccessDeniedException : Exception;

using Chiniseapp.Application.Vocabularies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chiniseapp.Api.Controllers;

[ApiController]
[Route("vocabularies")]
[Authorize]
public class VocabulariesController(IVocabularyService vocabularyService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VocabularyItemDto>>> Get(
        [FromQuery] string? category = null,
        CancellationToken ct = default) =>
        Ok(await vocabularyService.GetActiveAsync(category, ct));
}

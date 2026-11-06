using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Chiniseapp.Application.Entries;
using Chiniseapp.Application.Legacy;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Domain.Rules;
using Chiniseapp.Infrastructure;
using Chiniseapp.Infrastructure.Entries;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// The connection string is inherited from the server environment, never a CLI argument.
// Explicit --apply is required. An entire batch is one transaction and never overwrites entries.
if (args.Length is < 5 or > 6 || args[0] is not ("--dry-run" or "--apply") ||
    (args.Length == 6 && args[5] != "--complex") || !int.TryParse(args[3], out var actorId))
{
    Console.Error.WriteLine("Usage: LegacyImport --dry-run|--apply input.json source-system actor-id report.json [--complex]");
    return 1;
}
var apply = args[0] == "--apply";
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{ WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
var rows = JsonSerializer.Deserialize<List<LegacyArticle>>(await File.ReadAllTextAsync(args[1]), json)
    ?? throw new InvalidDataException("Expected source array.");
if (rows.Count == 0 || rows.Any(r => r is null || string.IsNullOrWhiteSpace(r.SourceId) || string.IsNullOrWhiteSpace(r.Word)) ||
    rows.Select(r => r.SourceId).Distinct().Count() != rows.Count ||
    rows.Select(r => TitleNormalizer.Normalize(r.Word.Trim())).Distinct().Count() != rows.Count)
    throw new InvalidDataException("Source IDs and normalized headwords must be nonempty and unique.");
if (args[2].Length is < 1 or > 80 || !args[2].All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))
    throw new InvalidDataException("Use a stable source-system identifier.");
if (Path.GetFullPath(args[1]).Equals(Path.GetFullPath(args[4]), StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException("Source and report paths must differ.");
var services = new ServiceCollection().AddInfrastructure(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    .BuildServiceProvider();
await using var scope = services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<ChiniseDbContext>();
if (!await db.Editors.AnyAsync(e => e.Id == actorId && e.IsActive && e.Role == EditorRole.SuperAdmin))
    throw new InvalidDataException("An active migration operator with super_admin role is required.");
await using var tx = await db.Database.BeginTransactionAsync();
await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(712403120)");
var vocab = await db.ControlledVocabularies.Where(v => v.IsActive).ToListAsync();
var outcomes = new List<object>();
var imported = new List<(LegacyParseResult Parsed, Entry Entry, string Key, string Hash)>();
foreach (var row in rows)
{
    var key = $"legacy-import:{args[2]}:{row.SourceId}";
    var sourceJson = JsonSerializer.Serialize(row, json);
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sourceJson)));
    var previous = await db.AuditLogEntries.SingleOrDefaultAsync(a => a.EntityType == "LegacyImport" && a.Notes == key);
    if (previous is not null)
    {
        using var snapshot = JsonDocument.Parse(previous.NewValue!);
        if (snapshot.RootElement.GetProperty("sourceHash").GetString() != hash ||
            !await db.Entries.AnyAsync(e => e.Id == previous.EntityId))
            throw new InvalidDataException($"Source {row.SourceId} changed or destination is missing; review before reimport.");
        outcomes.Add(new { row.SourceId, row.Word, entryId = previous.EntityId, action = "already-imported" });
        continue;
    }
    var normalized = TitleNormalizer.Normalize(row.Word.Trim());
    if (await db.Entries.AnyAsync(e => e.SearchNormalizedTitle == normalized))
        throw new InvalidDataException($"Headword conflict for source {row.SourceId}; batch rolled back.");
    var parsed = LegacySemanticParser.Parse(row, args.Length == 6);
    if (parsed.ProposedContent is null)
        throw new InvalidDataException($"Source {row.SourceId} has blocking parser issues: {string.Join(',', parsed.Issues.Select(i => i.Code))}");
    var tree = parsed.ProposedContent;
    var groups = tree.GramGroups.Concat(tree.Homonyms.SelectMany(h => h.GramGroups ?? [])).ToList();
    var senses = tree.Senses.Concat(groups.SelectMany(g => g.Senses)).Concat(tree.Homonyms.SelectMany(h => h.DirectSenses));
    var markerItems = groups.SelectMany(g => g.Markers).Concat(tree.Homonyms.SelectMany(h => h.Markers))
        .Concat(senses.SelectMany(s => s.Markers.Concat(s.Examples.SelectMany(e => e.Markers))));
    foreach (var marker in markerItems)
        if (!vocab.Any(v => v.Category.ToString() == marker.Category && v.Code == marker.Value))
            throw new InvalidDataException($"Unknown vocabulary {marker.Category}:{marker.Value} in {row.SourceId}");
    foreach (var group in groups)
        if (!vocab.Any(v => v.Category == VocabularyCategory.Pos && v.Code == group.Pos))
            throw new InvalidDataException($"Unknown POS in {row.SourceId}");
    var now = DateTime.UtcNow;
    var entry = new Entry
    {
        Lemma = row.Word.Trim(), Pinyin = row.Transcription,
        SearchNormalizedTitle = normalized, Status = EntryStatus.NewEntry,
        StatusPriority = EntryStatusPriority.For(EntryStatus.NewEntry),
        CreatedByEditorId = actorId, LastEditorEditorId = actorId,
        CreatedAtUtc = now, UpdatedAtUtc = now, IsFlaggedForRepair = true,
        RepairNotes = $"Legacy staging import {row.SourceId}. Review source snapshot and parser issues in migration audit.",
        LegacyRawCommentBlob = row.Comment
    };
    EntryTreeMapper.SaveForms(entry, tree.Pinyins, tree.ComplexForms);
    db.Entries.Add(entry);
    await db.SaveChangesAsync();
    imported.Add((parsed, entry, key, hash));
}
var allTargets = await db.Entries.AsNoTracking().Select(e => new { e.Id, e.Lemma }).ToListAsync();
foreach (var (parsed, entry, key, hash) in imported)
{
    CrossReferenceDto Resolve(CrossReferenceDto reference)
    {
        var targets = reference.Targets.Select(t =>
        {
            var target = allTargets.SingleOrDefault(e => e.Lemma == t.Target);
            if (target is null) parsed.Issues.Add(new("UnresolvedCrossReference", "translation", 0, t.Target));
            return t with { TargetEntryId = target?.Id, TargetEntryLemma = target?.Lemma };
        }).ToList();
        return reference with { Targets = targets };
    }
    SenseDto ResolveSense(SenseDto s) => s with
    {
        CrossReferences = s.CrossReferences.Select(Resolve).ToList(),
        Examples = s.Examples.Select(e => e with { CrossReferences = e.CrossReferences.Select(Resolve).ToList() }).ToList()
    };
    GramGroupDto ResolveGroup(GramGroupDto g) => g with
    { Senses = g.Senses.Select(ResolveSense).ToList(), CrossReferences = g.CrossReferences.Select(Resolve).ToList() };
    var tree = parsed.ProposedContent! with
    {
        GramGroups = parsed.ProposedContent!.GramGroups.Select(ResolveGroup).ToList(),
        Senses = parsed.ProposedContent.Senses.Select(ResolveSense).ToList(),
        Homonyms = parsed.ProposedContent.Homonyms.Select(h => h with
        {
            GramGroups = h.GramGroups?.Select(ResolveGroup).ToList(),
            DirectSenses = h.DirectSenses.Select(ResolveSense).ToList(),
            CrossReferences = h.CrossReferences.Select(Resolve).ToList()
        }).ToList()
    };
    tree = EntryTreeMapper.LinkPronunciations(tree, EntryTreeMapper.ReadForms(entry));
    var mapper = new EntryTreeMapper(entry.Id, [], entry.UpdatedAtUtc);
    mapper.Apply(tree);
    db.Segments.AddRange(mapper.Nodes);
    var r = parsed.ReferenceMaterial;
    db.ReferenceMaterials.Add(new ReferenceMaterial
    {
        EntryId = entry.Id, G1 = r.G1, G2 = r.G2, G3 = r.G3, G4 = r.G4, G5 = r.G5,
        OriginalRawReferenceMaterial = parsed.Source.WorkingSet ?? "",
        CreatedAtUtc = entry.CreatedAtUtc, UpdatedAtUtc = entry.UpdatedAtUtc
    });
    if (!string.IsNullOrWhiteSpace(parsed.Source.Comment))
        db.Comments.Add(new Comment
        {
            EntryId = entry.Id, AuthorEditorId = actorId, CommentText = parsed.Source.Comment,
            OriginalRawComment = parsed.Source.Comment,
            ParsedCommentParts = JsonSerializer.Serialize(parsed.CommentParts, json),
            CreatedAtUtc = entry.CreatedAtUtc, UpdatedAtUtc = entry.UpdatedAtUtc
        });
    // article_examples is a secondary index in this corpus. Keep unmatched rows visible
    // for review rather than silently adding them to an arbitrary sense.
    if (parsed.Source.SourceMetadata is { } meta && meta.TryGetProperty("examples", out var examples))
    {
        var contentExamples = tree.Senses.Concat(tree.GramGroups.SelectMany(g => g.Senses)).SelectMany(s => s.Examples).ToList();
        foreach (var example in examples.EnumerateArray())
        {
            var zh = example.GetProperty("example").GetString() ?? "";
            var ka = example.GetProperty("translation").GetString() ?? "";
            if (!contentExamples.Any(e => e.Zh.Trim().TrimEnd('。') == zh.Trim().TrimEnd('。') && e.Ka.Trim() == ka.Trim()))
            {
                parsed.Issues.Add(new("SupplementaryExampleNeedsReview", "sourceMetadata", 0, example.GetRawText()));
                db.Comments.Add(new Comment { EntryId = entry.Id, AuthorEditorId = actorId,
                    CommentText = $"Legacy supplementary example #{example.GetProperty("id")}:\n{zh}\n{ka}",
                    CreatedAtUtc = entry.CreatedAtUtc, UpdatedAtUtc = entry.UpdatedAtUtc });
            }
        }
    }
    await db.SaveChangesAsync();
    var snapshot = new { schemaVersion = 1, sourceSystem = args[2], sourceHash = hash,
        source = parsed.Source, issues = parsed.Issues, content = EntryTreeMapper.Read(mapper.Nodes),
        forms = EntryTreeMapper.ReadForms(entry), reference = r };
    db.AuditLogEntries.Add(new AuditLogEntry
    {
        EntityType = "LegacyImport", EntityId = entry.Id, Action = AuditAction.Created,
        PerformedByEditorId = actorId, PerformedAtUtc = entry.CreatedAtUtc, Notes = key,
        NewValue = JsonSerializer.Serialize(snapshot, json)
    });
    outcomes.Add(new { parsed.Source.SourceId, parsed.Source.Word, entryId = entry.Id,
        action = apply ? "imported-draft" : "dry-run-rolled-back", segments = mapper.Nodes.Count,
        senses = tree.Senses.Count + tree.GramGroups.Sum(g => g.Senses.Count),
        examples = tree.Senses.Concat(tree.GramGroups.SelectMany(g => g.Senses)).Sum(s => s.Examples.Count),
        issues = parsed.Issues });
}
await db.SaveChangesAsync();
// Verify persisted trees and untouched raw data within the same transaction.
foreach (var item in imported)
{
    var nodes = await db.Segments.AsNoTracking().Where(s => s.EntryId == item.Entry.Id).ToListAsync();
    var read = EntryTreeMapper.Read(nodes);
    if (read.GramGroups.Count + read.Senses.Count + read.Homonyms.Count == 0 ||
        !await db.ReferenceMaterials.AnyAsync(r => r.EntryId == item.Entry.Id && r.OriginalRawReferenceMaterial == (item.Parsed.Source.WorkingSet ?? "")))
        throw new InvalidDataException($"Verification failed for {item.Parsed.Source.SourceId}");
}
var report = new { mode = apply ? "apply" : "dry-run", sourceSystem = args[2], count = rows.Count, outcomes };
// Write review report before committing so a filesystem failure cannot produce an unreported import.
await File.WriteAllTextAsync(args[4], JsonSerializer.Serialize(report, json));
if (apply) await tx.CommitAsync(); else await tx.RollbackAsync();
Console.WriteLine($"{(apply ? "Committed" : "Rolled back dry-run")}: {imported.Count} new drafts; {rows.Count - imported.Count} already imported.");
return 0;

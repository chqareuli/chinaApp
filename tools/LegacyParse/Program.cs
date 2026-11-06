using System.Text.Encodings.Web;
using System.Text.Json;
using Chiniseapp.Application.Legacy;

if (args.Length is < 2 or > 3 || (args.Length == 3 && args[2] is not ("--semantic" or "--complex")))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/LegacyParse -- input.json report.json [--semantic|--complex]");
    return 1;
}
if (Path.GetFullPath(args[0]).Equals(Path.GetFullPath(args[1]), StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Input and report paths must differ.");
    return 1;
}
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};
var sources = JsonSerializer.Deserialize<List<LegacyArticle>>(await File.ReadAllTextAsync(args[0]), options)
    ?? throw new InvalidDataException("Expected an array of legacy articles.");
if (sources.Any(s => s is null || s.SourceId is null || s.Word is null))
    throw new InvalidDataException("Each article requires sourceId and word.");
var results = args.Length == 3 ? sources.Select(row => LegacySemanticParser.Parse(row, args[2] == "--complex")).ToList() : LegacyParser.ParseBatch(sources);
foreach (var group in results.GroupBy(r => r.Source.Word.Trim().Replace('，', ','), StringComparer.Ordinal).Where(g => g.Count() > 1))
    foreach (var row in group) row.Issues.Add(new("DuplicateHeadword", "word", 0, row.Source.Word));
results = results.Select(r => r.Issues.Any(i => i.Code == "DuplicateHeadword") ? r with { ProposedContent = null } : r).ToList();
var report = new
{
    parserVersion = args.Length == 2 ? "0.1" : args[2] == "--complex" ? "0.3-complex" : "0.2-semantic",
    mode = "dry-run", total = results.Count,
    proposed = results.Count(r => r.ProposedContent is not null),
    requiresReview = results.Count(r => r.RequiresReview),
    issuesByCode = results.SelectMany(r => r.Issues).GroupBy(i => i.Code)
        .ToDictionary(g => g.Key, g => g.Count()),
    results
};
await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(report, options));
Console.WriteLine($"Analyzed {results.Count} articles; {report.proposed} proposals; {report.requiresReview} require review. No database writes.");
return 0;

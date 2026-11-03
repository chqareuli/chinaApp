using System.Text.Json;
using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Infrastructure.Entries;

namespace Chiniseapp.Tests;

internal static class ExportTestSamples
{
    // Opt-in artifacts for the standalone JSON Schema check; ordinary test runs write nothing.
    public static void Write(string name, EntryDetail entry, params MediaAsset[] media)
    {
        var directory = Environment.GetEnvironmentVariable("CHINESE_EXPORT_SAMPLES");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name + ".json"), JsonSerializer.Serialize(new[] {
            EntryExportSerializer.DictionaryContent(entry, media) }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        File.WriteAllText(Path.Combine(directory, name + ".xml"), EntryExportSerializer.Xml([entry], media));
    }
}

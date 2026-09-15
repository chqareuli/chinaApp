using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Chiniseapp.Tests;

public sealed class LocalPostgresFactAttribute : FactAttribute
{
    public LocalPostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CHINESE_TEST_POSTGRES")))
            Skip = "Set CHINESE_TEST_POSTGRES to run against an isolated local PostgreSQL database.";
    }
}

public class StructuredEntryPostgresTests
{
    [LocalPostgresFact]
    public async Task SavesReloadsPreservesCommentsAndRejectsStaleWrites()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CHINESE_TEST_POSTGRES"));
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        var database = "chinese_editor_test_" + Guid.NewGuid().ToString("N");
        connection.Database = "postgres";
        connection.Pooling = false;
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            connection.Database = database;
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString }).Build();
            await using var provider = new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
            int entryId, editorId, senseId;
            uint firstVersion;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ChiniseDbContext>();
                await db.Database.MigrateAsync();
                var editor = new Editor { Email = "local-test@example.test", DisplayName = "Test Editor", Role = EditorRole.SuperAdmin,
                    FirstName = "Test", LastName = "Editor", ChineseName = "测试", WeChatAccount = "test-wechat",
                    EditorialRole = nameof(EditorRole.ChiefEditor), CreatedAtUtc = DateTime.UtcNow };
                db.Editors.Add(editor);
                await db.SaveChangesAsync();
                editorId = editor.Id;
                var service = scope.ServiceProvider.GetRequiredService<IEntryService>();
                var entry = await service.CreateAsync(new("测试", null) { Pinyins = ["cèshì"] }, editorId, EditorRole.SuperAdmin);
                entryId = entry.Id;
                var saved = await service.SaveContentAsync(entryId, new("测试", null, [], entry.RowVersion)
                {
                    Pinyins = entry.Pinyins,
                    ComplexForms = [new(null, "測試", 0)],
                    Senses = [new(1, "ტესტი", [new("你好", "გამარჯობა")])],
                    GramGroups = [new() { Pos = "n", Senses = [new(1, "სახელი", [])] }]
                }, editorId, EditorRole.SuperAdmin);
                firstVersion = saved.RowVersion;
                senseId = saved.Senses[0].Id!.Value;
                Assert.True(senseId > 0);
                Assert.Equal("測試", saved.ComplexForms[0].Value);
                db.Comments.Add(new Comment { EntryId = entryId, TargetSegmentId = senseId, AuthorEditorId = editorId,
                    CommentText = "keep this anchor", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }
            await using (var scope = provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IEntryService>();
                var entry = (await service.GetAsync(entryId, EditorRole.SuperAdmin))!;
                Assert.Equal("cèshì", entry.Pinyins[0].Value);
                Assert.Equal("გამარჯობა", entry.Senses[0].Examples[0].Ka);
                // Move the existing sense under a grammar node while deleting its old sibling.
                var group = entry.GramGroups[0] with { Senses = [entry.Senses[0] with { Definition = "შეცვლილი" }] };
                var saved = await service.SaveContentAsync(entryId, new("测试", null, [], entry.RowVersion)
                    { Pinyins = entry.Pinyins, ComplexForms = entry.ComplexForms, GramGroups = [group] }, editorId, EditorRole.SuperAdmin);
                Assert.Equal(senseId, saved.GramGroups[0].Senses[0].Id);
                var db = scope.ServiceProvider.GetRequiredService<ChiniseDbContext>();
                var reloadedEditor = await db.Editors.SingleAsync(e => e.Id == editorId);
                Assert.Equal("Test", reloadedEditor.FirstName);
                Assert.Equal("Editor", reloadedEditor.LastName);
                Assert.Equal("测试", reloadedEditor.ChineseName);
                Assert.Equal("test-wechat", reloadedEditor.WeChatAccount);
                Assert.Equal(nameof(EditorRole.ChiefEditor), reloadedEditor.EditorialRole);
                Assert.Equal(EditorRole.SuperAdmin, reloadedEditor.Role);
                Assert.False(reloadedEditor.IsCancelled);
                Assert.Equal(senseId, (await db.Comments.SingleAsync()).TargetSegmentId);
            }
            await using (var scope = provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IEntryService>();
                await Assert.ThrowsAsync<EntryConcurrencyException>(() => service.SaveContentAsync(entryId,
                    new("stale", null, [], firstVersion) { Senses = [new(1, "must not persist", [])] }, editorId, EditorRole.SuperAdmin));
            }
            await using (var scope = provider.CreateAsyncScope())
            {
                var entry = (await scope.ServiceProvider.GetRequiredService<IEntryService>().GetAsync(entryId, EditorRole.SuperAdmin))!;
                Assert.Equal("测试", entry.Lemma);
                Assert.Equal("შეცვლილი", entry.GramGroups[0].Senses[0].Definition);
                Assert.Equal(senseId, entry.GramGroups[0].Senses[0].Id);
                var service = scope.ServiceProvider.GetRequiredService<IEntryService>();
                var homographs = await service.CreateAsync(new("行",null),editorId,EditorRole.SuperAdmin);
                var withSenses=await service.SaveContentAsync(homographs.Id,new("行",null,
                    [new(null){GramGroups=[],DirectSenses=[new(1,"პირველი",[])]},
                     new(null){GramGroups=[],DirectSenses=[new(1,"მეორე",[])]}],homographs.RowVersion),editorId,EditorRole.SuperAdmin);
                var directId=withSenses.Homonyms[0].DirectSenses[0].Id;
                Assert.NotNull(directId);
                var reloaded=(await service.GetAsync(homographs.Id,EditorRole.SuperAdmin))!;
                Assert.Empty(reloaded.Homonyms[0].GramGroups!);
                Assert.Equal("პირველი",reloaded.Homonyms[0].DirectSenses[0].Definition);
                var again=await service.SaveContentAsync(reloaded.Id,new(reloaded.Lemma,null,reloaded.Homonyms,reloaded.RowVersion),editorId,EditorRole.SuperAdmin);
                Assert.Equal(directId,again.Homonyms[0].DirectSenses[0].Id);
            }
        }
        finally
        {
            // This name is generated above, never obtained from user configuration.
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
}


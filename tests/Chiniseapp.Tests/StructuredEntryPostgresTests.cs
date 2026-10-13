using Chiniseapp.Application.Entries;
using Chiniseapp.Application.Comments;
using System.Text.Json;
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
                var draft = await service.CreateAsync(new("草稿", null), editorId, EditorRole.SuperAdmin);
                var incomplete = await service.SaveContentAsync(draft.Id, new(null, null, [], draft.RowVersion) {
                    GramGroups = [new() { Senses = [new(1, "", [])] }] }, editorId, EditorRole.SuperAdmin);
                Assert.Equal("NewEntry", incomplete.Status);
                await Assert.ThrowsAsync<EntryValidationException>(() => service.ChangeStatusAsync(draft.Id, EntryStatus.ZhReview, editorId));
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
                var audit = await db.AuditLogEntries.Where(a => a.EntityId == entryId && a.Action == AuditAction.ContentEdited)
                    .OrderByDescending(a => a.Id).FirstAsync();
                using var before = JsonDocument.Parse(audit.OldValue!);
                using var after = JsonDocument.Parse(audit.NewValue!);
                Assert.Equal("გამარჯობა", before.RootElement.GetProperty("content").GetProperty("Senses")[0].GetProperty("Examples")[0].GetProperty("Ka").GetString());
                Assert.Equal(saved.GramGroups[0].Senses[0].Id, after.RootElement.GetProperty("content").GetProperty("GramGroups")[0].GetProperty("Senses")[0].GetProperty("Id").GetInt32());
                var commentService = scope.ServiceProvider.GetRequiredService<ICommentService>();
                var targeted = await commentService.AddAsync(entryId, new("Targeted note") { TargetSegmentId = senseId }, editorId);
                var reply = await commentService.AddAsync(entryId, new("Reply") { ReplyToCommentId = targeted.Id }, editorId);
                Assert.Equal(targeted.Id, reply.ReplyToCommentId);
                Assert.Equal(senseId, reply.TargetSegmentId);
                Assert.Equal(nameof(EditorRole.SuperAdmin), targeted.AuthorRole);
                var linkedComment = await commentService.AddAsync(entryId, new("See 测试"), editorId);
                Assert.Equal(entryId, Assert.Single(linkedComment.EntryLinks!).EntryId);
                var linkedEntry = await db.Entries.FindAsync(entryId);
                var savedLemma = linkedEntry!.Lemma; linkedEntry.Lemma = "测试改名"; await db.SaveChangesAsync();
                var afterRename = (await commentService.GetForEntryAsync(entryId)).Single(c => c.Id == linkedComment.Id);
                Assert.Equal(entryId, Assert.Single(afterRename.EntryLinks!).EntryId);
                var editedAfterRename = await commentService.UpdateTextAsync(linkedComment.Id, "Still see 测试", editorId, EditorRole.SuperAdmin);
                Assert.Equal(entryId, Assert.Single(editedAfterRename!.EntryLinks!).EntryId);
                linkedEntry.Lemma = savedLemma; await db.SaveChangesAsync();
                var other = new Editor { Email = "reply-test@example.test", DisplayName = "Reply Editor", Role = EditorRole.SuperAdmin,
                    FirstName = "Reply", LastName = "Editor", ChineseName = "回复", WeChatAccount = "reply-wechat", CreatedAtUtc = DateTime.UtcNow };
                db.Editors.Add(other); await db.SaveChangesAsync();
                await commentService.AddAsync(entryId, new("Reply from another editor") { ReplyToCommentId = targeted.Id }, other.Id);
                Assert.True(await db.Notifications.AnyAsync(n => n.EntryId == entryId && n.RecipientEditorId == editorId && n.TriggeredByEditorId == other.Id));
                var referenceController = new Chiniseapp.Api.Controllers.ReferenceMaterialController(
                    scope.ServiceProvider.GetRequiredService<Chiniseapp.Application.ReferenceMaterials.IReferenceMaterialService>(), db,
                    scope.ServiceProvider.GetRequiredService<Chiniseapp.Application.Notifications.INotificationService>(),
                    scope.ServiceProvider.GetRequiredService<Chiniseapp.Application.Scoring.IScoringService>());
                referenceController.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext {
                    HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext {
                        User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([
                            new(System.Security.Claims.ClaimTypes.NameIdentifier, other.Id.ToString()),
                            new(System.Security.Claims.ClaimTypes.Role, nameof(EditorRole.SuperAdmin))], "test")) }};
                await referenceController.Save(entryId, new("都 dōu\n● all", "", "", "", ""), CancellationToken.None);
                var referenceAudit = await db.AuditLogEntries.Where(a => a.EntityId == entryId && a.Notes!.StartsWith("Reference materials"))
                    .OrderByDescending(a => a.Id).FirstAsync();
                using var referenceSnapshot = JsonDocument.Parse(referenceAudit.NewValue!);
                Assert.Equal("都 dōu\n● all", referenceSnapshot.RootElement.GetProperty("reference").GetProperty("G1").GetString());
                Assert.True(referenceSnapshot.RootElement.TryGetProperty("content", out _));
                referenceController.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([
                    new(System.Security.Claims.ClaimTypes.NameIdentifier, other.Id.ToString()),
                    new(System.Security.Claims.ClaimTypes.Role, nameof(EditorRole.KaEditor))], "test"));
                var deniedReference = await referenceController.Get(entryId, CancellationToken.None);
                Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(deniedReference.Result);
                var deniedSave = await referenceController.Save(entryId, new("must not save", "", "", "", ""), CancellationToken.None);
                Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(deniedSave.Result);
                Assert.Equal("都 dōu\n● all", (await db.ReferenceMaterials.SingleAsync(r => r.EntryId == entryId)).G1);
                await Assert.ThrowsAsync<CommentValidationException>(() => commentService.AddAsync(entryId, new("Foreign target") { TargetSegmentId = int.MaxValue }, editorId));
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


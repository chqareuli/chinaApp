using System.Security.Claims;
using System.Text.Json;
using Chiniseapp.Api.Controllers;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Npgsql;
using Chiniseapp.Application.Entries;

namespace Chiniseapp.Tests;
public class MediaManagementPostgresTests
{
    [LocalPostgresFact]
    public async Task CleaningReportedStaleLinksKeepsEveryFileAndTheActiveSaraLink()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CHINESE_TEST_POSTGRES"));
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        var database = "chinese_media_cleanup_test_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), database);
        connection.Database = "postgres"; connection.Pooling = false;
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await command.ExecuteNonQueryAsync();
        try
        {
            connection.Database = database;
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString }).Build();
            await using var provider = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ChiniseDbContext>();
            await db.Database.MigrateAsync();
            var editor = new Editor { Email = "cleanup@example.test", DisplayName = "Editor", Role = EditorRole.SuperAdmin, IsActive = true, CreatedAtUtc = DateTime.UtcNow };
            db.Editors.Add(editor); await db.SaveChangesAsync();
            var entries = scope.ServiceProvider.GetRequiredService<IEntryService>();
            await entries.CreateAsync(new("占位", null), editor.Id, editor.Role);
            var entry = await entries.CreateAsync(new("标测", null), editor.Id, editor.Role);
            Assert.Equal(2, entry.Id);
            db.Segments.AddRange(new Segment { Id = 811, EntryId = 2, SegmentType = SegmentType.Sense, Number = 1 },
                new Segment { Id = 814, EntryId = 2, ParentSegmentId = 811, SegmentType = SegmentType.Definition, Value = "Current definition" });
            await db.SaveChangesAsync();
            var keys = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid().ToString("N")).ToArray();
            int[] linkIds = [12, 14, 15, 16, 22], parents = [138, 119, 131, 119, 811];
            var media = linkIds.Select((id, i) => new MediaAsset { Id = id, EntryId = 2, InsertedIntoSegmentId = id == 22 ? 811 : null,
                SafeStorageName = keys[i == 4 ? 0 : i], StorageUrl = $"/media/{id}/content", MimeType = "image/png", OriginalFilename = $"{id}.png",
                DisplayName = id is 12 or 22 ? "SARA" : $"Image {id}", UploadedByEditorId = editor.Id, UploadedAtUtc = DateTime.UtcNow,
                Placement = "inline:" + JsonSerializer.Serialize(new { kind = "sense", nodeId = parents[i], field = "definition", offset = 0, before = "", after = "" }) }).ToList();
            db.MediaAssets.AddRange(media); await db.SaveChangesAsync();
            var storage = Path.Combine(directory, "App_Data", "media");
            Directory.CreateDirectory(storage);
            foreach (var key in keys) await File.WriteAllBytesAsync(Path.Combine(storage, key), [137, 80, 78, 71]);
            var controller = new MediaController(db, new TestEnvironment(directory)) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
            void Role(EditorRole role) => controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, editor.Id.ToString()), new Claim(ClaimTypes.Role, role.ToString())], "Test"));
            Role(EditorRole.KaEditor);
            Assert.IsType<ForbidResult>(await controller.DetachStaleLink(12, default));
            Role(EditorRole.SuperAdmin);
            var activePlacement = media[4].Placement;
            Assert.IsType<ConflictObjectResult>(await controller.DetachStaleLink(22, default));
            foreach (var id in linkIds.Take(4)) Assert.IsType<NoContentResult>(await controller.DetachStaleLink(id, default));
            Assert.Equal(5, await db.MediaAssets.CountAsync());
            Assert.All(keys, key => Assert.True(File.Exists(Path.Combine(storage, key))));
            Assert.Equal(activePlacement, media[4].Placement);
            Assert.Equal(811, media[4].InsertedIntoSegmentId);
            var listed = Assert.IsType<OkObjectResult>(await controller.List(2, default));
            var list = JsonSerializer.SerializeToElement(listed.Value);
            Assert.Equal(22, Assert.Single(list.EnumerateArray()).GetProperty("Id").GetInt32());
            var library = JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(await controller.Library()).Value);
            Assert.Equal(4, library.GetProperty("items").GetArrayLength());
            Assert.IsType<PhysicalFileResult>(await controller.Content(14, default));
            Assert.Equal(4, await db.AuditLogEntries.CountAsync(a => a.EntityType == nameof(MediaAsset) && a.Action == AuditAction.Other));
            var detail = (await entries.GetAsync(2, editor.Role))!;
            var exported = JsonSerializer.SerializeToElement(Chiniseapp.Infrastructure.Entries.EntryExportSerializer.DictionaryContent(detail, media), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.Equal(22, Assert.Single(exported.GetProperty("media").EnumerateArray()).GetProperty("id").GetInt32());
            Assert.Empty(exported.GetProperty("exportWarnings").EnumerateArray());
            Assert.IsType<OkObjectResult>(await controller.Reuse(2, new(14, "sense:811"), default));
            Assert.All(keys, key => Assert.True(File.Exists(Path.Combine(storage, key))));
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [LocalPostgresFact]
    public async Task RemovingAnImageParentRollsBackUntilTheMediaLinkIsRemoved()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CHINESE_TEST_POSTGRES"));
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        var database = "chinese_media_parent_test_" + Guid.NewGuid().ToString("N");
        connection.Database = "postgres";
        connection.Pooling = false;
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await create.ExecuteNonQueryAsync();
        try
        {
            connection.Database = database;
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString }).Build();
            await using var provider = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
            int entryId, editorId, senseId, mediaId;
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ChiniseDbContext>();
                await db.Database.MigrateAsync();
                var editor = new Editor { Email = "media-parent@example.test", DisplayName = "Media Editor", Role = EditorRole.SuperAdmin,
                    IsActive = true, CreatedAtUtc = DateTime.UtcNow };
                db.Editors.Add(editor);
                await db.SaveChangesAsync();
                editorId = editor.Id;
                var service = scope.ServiceProvider.GetRequiredService<IEntryService>();
                var entry = await service.CreateAsync(new("图", null), editorId, EditorRole.SuperAdmin);
                var saved = await service.SaveContentAsync(entry.Id, new(null, null, [], entry.RowVersion) {
                    Senses = [new(1, "Image definition", []), new(2, "Unrelated definition", [])] }, editorId, EditorRole.SuperAdmin);
                entryId = saved.Id;
                senseId = saved.Senses[0].Id!.Value;
                var media = new MediaAsset { EntryId = entryId, InsertedIntoSegmentId = senseId, Placement = $"sense:{senseId}",
                    SafeStorageName = Guid.NewGuid().ToString("N"), StorageUrl = "/image", MimeType = "image/png", OriginalFilename = "image.png",
                    UploadedByEditorId = editorId, UploadedAtUtc = DateTime.UtcNow };
                db.MediaAssets.Add(media);
                await db.SaveChangesAsync();
                mediaId = media.Id;
            }
            await using (var scope = provider.CreateAsyncScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<IEntryService>();
                var entry = (await service.GetAsync(entryId, EditorRole.SuperAdmin))!;
                var error = await Assert.ThrowsAsync<EntryValidationException>(() => service.SaveContentAsync(entryId,
                    new("Must roll back", null, [], entry.RowVersion) { Senses = [entry.Senses[1]] }, editorId, EditorRole.SuperAdmin));
                Assert.Contains($"media {mediaId}, segment {senseId}", error.Message);
            }
            await using (var scope = provider.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ChiniseDbContext>();
                var service = scope.ServiceProvider.GetRequiredService<IEntryService>();
                var entry = (await service.GetAsync(entryId, EditorRole.SuperAdmin))!;
                Assert.Equal("图", entry.Lemma);
                Assert.Equal(2, entry.Senses.Count);
                var media = await db.MediaAssets.SingleAsync(m => m.Id == mediaId);
                Assert.Equal(senseId, media.InsertedIntoSegmentId);
                Assert.Equal($"sense:{senseId}", media.Placement);
                // Reordering and editing the surviving parent keeps the same media anchor.
                var edited = await service.SaveContentAsync(entryId, new(null, null, [], entry.RowVersion) {
                    Senses = [entry.Senses[1], entry.Senses[0] with { Definition = "Edited image definition" }] }, editorId, EditorRole.SuperAdmin);
                Assert.Equal(senseId, edited.Senses[1].Id);
                var originalKey = media.SafeStorageName;
                var controller = new MediaController(db, new TestEnvironment(Path.GetTempPath())) {
                    ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
                void SetRole(EditorRole role) => controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, editorId.ToString()), new Claim(ClaimTypes.Role, role.ToString())], "Test"));
                SetRole(EditorRole.SuperAdmin);
                Assert.IsType<BadRequestObjectResult>(await controller.Reattach(mediaId, new("sense:999999"), default));
                SetRole(EditorRole.KaEditor);
                Assert.IsType<ForbidResult>(await controller.Reattach(mediaId, new($"sense:{edited.Senses[0].Id}"), default));
                SetRole(EditorRole.SuperAdmin);
                Assert.IsType<OkObjectResult>(await controller.Reattach(mediaId, new($"sense:{edited.Senses[0].Id}"), default));
                Assert.Equal(originalKey, media.SafeStorageName);
                Assert.Equal(edited.Senses[0].Id, media.InsertedIntoSegmentId);
                var removed = await service.SaveContentAsync(entryId, new(null, null, [], edited.RowVersion) {
                    Senses = [edited.Senses[0]] }, editorId, EditorRole.SuperAdmin);
                Assert.Single(removed.Senses);
                Assert.False(await db.Segments.AnyAsync(s => s.Id == senseId));
                Assert.True(await db.MediaAssets.AnyAsync(m => m.Id == mediaId && m.SafeStorageName == originalKey));
            }
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed class TestEnvironment(string path) : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = path;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = path;
        public string EnvironmentName { get; set; } = "Test";
    }
    [LocalPostgresFact]
    public async Task SharedImagesRenameAndReplaceTogetherWithoutBreakingLinksOrPermissions()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CHINESE_TEST_POSTGRES"));
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        var database = "chinese_media_test_" + Guid.NewGuid().ToString("N");
        var directory = Path.Combine(Path.GetTempPath(), database);
        connection.Database = "postgres"; connection.Pooling = false;
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await command.ExecuteNonQueryAsync();
        try
        {
            connection.Database = database;
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString }).Build();
            await using var provider = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ChiniseDbContext>();
            await db.Database.MigrateAsync();
            var editor = new Editor { Email="media@example.test", DisplayName="Media Editor", Role=EditorRole.SuperAdmin, IsActive=true, CreatedAtUtc=DateTime.UtcNow };
            db.Editors.Add(editor); await db.SaveChangesAsync();
            var entries = scope.ServiceProvider.GetRequiredService<Chiniseapp.Application.Entries.IEntryService>();
            var first = await entries.CreateAsync(new("甲",null),editor.Id,editor.Role);
            var second = await entries.CreateAsync(new("乙",null),editor.Id,editor.Role);
            var key = Guid.NewGuid().ToString("N");
            var shared = new[]{first.Id,second.Id}.Select(id=>new MediaAsset {EntryId=id,DisplayName="Original",OriginalFilename="old.png",SafeStorageName=key,FileType=MediaFileType.Image,MimeType="image/png",UploadedByEditorId=editor.Id,UploadedAtUtc=DateTime.UtcNow}).ToArray();
            var other = new MediaAsset {EntryId=first.Id,DisplayName="Taken",OriginalFilename="other.png",SafeStorageName=Guid.NewGuid().ToString("N"),FileType=MediaFileType.Image,MimeType="image/png",UploadedByEditorId=editor.Id,UploadedAtUtc=DateTime.UtcNow};
            db.MediaAssets.AddRange(shared); db.MediaAssets.Add(other); await db.SaveChangesAsync();
            foreach(var asset in shared) asset.StorageUrl=$"/media/{asset.Id}/content";
            await db.SaveChangesAsync();
            Directory.CreateDirectory(Path.Combine(directory,"App_Data","media"));
            var path=Path.Combine(directory,"App_Data","media",key);
            await File.WriteAllBytesAsync(path,[137,80,78,71,13,10,26,10,1,2,3,4]);
            var controller=new MediaController(db,new TestEnvironment(directory)) { ControllerContext=new ControllerContext {HttpContext=new DefaultHttpContext()} };
            void Role(EditorRole role) => controller.HttpContext.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,editor.Id.ToString()),new Claim(ClaimTypes.Role,role.ToString())],"Test"));
            Role(EditorRole.SuperAdmin);
            Assert.IsType<ConflictObjectResult>(await controller.Rename(shared[0].Id,new(" taken "),default));
            Assert.IsType<NoContentResult>(await controller.Rename(shared[0].Id,new("My exact name"),default));
            Assert.Equal(2,await db.MediaAssets.CountAsync(m=>m.DisplayName=="My exact name"));
            var library=Assert.IsType<OkObjectResult>(await controller.Library());
            var json=JsonSerializer.SerializeToElement(library.Value);
            var result=json.GetProperty("items").EnumerateArray().Single(m=>m.GetProperty("Id").GetInt32()==shared[0].Id);
            Assert.Equal(2,result.GetProperty("LinkedEntries").GetArrayLength());
            byte[] replacement=[255,216,255,1,2,3,4,5,6,7,8,9];
            using var stream=new MemoryStream(replacement);
            var file=new FormFile(stream,0,replacement.Length,"file","new.jpg");
            Role(EditorRole.KaEditor);
            Assert.IsType<ForbidResult>(await controller.Replace(shared[0].Id,file,default));
            Assert.IsType<ForbidResult>(await controller.Rename(shared[0].Id,new("Denied"),default));
            stream.Position=0; Role(EditorRole.SuperAdmin);
            Assert.IsType<NoContentResult>(await controller.Replace(shared[0].Id,file,default));
            Assert.Equal(replacement,await File.ReadAllBytesAsync(path));
            Assert.Equal(2,await db.MediaAssets.CountAsync(m=>m.SafeStorageName==key && m.MimeType=="image/jpeg" && m.DisplayName=="My exact name"));
            Assert.All(await db.MediaAssets.Where(m=>m.SafeStorageName==key).ToListAsync(),m=>Assert.Equal($"/media/{m.Id}/content",m.StorageUrl));
        }
        finally
        {
            await using var command=new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin); await command.ExecuteNonQueryAsync();
            if(Directory.Exists(directory))Directory.Delete(directory,true);
        }
    }
}

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

namespace Chiniseapp.Tests;
public class MediaManagementPostgresTests
{
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

using Chiniseapp.Application.Notifications;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Chiniseapp.Tests;

public class CollaborationPostgresTests
{
    [LocalPostgresFact]
    public async Task BroadcastsStaySeparateFromPrivateMessagesAndReadsArePerRecipient()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CHINESE_TEST_POSTGRES"));
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        var database = "chinese_messages_test_" + Guid.NewGuid().ToString("N");
        connection.Database = "postgres";
        connection.Pooling = false;
        await using var admin = new NpgsqlConnection(connection.ConnectionString);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE {database}", admin)) await command.ExecuteNonQueryAsync();
        try
        {
            connection.Database = database;
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString }).Build();
            await using var provider = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ChiniseDbContext>();
            await db.Database.MigrateAsync();
            var people = Enumerable.Range(1,4).Select(i=>new Editor { Email=$"test{i}@example.test",DisplayName=$"Editor {i}",
                Role=EditorRole.Editor, IsActive=i!=4, CreatedAtUtc=DateTime.UtcNow }).ToArray();
            db.Editors.AddRange(people);
            await db.SaveChangesAsync();
            var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var group = await service.SendMessageAsync(people[0].Id,new(0,"Group message") {AllEditors=true});
            Assert.NotNull(group.BroadcastId);
            Assert.Equal(2,await db.DirectMessages.CountAsync());
            await service.SendMessageAsync(people[0].Id,new(people[1].Id,"Private message"));
            db.ChangeTracker.Clear();
            var sender = await service.GetInboxAsync(people[0].Id);
            Assert.Equal(2,sender.Count);
            var first = await service.GetInboxAsync(people[1].Id);
            var second = await service.GetInboxAsync(people[2].Id);
            Assert.Equal(2,first.Count);
            Assert.Single(second);
            Assert.Empty(await service.GetInboxAsync(people[3].Id));
            var firstGroup = first.Single(m=>m.BroadcastId!=null);
            Assert.False(await service.MarkMessageReadAsync(firstGroup.Id,people[2].Id));
            Assert.True(await service.MarkMessageReadAsync(firstGroup.Id,people[1].Id));
            Assert.NotNull((await service.GetInboxAsync(people[1].Id)).Single(m=>m.BroadcastId!=null).ReadAtUtc);
            Assert.Null((await service.GetInboxAsync(people[2].Id)).Single().ReadAtUtc);
            await Assert.ThrowsAsync<NotificationValidationException>(()=>service.SendMessageAsync(people[0].Id,new(0," ") {AllEditors=true}));
            var entryService = scope.ServiceProvider.GetRequiredService<Chiniseapp.Application.Entries.IEntryService>();
            var entry = await entryService.CreateAsync(new("词",null),people[0].Id,EditorRole.Editor);
            var notification = new Notification {EntryId=entry.Id,RecipientEditorId=people[0].Id,
                TriggeredByEditorId=people[1].Id,Type="CommentAdded",Detail="核对例句",CreatedAtUtc=DateTime.UtcNow};
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var preview = Assert.Single(await service.GetForEditorAsync(people[0].Id,true));
            Assert.Equal("核对例句",preview.Detail);
            Assert.Equal("Editor",preview.TriggeredByRole);
            Assert.Equal("NewEntry",preview.EntryStatus);
            Assert.False(await service.MarkReadAsync(preview.Id,people[1].Id));
            Assert.True(await service.MarkReadAsync(preview.Id,people[0].Id));
            Assert.Empty(await service.GetForEditorAsync(people[0].Id,true));
        }
        finally
        {
            await using var command = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin);
            await command.ExecuteNonQueryAsync();
        }
    }
}

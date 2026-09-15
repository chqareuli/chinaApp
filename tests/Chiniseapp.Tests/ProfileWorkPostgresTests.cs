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

public class ProfileWorkPostgresTests
{
    [LocalPostgresFact]
    public async Task WorkCountsAndListsUseTheSamePersistedScoresAndIncludeDrafts()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CHINESE_TEST_POSTGRES"));
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        var database = "chinese_profile_test_" + Guid.NewGuid().ToString("N");
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
            var entries = scope.ServiceProvider.GetRequiredService<Chiniseapp.Application.Entries.IEntryService>();
            var accounting = scope.ServiceProvider.GetRequiredService<Chiniseapp.Application.Accounting.IAccountingService>();
            var draft = await entries.CreateAsync(new("新",null),people[0].Id,EditorRole.Editor);
            var other = await entries.CreateAsync(new("词",null),people[1].Id,EditorRole.Editor);
            var saved = await db.Entries.FindAsync(other.Id);
            saved!.Status=EntryStatus.Ready;
            db.ScoreEntries.Add(new ScoreEntry {EntryId=other.Id,EditorId=people[0].Id,ScoreType=ScoreType.Additional,AwardedAtUtc=DateTime.UtcNow});
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var detail=(await accounting.GetEditorDetailAsync(people[0].Id))!;
            Assert.Equal(1,detail.EntriesByStatus["NewEntry"]);
            Assert.Equal(1,detail.EntriesByStatus["Ready"]);
            Assert.Equal(1,detail.ScoresByStatus.Additional["Ready"]);
            var all=await entries.GetFilteredListAsync(new(WorkEditorId:people[0].Id),EditorRole.Editor);
            Assert.Equal(2,all.TotalCount);
            var scored=await entries.GetFilteredListAsync(new(WorkEditorId:people[0].Id,ScoreKind:"additional",Status:EntryStatus.Ready),EditorRole.Editor);
            Assert.Equal(other.Id,Assert.Single(scored.Items).Id);
            var main=await entries.GetFilteredListAsync(new(WorkEditorId:people[0].Id,ScoreKind:"main"),EditorRole.Editor);
            Assert.Empty(main.Items);
            var archived=await db.Entries.FindAsync(other.Id);
            archived!.Status=EntryStatus.Archived;
            await db.SaveChangesAsync();
            var history=(await accounting.GetEditorDetailAsync(people[0].Id))!;
            Assert.Equal(1,history.ScoresByStatus.Additional["Archived"]);
            var historicalList=await entries.GetFilteredListAsync(new(WorkEditorId:people[0].Id,ScoreKind:"additional"),EditorRole.Editor);
            Assert.Equal(other.Id,Assert.Single(historicalList.Items).Id);
        }
        finally
        {
            await using var command = new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin);
            await command.ExecuteNonQueryAsync();
        }
    }
}

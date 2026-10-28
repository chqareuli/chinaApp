using Chiniseapp.Application.Entries;
using Chiniseapp.Domain.Entities;
using Chiniseapp.Domain.Enums;
using Chiniseapp.Infrastructure;
using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Chiniseapp.Tests;

public class EntryTitleUniquenessPostgresTests
{
    [LocalPostgresFact]
    public async Task TitlesAreUniqueOnCreateRenameAndConcurrentWritesWhileLegacyContentSurvives()
    {
        var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("CHINESE_TEST_POSTGRES"));
        Assert.Contains(connection.Host,new[]{"localhost","127.0.0.1","::1"});
        var database="chinese_titles_test_"+Guid.NewGuid().ToString("N");
        connection.Database="postgres";connection.Pooling=false;
        await using var admin=new NpgsqlConnection(connection.ConnectionString);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE DATABASE {database}",admin))await create.ExecuteNonQueryAsync();
        try
        {
            connection.Database=database;
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:DefaultConnection"]=connection.ConnectionString}).Build();
            await using var provider=new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
            int editorId=0,legacyId;
            Entry Row(string title)=>new(){Lemma=title,SearchNormalizedTitle=title,Status=EntryStatus.NewEntry,CreatedByEditorId=editorId,LastEditorEditorId=editorId,CreatedAtUtc=DateTime.UtcNow,UpdatedAtUtc=DateTime.UtcNow};
            await using(var setup=provider.CreateAsyncScope())
            {
                var db=setup.ServiceProvider.GetRequiredService<ChiniseDbContext>();
                await db.GetService<IMigrator>().MigrateAsync("20261013170000_PopulateMediaDisplayNames");
                var editor=new Editor{Email="titles@example.test",DisplayName="Editor",Role=EditorRole.SuperAdmin,IsActive=true,CreatedAtUtc=DateTime.UtcNow};
                db.Editors.Add(editor);await db.SaveChangesAsync();editorId=editor.Id;
                var legacy=Row("旧词");db.Entries.AddRange(legacy,Row("旧词"));await db.SaveChangesAsync();legacyId=legacy.Id;
                await db.Database.MigrateAsync();
                Assert.Equal(2,await db.Entries.CountAsync(e=>e.Lemma=="旧词"));
            }
            async Task<EntryDetail> Create(string title)
            {
                await using var scope=provider.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<IEntryService>().CreateAsync(new(title,null),editorId,EditorRole.SuperAdmin);
            }
            async Task<EntryDetail> Rename(int id,string title)
            {
                await using var scope=provider.CreateAsyncScope();var service=scope.ServiceProvider.GetRequiredService<IEntryService>();
                var detail=(await service.GetAsync(id,EditorRole.SuperAdmin))!;
                return await service.SaveContentAsync(id,new(title,null,[],detail.RowVersion),editorId,EditorRole.SuperAdmin);
            }
            var original=await Create("Étude，词");
            await Assert.ThrowsAsync<EntryDuplicateTitleException>(()=>Create(" e\u0301TUDE,词 "));
            await Assert.ThrowsAsync<EntryDuplicateTitleException>(()=>Create("旧词"));
            await Rename(legacyId,"旧词"); // Existing duplicate records remain editable until reviewed.
            var other=await Create("别词");
            await Assert.ThrowsAsync<EntryDuplicateTitleException>(()=>Rename(other.Id,"Étude,词"));
            await Rename(original.Id,"Renamed");
            await Create("Étude,词"); // A rename releases its previous title.
            async Task<bool> Race(){try{await Create("并发");return true;}catch(EntryDuplicateTitleException){return false;}}
            Assert.Single((await Task.WhenAll(Race(),Race())).Where(created=>created));
            // Direct database writes also obey the constraint, without the service precheck.
            await using(var direct=provider.CreateAsyncScope())
            {
                var db=direct.ServiceProvider.GetRequiredService<ChiniseDbContext>();db.Entries.Add(Row("并发"));
                var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
                Assert.Equal("uq_entries_title",Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
            }
            async Task<bool> DirectRace(){
                await using var scope=provider.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ChiniseDbContext>();db.Entries.Add(Row("database-parallel"));
                try{await db.SaveChangesAsync();return true;}catch(DbUpdateException error) when(error.InnerException is PostgresException {ConstraintName:"uq_entries_title"}){return false;}
            }
            Assert.Single((await Task.WhenAll(DirectRace(),DirectRace())).Where(created=>created));
            // Reservation insertion is atomic with the entry write: rollback does not consume a title.
            await using(var rollback=provider.CreateAsyncScope())
            {
                var db=rollback.ServiceProvider.GetRequiredService<ChiniseDbContext>();await using var tx=await db.Database.BeginTransactionAsync();
                db.Entries.Add(Row("rollback"));await db.SaveChangesAsync();await tx.RollbackAsync();
            }
            await Create("rollback");
        }
        finally{await using var drop=new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
}

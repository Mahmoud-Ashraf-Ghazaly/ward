using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ward.Data;
using Xunit;

namespace ward.Tests
{
    public class DatabaseMigrationTests
    {
        [Fact]
        public void DisposableDatabase_CanConnectAndEnsureCreated_Successfully()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            using var context = new ApplicationDbContext(options);
            var created = context.Database.EnsureCreated();
            Assert.True(created);
            Assert.True(context.Database.CanConnect());
        }
    }
}

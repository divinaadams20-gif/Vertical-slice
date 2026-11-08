namespace Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

// Used ONLY by the dotnet-ef command-line tool. Never runs as part of the API.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "VerticalSlice.slnx")))
        {
            directory = directory.Parent;
        }

        var databasePath = directory is null
            ? "verticalslice.db"
            : Path.Combine(directory.FullName, "Api", "verticalslice.db");
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? $"Data Source={databasePath}";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}

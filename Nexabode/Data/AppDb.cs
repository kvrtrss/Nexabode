namespace Nexabode.Data;

// Where the app gets its database from. Everything asks for it here instead of building it directly,
// so a test can point the app's own code at a throwaway database in memory.
public static class AppDb
{
    public static Func<AppDbContext> Factory { get; set; } = () => new AppDbContext();

    public static AppDbContext Create() => Factory();

    // For tests only.
    public static void UseDefault() => Factory = () => new AppDbContext();
}

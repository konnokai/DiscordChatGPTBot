﻿using DiscordChatGPTBot.DataBase.Table;
using Microsoft.EntityFrameworkCore;

#nullable disable
namespace DiscordChatGPTBot.DataBase
{
    public class MainDbContext : DbContext
    {
        public DbSet<GuildConfig> GuildConfig { get; set; }
        public DbSet<ChannelConfig> ChannelConfig { get; set; }
        public DbSet<ChatHistroy> ChatHistroy { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={Program.GetDataFilePath("DataBase.db")}")
#if DEBUG
            //.LogTo((act) => System.IO.File.AppendAllText("DbTrackerLog.txt", act), Microsoft.Extensions.Logging.LogLevel.Information)
#endif
            .EnableSensitiveDataLogging();

        public static MainDbContext GetDbContext()
        {
            var context = new MainDbContext();
            context.Database.SetCommandTimeout(60);
            var conn = context.Database.GetDbConnection();
            conn.Open();
            using (var com = conn.CreateCommand())
            {
                com.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=OFF";
                com.ExecuteNonQuery();
            }
            return context;
        }
    }
}
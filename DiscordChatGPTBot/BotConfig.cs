using DiscordChatGPTBot;

public class BotConfig
{
    public string DiscordToken { get; set; } = "";
    public string AESKey { get; set; } = "";
    public string RedisOption { get; set; } = "127.0.0.1,syncTimeout=3000";
    public ulong TestSlashCommandGuildId { get; set; } = 0;
    public string WebHookUrl { get; set; } = "";
    public string UptimeKumaPushUrl { get; set; } = "";

    public void InitBotConfig()
    {
        if (Utility.InDocker)
        {
            if (!File.Exists("bot_config.json") || string.IsNullOrEmpty( File.ReadAllText("bot_config.json")))
            {
                try { File.WriteAllText("bot_config.json", JsonConvert.SerializeObject(new BotConfig(), Formatting.Indented)); } catch { }
                Log.Error($"bot_config.json 遺失，請依照 {Path.GetFullPath("bot_config.json")} 內的格式填入正確的數值");
                if (!Console.IsInputRedirected)
                    Console.ReadKey();
                Environment.Exit(0);
            }
        }
        else
        {
            try { File.WriteAllText("bot_config_example.json", JsonConvert.SerializeObject(new BotConfig(), Formatting.Indented)); } catch { }
            if (!File.Exists("bot_config.json"))
            {
                Log.Error($"bot_config.json 遺失，請依照 {Path.GetFullPath("bot_config_example.json")} 內的格式填入正確的數值");
                if (!Console.IsInputRedirected)
                    Console.ReadKey();
                Environment.Exit(3);
            }
        }

        try
        {
            var config = JsonConvert.DeserializeObject<BotConfig>(File.ReadAllText("bot_config.json"))!;

            if (string.IsNullOrWhiteSpace(config.DiscordToken))
            {
                Log.Error($"{nameof(DiscordToken)}遺失，請輸入至 bot_config.json 後重開Bot");
                if (!Console.IsInputRedirected)
                    Console.ReadKey();
                Environment.Exit(3);
            }

            if (string.IsNullOrWhiteSpace(config.WebHookUrl))
            {
                Log.Error($"{nameof(WebHookUrl)}遺失，請輸入至 bot_config.json 後重開Bot");
                if (!Console.IsInputRedirected)
                    Console.ReadKey();
                Environment.Exit(3);
            }

            if (string.IsNullOrEmpty(config.AESKey))
            {
                Log.Warn($"{nameof(AESKey)}遺失，自動產生...");
                config.AESKey = GenRandomKey();
                File.WriteAllText("bot_config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
            }

            DiscordToken = config.DiscordToken;
            AESKey = config.AESKey;
            WebHookUrl = config.WebHookUrl;
            TestSlashCommandGuildId = config.TestSlashCommandGuildId;
            RedisOption = config.RedisOption;
            UptimeKumaPushUrl = config.UptimeKumaPushUrl;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "設定檔讀取失敗");
            throw;
        }
    }

    private static string GenRandomKey()
    {
        var characters = "ABCDEF_GHIJKLMNOPQRSTUVWXYZ@abcdefghijklmnopqrstuvwx-yz0123456789";
        var Charsarr = new char[128];
        var random = new Random();

        for (int i = 0; i < Charsarr.Length; i++)
        {
            Charsarr[i] = characters[random.Next(characters.Length)];
        }

        var resultString = new string(Charsarr);
        return resultString;
    }
}
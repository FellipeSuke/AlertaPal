using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;

class Program
{
    static string baseDir;
    static string apiUrl;
    static string authUsername;
    static string authPassword;
    static string whatsappApiUrl;
    static string whatsappApiKey;
    static string ChatIdContact;
    static string discordWebhookUrl;
    static int toleranciaProxima;
    static string toleranciaProximaStr;
    static int toleranciaPerigo;
    static string toleranciaPerigoStr;
    static int tempoDeAtualizacao;
    static string tempoDeAtualizacaoStr;



    static async Task Main()
    {
        InitializeDirectoriesAndFiles();
        InitializeEnvironmentVariables();

        if (string.IsNullOrEmpty(toleranciaProximaStr) || !int.TryParse(toleranciaProximaStr, out toleranciaProxima))
        {
            toleranciaProxima = 7000;
        }
        if (string.IsNullOrEmpty(toleranciaPerigoStr) || !int.TryParse(toleranciaPerigoStr, out toleranciaPerigo))
        {
            toleranciaPerigo = 3500;
        }
        if (string.IsNullOrEmpty(tempoDeAtualizacaoStr) || !int.TryParse(tempoDeAtualizacaoStr, out tempoDeAtualizacao))
        {
            tempoDeAtualizacao = 10000;
        }

        while (true)
        {
            baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string logsDir = Path.Combine(baseDir, @"Dados\logs");
            string playersCsvFile = Path.Combine(baseDir, @"Dados\players_data.csv");
            string responseFile = Path.Combine(baseDir, @"Dados\response.json");


            Console.WriteLine($"################### {DateTime.Now} ###################");
            Console.WriteLine($"Tolerância definida em {toleranciaProxima} perigo em {toleranciaPerigo}");

            CleanTempFiles();

            string curlStatusFile = Path.Combine(baseDir, "Dados", "curl_status.txt");
            string curlOutputFile = Path.Combine(baseDir, "Dados", "curl_output.txt");

            if (!await MakeApiRequest(apiUrl, authUsername, authPassword, responseFile, curlStatusFile, curlOutputFile))
            {
                if (!File.Exists(Path.Combine(baseDir, "Dados", "statusServer.txt")))
                {
                    File.WriteAllText(Path.Combine(baseDir, "Dados", "statusServer.txt"), "");
                }
                string content = File.ReadAllText(Path.Combine(baseDir, "Dados", "statusServer.txt"));

                if (content != "Servidor Offline")
                {
                    File.WriteAllText(Path.Combine(baseDir, "Dados", "statusServer.txt"), "Servidor Offline");
                    Console.WriteLine("Servidor Offline");

                    ProcessJsonAndUpdateCsv(playersCsvFile, responseFile);

                    SendDiscordNotification("Servidor Offline", "16711680"); //Color red
                    await SendWhatsAppNotification("SERVIDOR DOWN, SOLICITANDO REINICIO");
                }
                Console.WriteLine($"Falha na requisição à API. Detalhes no arquivo: {Path.Combine(logsDir, "logs.txt")}");

                continue;
            }
            else
            {
                if (!File.Exists(Path.Combine(baseDir, "Dados", "statusServer.txt")))
                {
                    File.WriteAllText(Path.Combine(baseDir, "Dados", "statusServer.txt"), "");
                }

                string content = File.ReadAllText(Path.Combine(baseDir, "Dados", "statusServer.txt"));

                if (content != "Servidor Online")
                {
                    File.WriteAllText(Path.Combine(baseDir, "Dados", "statusServer.txt"), "Servidor Online");
                    Console.WriteLine("Servidor Online");
                    SendDiscordNotification("Servidor Online", "5763719"); //Color green
                    await SendWhatsAppNotification("Servidor ONLINE. RR com SUCESSO");
                }
                Console.WriteLine("Requisição a API concluída com sucesso.");
            }

            if (ProcessJsonAndUpdateCsv(playersCsvFile, responseFile) == false)
            {
                Thread.Sleep(10000);
                Console.WriteLine("Player sem ID");
                continue;
            }

            string directoryPath = Path.Combine(baseDir, "Dados", "Guildas");
            CheckCoordinatesInTextFiles(playersCsvFile, directoryPath);

            Console.WriteLine("Verificação de coordenadas concluída.");
            Thread.Sleep(tempoDeAtualizacao);
        }
    }

    static void InitializeEnvironmentVariables()
    {
        apiUrl = Environment.GetEnvironmentVariable("API_URL") ?? "http://192.168.100.73:8212/v1/api/players";
        toleranciaProximaStr = Environment.GetEnvironmentVariable("TOLERANCIA_PROXIMA");
        toleranciaPerigoStr = Environment.GetEnvironmentVariable("TOLERANCIA_PERIGO");
        authUsername = Environment.GetEnvironmentVariable("AUTH_USERNAME") ?? "admin";
        authPassword = Environment.GetEnvironmentVariable("AUTH_PASSWORD") ?? "unreal";
        whatsappApiUrl = Environment.GetEnvironmentVariable("WHATSAPP_API_URL") ?? "http://192.168.100.84:3000/client/sendMessage/suke";
        whatsappApiKey = Environment.GetEnvironmentVariable("WHATSAPP_API_KEY") ?? "SukeApiWhatsApp";
        ChatIdContact = Environment.GetEnvironmentVariable("CHAT_ID_CONTACT") ?? "120363315524671818@g.us";
        discordWebhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL") ?? "https://discord.com/api/webhooks/1261089127546355783/mKVdDzog3EUjLyvxPvwzDFX_-EqbzO4VWiCSc3RTQefADZl4Iz5kBGkFlEQIMVp6_jV_";
        tempoDeAtualizacaoStr = Environment.GetEnvironmentVariable("TEMPO_DE_ATUALIZACAO");
#if DEBUG
        Console.WriteLine("Discord em testes Internos Captain Hook");
        discordWebhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL") ?? "https://discord.com/api/webhooks/1262885751532552234/HBgF6Fkm76bsNXxsLr_SDfAhjgD_2hTEP7PUkF5eT7RX6nhEj57lGq3BllBZtvNvWnGo";
#endif
    }
    static void InitializeDirectoriesAndFiles()
    {
        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        string dadosPath = Path.Combine(basePath, "Dados");
        string logsPath = Path.Combine(dadosPath, "logs");
        string guildasPath = Path.Combine(dadosPath, "Guildas");
        string playersDataFile = Path.Combine(dadosPath, "players_data.csv");
        string responseFile = Path.Combine(dadosPath, "response.json");

        try
        {
            // Cria o diretório Dados se não existir
            if (!Directory.Exists(dadosPath))
            {
                Directory.CreateDirectory(dadosPath);
                Console.WriteLine($"Diretório criado: {dadosPath}");
            }

            // Cria o diretório logs se não existir
            if (!Directory.Exists(logsPath))
            {
                Directory.CreateDirectory(logsPath);
                Console.WriteLine($"Diretório criado: {logsPath}");
            }

            // Cria o diretório Guildas se não existir
            if (!Directory.Exists(guildasPath))
            {
                Directory.CreateDirectory(guildasPath);
                Console.WriteLine($"Diretório criado: {guildasPath}");
            }

            // Cria o arquivo players_data.csv se não existir
            if (!File.Exists(playersDataFile))
            {
                File.WriteAllText(playersDataFile, string.Empty);
                Console.WriteLine($"Arquivo criado: {playersDataFile}");
            }

            // Cria o arquivo response.json se não existir
            if (!File.Exists(responseFile))
            {
                File.WriteAllText(responseFile, string.Empty);
                Console.WriteLine($"Arquivo criado: {responseFile}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao criar diretórios ou arquivos: {ex.Message}");
        }
    }

    static void CleanTempFiles()
    {
        string[] tempFiles = { "curl_status.txt", "curl_output.txt", "response.json", "temp_error.json" };
        foreach (var file in tempFiles)
        {
            string filePath = Path.Combine(baseDir, "Dados", file);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    static async Task<bool> MakeApiRequest(string apiUrl, string username, string password, string responseFile, string curlStatusFile, string curlOutputFile)
    {
        try
        {
            using (var client = new HttpClient())
            {
                var authInfo = Convert.ToBase64String(Encoding.Default.GetBytes($"{username}:{password}"));
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authInfo);

                var response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    File.WriteAllText(responseFile, content);
                    return true;
                }
                else
                {
                    var statusCode = (int)response.StatusCode;
                    File.WriteAllText(curlStatusFile, statusCode.ToString());
                    var errorContent = await response.Content.ReadAsStringAsync();
                    File.AppendAllText(Path.Combine(baseDir, "Dados", "logs", "logs.txt"), $"Falha na requisição à API. Código de status: {statusCode}\nDetalhes do erro:\n{errorContent}\n");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            File.AppendAllText(Path.Combine(baseDir, "Dados", "logs", "logs.txt"), $"Erro na requisição à API: {ex.Message}\n");
            return false;
        }
    }

    public static bool ProcessJsonAndUpdateCsv(string playersCsvFile, string responseFile)
    {
        List<Player> existingPlayers = ReadPlayersFromCsv(playersCsvFile);
        List<Player> newPlayers = ParsePlayersFromJson(responseFile);

        List<Player> playersEntered = new List<Player>();
        List<Player> playersExited = new List<Player>();
        List<Player> playersLoading = new List<Player>();

        foreach (var newPlayer in newPlayers)
        {
            if (newPlayer.PlayerId == "None")
            {
                Console.WriteLine($"Account {newPlayer.AccountName} na tela de Loading");
                playersLoading.Add(newPlayer);
            }
            else
            {

                if (!existingPlayers.Any(p => p.AccountName == newPlayer.AccountName))
                {
                    playersEntered.Add(newPlayer);
                }
            }



        }

        foreach (var existingPlayer in existingPlayers)
        {
            if (!newPlayers.Any(p => p.AccountName == existingPlayer.AccountName))
            {
                playersExited.Add(existingPlayer);
            }
        }
        foreach (var PlayerLoad in playersLoading)
        {
            newPlayers.Remove(PlayerLoad);
        }

        UpdateCsvWithPlayers(playersCsvFile, newPlayers);

        Console.WriteLine("Jogadores que Entraram:");
        foreach (var player in playersEntered)
        {
            Console.WriteLine($"{player.Name}");
            SendDiscordNotification($"{player.Name} ({player.AccountName}) entrou no servidor", "1752220");
        }

        Console.WriteLine("Jogadores que Saíram:");
        foreach (var player in playersExited)
        {
            Console.WriteLine($"{player.Name}");
            SendDiscordNotification($"{player.Name} ({player.AccountName}) saiu do servidor", "10181046");
        }
        return true;
    }

    static List<Player> ReadPlayersFromCsv(string filePath)
    {
        List<Player> players = new List<Player>();
        if (!File.Exists(filePath))
            return players;

        var lines = File.ReadAllLines(filePath);
        foreach (var line in lines)
        {
            var cols = line.Split(',');
            if (cols.Length >= 4)
            {
                players.Add(new Player
                {
                    Name = cols[0],
                    AccountName = cols[1],
                    LocationX = int.Parse(cols[2]),
                    LocationY = int.Parse(cols[3])
                });
            }
        }
        return players;
    }

    static List<Player> ParsePlayersFromJson(string filePath)
    {
        List<Player> players = new List<Player>();
        string jsonContent;
        JObject json = null; // Inicializando com null

        try
        {
            jsonContent = File.ReadAllText(filePath);
            json = JObject.Parse(jsonContent);
        }
        catch
        {
            Console.WriteLine("Server OffLine, sem Json");
            return players; // Retorna uma lista vazia
        }

        // Verifica se o json foi corretamente inicializado
        if (json != null && json["players"] != null)
        {
            foreach (var player in json["players"])
            {
                players.Add(new Player
                {
                    Name = (string)player["name"],
                    AccountName = (string)player["accountName"],
                    PlayerId = (string)player["playerId"],
                    LocationX = (int)Math.Floor((double)player["location_x"]),
                    LocationY = (int)Math.Floor((double)player["location_y"])
                });
            }
        }
        return players;
    }

    static void UpdateCsvWithPlayers(string filePath, List<Player> players)
    {
        using (StreamWriter writer = new StreamWriter(filePath, false))
        {
            foreach (var player in players)
            {
                string line = $"{player.Name},{player.AccountName},{player.LocationX},{player.LocationY}";
                writer.WriteLine(line);
            }
        }
        Console.WriteLine("Arquivo CSV atualizado.");
    }

    static void CheckCoordinatesInTextFiles(string playersCsvFile, string directoryPath)
    {
        try
        {
            var players = ReadPlayersFromCsv(playersCsvFile);
            var textFiles = Directory.GetFiles(directoryPath, "*.txt");

            foreach (var file in textFiles)
            {
                var lines = File.ReadAllLines(file);
                foreach (var line in lines)
                {
                    var data = line.Split(',');
                    if (data.Length >= 3)
                    {
                        string fileName = data[0];
                        string fileAccountName = data[1];
                        int fileLocationX = int.Parse(data[2]);
                        int fileLocationY = int.Parse(data[3]);

                        foreach (var player in players)
                        {
                            if (player.AccountName != fileAccountName &&
                                Math.Abs(player.LocationX - fileLocationX) < toleranciaProxima &&
                                Math.Abs(player.LocationY - fileLocationY) < toleranciaProxima)
                            {
                                if (player.AccountName != fileAccountName &&
                                Math.Abs(player.LocationX - fileLocationX) < toleranciaPerigo &&
                                Math.Abs(player.LocationY - fileLocationY) < toleranciaPerigo)
                                {
                                    string message = $"⚔️ {player.Name} Invadiu a área dos {Path.GetFileNameWithoutExtension(file)}! ⚔️";
                                    Console.WriteLine(message);
                                    SendWhatsAppNotification(message);
                                }
                                else
                                {
                                    string message = $"⚠️ {player.Name} Proximo a área dos {Path.GetFileNameWithoutExtension(file)}! ⚠️";
                                    Console.WriteLine(message);
                                    SendWhatsAppNotification(message);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar coordenadas: {ex.Message}");
        }
    }

    static async Task SendDiscordNotification(string message, string color)
    {
        try
        {
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;

            var httpWebRequest = (HttpWebRequest)WebRequest.Create(discordWebhookUrl);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                string json = new JObject(
                    new JProperty("embeds", new JArray(
                        new JObject(
                            new JProperty("description", message),
                            new JProperty("color", color)
                        )
                    ))
                ).ToString();

                streamWriter.Write(json);
            }

            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                //Console.WriteLine(result);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao enviar notificação para o Discord: {ex.Message}");
        }
    }

    static async Task SendNotification(string message)
    {
        await SendWhatsAppNotification(message);
        SendDiscordNotification(message, "16711680"); // Red color
    }

    static async Task SendWhatsAppNotification(string message)
    {
        try
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, whatsappApiUrl);
                request.Headers.Add("x-api-key", whatsappApiKey);

                var content = new JObject(
                    new JProperty("chatId", ChatIdContact),
                    new JProperty("contentType", "string"),
                    new JProperty("content", message)
                ).ToString();

                request.Content = new StringContent(content, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                Console.WriteLine(await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao enviar notificação pelo WhatsApp: {ex.Message}");
        }
    }
}

class Player
{
    public string Name { get; set; }
    public string AccountName { get; set; }
    public string PlayerId { get; set; }
    public int LocationX { get; set; }
    public int LocationY { get; set; }
}

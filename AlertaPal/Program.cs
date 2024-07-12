using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;

class Program
{

    static string baseDir;

    static void Main()
    {
    inicio:
        baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string logsDir = Path.Combine(baseDir, "logs");
        string playersCsvFile = Path.Combine(baseDir, "players_data.csv");
        string responseFile = Path.Combine(baseDir, "response.json");

        int tolerancia = 7000;
        Console.WriteLine($"################### {DateTime.Now} ###################");
        Console.WriteLine($"Tolerância definida em {tolerancia}");

        CleanTempFiles();

        string apiUrl = "http://192.168.100.73:8212/v1/api/players";
        string authHeader = "Basic YWRtaW46dW5yZWFs";
        string curlStatusFile = Path.Combine(baseDir, "curl_status.txt");
        string curlOutputFile = Path.Combine(baseDir, "curl_output.txt");

        if (!MakeApiRequest(apiUrl, authHeader, responseFile, curlStatusFile, curlOutputFile))
        {
            Console.WriteLine($"Falha na requisição à API. Detalhes no arquivo: {Path.Combine(logsDir, "logs.txt")}");
            SendNotification("SERVIDOR DOWN, SOLICITANDO REINICIO");
            return;
        }
        else
        {
            Console.WriteLine("Requisição a API concluída com sucesso.");
        }

        if (ProcessJsonAndUpdateCsv(playersCsvFile, responseFile) == false)
        {
            Thread.Sleep(10000);
            Console.WriteLine("Payer sem ID");
            goto inicio;
        }


        string directoryPath = Path.Combine(baseDir, "nome");
        CheckCoordinatesInTextFiles(playersCsvFile, directoryPath, tolerancia);

        Console.WriteLine("Verificação de coordenadas concluída.");
        Thread.Sleep(30000);
        goto inicio;
    }

    static void CleanTempFiles()
    {
        string[] tempFiles = { "curl_status.txt", "curl_output.txt", "response.json", "temp_error.json" };
        foreach (var file in tempFiles)
        {
            string filePath = Path.Combine(baseDir, file);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    static bool MakeApiRequest(string apiUrl, string authHeader, string responseFile, string curlStatusFile, string curlOutputFile)
    {
        try
        {
            WebClient client = new WebClient();
            client.Headers.Add("Authorization", authHeader);
            client.DownloadFile(apiUrl, responseFile);
            File.WriteAllText(curlOutputFile, client.ResponseHeaders["http_code"]);
            return true;
        }
        catch (WebException ex)
        {
            using (WebResponse response = ex.Response)
            {
                HttpWebResponse httpResponse = (HttpWebResponse)response;
                if (httpResponse != null)
                {
                    File.WriteAllText(curlStatusFile, ((int)httpResponse.StatusCode).ToString());
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        string text = reader.ReadToEnd();
                        File.AppendAllText(Path.Combine(baseDir, "logs", "logs.txt"), $"Falha na requisição à API. Código de status: {httpResponse.StatusCode}\nDetalhes do erro:\n{text}\n");
                    }
                }
            }
            return false;
        }
    }

    public static bool ProcessJsonAndUpdateCsv(string playersCsvFile, string responseFile)
    {
        List<Player> existingPlayers = ReadPlayersFromCsv(playersCsvFile);
        List<Player> newPlayers = ParsePlayersFromJson(responseFile);

        List<Player> playersEntered = new List<Player>();
        List<Player> playersExited = new List<Player>();



        foreach (var newPlayer in newPlayers)
        {
            if (newPlayer.PlayerId == "None")
            {
                Console.WriteLine("Acconut sem ID");
                return false;
            }

            if (!existingPlayers.Any(p => p.AccountName == newPlayer.AccountName))
            {
                playersEntered.Add(newPlayer);

            }
        }

        foreach (var existingPlayer in existingPlayers)
        {
            if (!newPlayers.Any(p => p.AccountName == existingPlayer.AccountName))
            {
                playersExited.Add(existingPlayer);
            }
        }



        UpdateCsvWithPlayers(playersCsvFile, newPlayers);

        Console.WriteLine("Jogadores que Entraram:");
        foreach (var player in playersEntered)
        {
            Console.WriteLine($"{player.Name}");
            SendDiscordNotification($"{player.Name} entrou no servidor", "5763719");
        }

        Console.WriteLine("Jogadores que Saíram:");
        foreach (var player in playersExited)
        {
            Console.WriteLine($"{player.Name}");
            SendDiscordNotification($"{player.Name} Saiu do servidor", "16411130");
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
        string jsonContent = File.ReadAllText(filePath);
        JObject json = JObject.Parse(jsonContent);

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

    static void CheckCoordinatesInTextFiles(string playersCsvFile, string directoryPath, int tolerancia)
    {
        try
        {
            var players = File.ReadAllLines(playersCsvFile)
                              .Select(line => line.Split(','))
                              .Select(cols => new Player
                              {
                                  Name = cols[0],
                                  AccountName = cols[1],
                                  LocationX = int.Parse(cols[2]),
                                  LocationY = int.Parse(cols[3])
                              })
                              .ToList();

            foreach (var file in Directory.GetFiles(directoryPath, "*.txt"))
            {
                var fileLines = File.ReadAllLines(file);
                foreach (var line in fileLines)
                {
                    var fileCoords = line.Split(',');
                    if (fileCoords.Length >= 4)
                    {
                        string fileName = fileCoords[0];
                        string fileAccountName = fileCoords[1];
                        int fileLocationX = int.Parse(fileCoords[2]);
                        int fileLocationY = int.Parse(fileCoords[3]);

                        foreach (var player in players)
                        {
                            int dx = Math.Abs(player.LocationX - fileLocationX);
                            int dy = Math.Abs(player.LocationY - fileLocationY);

                            if (dx <= tolerancia && dy <= tolerancia &&
                                (player.Name != fileName || player.AccountName != fileAccountName))
                            {
                                string msg = $"\"{player.Name} está invadindo a base de {fileName}\"";
                                SendNotification(msg);
                                Console.WriteLine("Notificação enviada para WhatsApp.");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao verificar coordenadas nos arquivos de texto: {ex.Message}");
        }
    }

    static void SendNotification(string message)
    {
        try
        {
            string apiUrl = "http://192.168.100.84:3000/client/sendMessage/suke";
            string apiKey = "SukeApiWhatsApp";
            string jsonBody = $"{{\"chatId\": \"120363315524671818@g.us\", \"contentType\": \"string\", \"content\": \"{message}\"}}";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "curl.exe",
                Arguments = $"--silent --location \"{apiUrl}\" --header \"Content-Type: application/json\" --header \"x-api-key: {apiKey}\" --data \"{jsonBody}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                Console.WriteLine("Notificação enviada para WhatsApp.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao enviar notificação para WhatsApp: {ex.Message}");
        }
    }

    static void SendDiscordNotification(string message, string discordColor)
    {
        try
        {
            string webhookUrl = "https://discord.com/api/webhooks/1261089127546355783/mKVdDzog3EUjLyvxPvwzDFX_-EqbzO4VWiCSc3RTQefADZl4Iz5kBGkFlEQIMVp6_jV_";
            string jsonBody = $"{{\\\"username\\\": \\\"Captain Hook\\\", \\\"embeds\\\": [{{\\\"description\\\": \\\"{message}\\\", \\\"color\\\": {discordColor}}}]}}";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "curl", //Linux
                //FileName = "curl.exe", //para windows
                Arguments = $"--location \"{webhookUrl}\" --header \"Content-Type: application/json\" --data \"{jsonBody}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine("Notificação enviada para o Discord com sucesso.");
                }
                else
                {
                    Console.WriteLine($"Erro ao enviar notificação para o Discord: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao enviar notificação para o Discord: {ex.Message}");
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

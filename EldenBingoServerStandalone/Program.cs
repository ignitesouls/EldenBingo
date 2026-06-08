using EldenBingoCommon;
using EldenBingoServer;
using InteractiveReadLine;
using Microsoft.Extensions.Configuration;
using Neto.Shared;

namespace EldenBingoServerStandalone
{
    internal static class Program
    {
        private const ConsoleColor DefaultColor = ConsoleColor.Gray;
        private const ConsoleColor StatusColor = ConsoleColor.DarkYellow;
        private const ConsoleColor InfoColor = ConsoleColor.Green;
        private const ConsoleColor ErrorColor = ConsoleColor.Red;
        private static bool _stopCalled = false;
        private static Server _server;
        private static Thread _keyboardListenThread;

        private static string _jsonFile;
        private static string _matchLogDirectory;

        private static bool _readInput;

        private static IDictionary<char, (string, Action)> _keyboardShortcuts = new Dictionary<char, (string, Action)>()
        {
            {'k', new("List keyboard commands", showShortcuts)},
            {'r', new("List all rooms", printRooms)},
            {'j', new("Print path to server data json", showJsonPath)},
            {'l', new("Toggle match logging", toggleLogging)},
            {'f', new("Analyze square frequency from match logs", analyzeFrequency)},
            {'m', new("Enable Maintenance mode", maintenanceMode)},
        };

        public static void Main(string[] args)
        {
            int port = BingoConstants.DefaultPort;
            bool analyzeRequested = args.Any(a => string.Equals(a, "--analyze", StringComparison.OrdinalIgnoreCase));
            var configArgs = args
                .Where(a => !string.Equals(a, "--analyze", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var config = new ConfigurationBuilder()
                .AddCommandLine(configArgs)
                .Build();

            if (config["simulate"] != null)
            {
                simulateBoards(config);
                return;
            }

            if (config["port"] != null)
            {
                if (!int.TryParse(config["port"], out port))
                {
                    output("Invalid port", ErrorColor);
                }
            }
            if (config["serverdata"] != null)
            {
                _jsonFile = config["serverdata"];
            }
            else
            {
                _jsonFile = Path.Combine(getApplicationDirectory(), "serverData.json");
            }
            _server = new Server(port, _jsonFile);
            if (config["matchlog"] != null)
            {
                _server.MatchLogging = true;
            }
            if (config["matchlogdir"] != null)
            {
                _matchLogDirectory = config["matchlogdir"];
            }
            else
            {
                _matchLogDirectory = getApplicationDirectory();
            }
            _server.MatchLogDirectory = _matchLogDirectory;

            if (analyzeRequested || config["analyze"] != null)
            {
                analyzeFrequency();
                return;
            }

            _server.OnError += server_OnError;
            _server.OnStatus += server_OnStatus;
            _server.Host();
            output("Press 'k' to list all keyboard commands", InfoColor);

            if (!Console.IsInputRedirected)
            {
                _readInput = true;
                _keyboardListenThread = new Thread(listenKeyBoardEvent);
                _keyboardListenThread.Start();
            }
            else
            {
                output("Running in headless mode (no keyboard input)", InfoColor);
            }

            var waitHandle = new ManualResetEvent(false);

            Console.CancelKeyPress += async (o, e) =>
            {
                e.Cancel = true;
                _stopCalled = true;
                output("Stopping server...", StatusColor);
                await _server.Stop();
                waitHandle.Set();
            };
            waitHandle.WaitOne();
        }

        private static string getApplicationDirectory()
        {
            string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appSpecificFolder = Path.Combine(appDataFolder, "EldenBingo");

            if (!Directory.Exists(appSpecificFolder))
            {
                Directory.CreateDirectory(appSpecificFolder);
            }
            return appSpecificFolder;
        }

        private static void log(string text)
        {
            var timestamp = DateTime.Now.ToString();
            File.AppendAllText("log.txt", $"[{timestamp}] {text}{Environment.NewLine}");
        }

        private static void output(string text, ConsoleColor foreColor = ConsoleColor.White, ConsoleColor backColor = ConsoleColor.Black)
        {
            Console.ForegroundColor = foreColor;
            Console.BackgroundColor = backColor;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
            Console.BackgroundColor = ConsoleColor.Black;
        }

        private static void listenKeyBoardEvent()
        {
            while (!_stopCalled)
            {
                if (_readInput && Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (_keyboardShortcuts.TryGetValue(key.KeyChar, out var item))
                    {
                        item.Item2();
                    }
                }
                Thread.Sleep(50);
            }
        }

        private static void showShortcuts()
        {
            output("---- Keyboard Commands ----", InfoColor);
            foreach (var kv in _keyboardShortcuts)
            {
                if (kv.Key == 'k')
                    continue;
                output($"{kv.Key}: {kv.Value.Item1}");
            }
            output("---------------------------", InfoColor);
        }

        private static void printRooms()
        {
            output("---- Current Rooms ----", InfoColor);
            foreach (var room in _server.Rooms)
            {
                output($"{room.Name}: {room.Users.Count} users | Last Activity: {room.LastActivity.ToShortDateString()} {room.LastActivity.ToShortTimeString()}", InfoColor);
                foreach (var client in room.Users)
                {
                    output($"\t{client.Nick}", DefaultColor);
                }
            }
            output("-----------------------", InfoColor);
        }

        private static void maintenanceMode()
        {
            try
            {
                output("Maintenance Mode", InfoColor);
                _readInput = false;
                output("Enter a message to send to all connected clients (Escape to cancel):", DefaultColor);
                ConsoleKeyInfo key;
                var config = ReadLineConfig.Basic;
                bool _cancelled = false;
                config.KeyBehaviors.Add(new InteractiveReadLine.KeyBehaviors.KeyId(ConsoleKey.Escape, false, false, false), (kbt) =>
                {
                    _cancelled = true;
                    kbt.Finish();
                });
                string message = ConsoleReadLine.ReadLine(config);
                if (_cancelled)
                {
                    Console.WriteLine();
                    output("Cancelled maintenance", InfoColor);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message = "Restarting soon due to maintenance";
                    }
                    _server.EnableMaintenanceMode(message);
                }
            }
            finally
            {
                _readInput = true;
            }
        }

        private static void toggleLogging()
        {
            _server.MatchLogging = !_server.MatchLogging;
            
            if (_server.MatchLogging)
            {
                output($"Match logging enabled: {_server.MatchLogDirectory}");
            }
            else
            {
                output("Match logging disabled");
            }
        }

        private static void analyzeFrequency()
        {
            try
            {
                var report = new MatchFrequencyAnalyzer().AnalyzeDirectory(_server.MatchLogDirectory);
                output($"---- Square Frequency ({report.MatchesAnalyzed} matches) ----", InfoColor);
                output($"{"Pop up",8} {"Used",8} {"Boards",8} {"Uses",8}  Square");

                foreach (var square in report.Squares)
                {
                    output(
                        $"{square.AppearanceProbability,7:P1} " +
                        $"{square.UseRate,7:P1} " +
                        $"{square.MatchesAppeared,8} " +
                        $"{square.Uses,8}  " +
                        square.Square,
                        DefaultColor);
                }

                if (report.SkippedFiles > 0)
                    output($"Skipped {report.SkippedFiles} non-match or invalid JSON file(s).", StatusColor);
                output("----------------------------------------", InfoColor);
            }
            catch (Exception ex)
            {
                output($"Frequency analysis failed: {ex.Message}", ErrorColor);
            }
        }

        private static void simulateBoards(IConfiguration config)
        {
            try
            {
                string jsonPath = config["simulate"]!;
                int boards = parsePositiveInt(config["boards"], 1000, "boards");
                int size = parsePositiveInt(config["size"], 5, "size");
                int categoryLimit = parseNonNegativeInt(config["categorylimit"], 0, "categorylimit");
                int seed = parseNonNegativeInt(config["seed"], 0, "seed");

                var report = new BoardFrequencySimulator().Simulate(
                    jsonPath,
                    boards,
                    size,
                    categoryLimit,
                    seed);

                output(
                    $"---- Simulated Square Frequency " +
                    $"({report.GeneratedBoards}/{report.RequestedBoards} boards, {report.BoardSize}x{report.BoardSize}) ----",
                    InfoColor);
                output($"{"Pop up",8} {"Boards",8} {"Slots",8}  Square");

                foreach (var square in report.Squares)
                {
                    output(
                        $"{square.AppearanceProbability,7:P1} " +
                        $"{square.BoardsContaining,8} " +
                        $"{square.Appearances,8}  " +
                        square.Square,
                        DefaultColor);
                }

                if (report.FailedBoards > 0)
                    output($"{report.FailedBoards} board(s) could not be generated.", StatusColor);
                output("----------------------------------------", InfoColor);
            }
            catch (Exception ex)
            {
                output($"Board simulation failed: {ex.Message}", ErrorColor);
            }
        }

        private static int parsePositiveInt(string? value, int defaultValue, string option)
        {
            if (value == null)
                return defaultValue;
            if (int.TryParse(value, out int result) && result > 0)
                return result;
            throw new ArgumentException($"--{option} must be a positive whole number.");
        }

        private static int parseNonNegativeInt(string? value, int defaultValue, string option)
        {
            if (value == null)
                return defaultValue;
            if (int.TryParse(value, out int result) && result >= 0)
                return result;
            throw new ArgumentException($"--{option} must be a non-negative whole number.");
        }

        private static void showJsonPath()
        {
            output("Json Path", InfoColor);
            var text = _jsonFile;
            try
            {
                if (File.Exists(_jsonFile))
                {
                    var info = new FileInfo(_jsonFile);
                    string[] sizes = { "Bytes", "KB", "MB", "GB", "TB", "PB", "EB" };
                    long len = info.Length;
                    int order = 0;

                    while (len >= 1024 && order < sizes.Length - 1)
                    {
                        order++;
                        len /= 1024;
                    }
                    text += $" ({len:0.##} {sizes[order]})";
                }
            } catch(Exception) {}
            output(text, DefaultColor);
        }

        private static void server_OnStatus(object? sender, StringEventArgs e)
        {
            output(e.Message, StatusColor);
        }

        private static void server_OnError(object? sender, StringEventArgs e)
        {
            var message = $"Error: {e.Message}";
            output(message, ErrorColor);
            try
            {
                log(message);
            }
            finally
            {
                Environment.Exit(0);
            }
        }
    }
}

using System.Diagnostics;

namespace MAGI.ApiGateway.Services;

/// <summary>
/// Управляет жизненным циклом Python-микросервисов как дочерних процессов.
/// Gateway запускает/останавливает процессы по запросу из AdminPanel.
/// </summary>
public class ProcessManager : IDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<ProcessManager> _logger;

    /// <summary>
    /// Запущенные процессы: ключ = serviceKey (Parser, Tagger, Publisher).
    /// </summary>
    private readonly Dictionary<string, ManagedProcess> _processes = new();
    private readonly object _lock = new();

    /// <summary>
    /// Маппинг serviceKey → относительный путь к скрипту (от ProjectRoot).
    /// </summary>
    private static readonly Dictionary<string, string> ServiceScripts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Parser"] = @"Parser\service.py",
        ["Tagger"] = @"FilenameTagger\service.py",
        ["Publisher"] = @"Auto-post\service.py",
    };

    /// <summary>Кэшированный полный путь к python.exe.</summary>
    private string? _pythonPath;

    public ProcessManager(IConfiguration config, ILogger<ProcessManager> logger)
    {
        _config = config;
        _logger = logger;
        _pythonPath = ResolvePythonPath();
        _logger.LogInformation("Python path resolved: {Path}", _pythonPath ?? "NOT FOUND");
    }

    /// <summary>
    /// Находит реальный путь к python.exe (не WindowsApps alias).
    /// </summary>
    private static string? ResolvePythonPath()
    {
        // 1. Из переменной окружения PYTHON_PATH (если задана)
        var envPath = Environment.GetEnvironmentVariable("MAGI_PYTHON_PATH");
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            return envPath;

        // 2. Стандартные пути установки Python на Windows
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programsDir = Path.Combine(localAppData, "Programs", "Python");

        if (Directory.Exists(programsDir))
        {
            // Ищем самую новую версию Python
            var pythonDirs = Directory.GetDirectories(programsDir, "Python*")
                .OrderByDescending(d => d)
                .ToList();

            foreach (var dir in pythonDirs)
            {
                var exe = Path.Combine(dir, "python.exe");
                if (File.Exists(exe))
                    return exe;
            }
        }

        // 3. Через where.exe (ищет в PATH, но фильтруем WindowsApps)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "python",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);

                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    // Пропускаем WindowsApps alias — он не работает как дочерний процесс
                    if (line.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (File.Exists(line))
                        return line;
                }
            }
        }
        catch { }

        // 4. Fallback — надеемся что python в PATH работает
        return "python";
    }

    /// <summary>
    /// Запускает Python-сервис, если он ещё не запущен.
    /// Возвращает true, если процесс запущен (или уже был запущен).
    /// </summary>
    public bool StartService(string serviceKey)
    {
        lock (_lock)
        {
            // Уже запущен и жив?
            if (_processes.TryGetValue(serviceKey, out var existing) && !existing.Process.HasExited)
            {
                _logger.LogInformation("Service {Key} is already running (PID {Pid})", serviceKey, existing.Process.Id);
                return true;
            }

            if (!ServiceScripts.TryGetValue(serviceKey, out var scriptRelPath))
            {
                _logger.LogError("Unknown service key: {Key}", serviceKey);
                return false;
            }

            var projectRoot = GetProjectRoot();
            var scriptPath = Path.Combine(projectRoot, scriptRelPath);

            if (!File.Exists(scriptPath))
            {
                _logger.LogError("Script not found: {Path}", scriptPath);
                return false;
            }

            var workingDir = Path.GetDirectoryName(scriptPath)!;

            try
            {
                var pythonExe = _pythonPath ?? "python";
                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{scriptPath}\"",
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Environment =
                    {
                        ["PYTHONIOENCODING"] = "utf-8",
                        ["PYTHONUNBUFFERED"] = "1",
                    }
                };

                var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogInformation("[{Service}] {Line}", serviceKey, e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logger.LogWarning("[{Service}] {Line}", serviceKey, e.Data);
                };
                process.Exited += (_, _) =>
                {
                    _logger.LogInformation("Service {Key} process exited", serviceKey);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _processes[serviceKey] = new ManagedProcess(process, DateTime.UtcNow);

                _logger.LogInformation("Started service {Key} (PID {Pid}) from {Script}",
                    serviceKey, process.Id, scriptPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start service {Key}", serviceKey);
                return false;
            }
        }
    }

    /// <summary>
    /// Останавливает Python-сервис (kill процесс).
    /// </summary>
    public bool StopService(string serviceKey)
    {
        lock (_lock)
        {
            if (!_processes.TryGetValue(serviceKey, out var managed))
                return true; // не был запущен = уже остановлен

            try
            {
                if (!managed.Process.HasExited)
                {
                    managed.Process.Kill(entireProcessTree: true);
                    managed.Process.WaitForExit(3000);
                    _logger.LogInformation("Stopped service {Key} (PID {Pid})", serviceKey, managed.Process.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping service {Key}", serviceKey);
            }
            finally
            {
                managed.Process.Dispose();
                _processes.Remove(serviceKey);
            }

            return true;
        }
    }

    /// <summary>
    /// Проверяет, жив ли процесс сервиса.
    /// </summary>
    public bool IsProcessRunning(string serviceKey)
    {
        lock (_lock)
        {
            return _processes.TryGetValue(serviceKey, out var m) && !m.Process.HasExited;
        }
    }

    /// <summary>
    /// Возвращает информацию обо всех управляемых процессах.
    /// </summary>
    public Dictionary<string, ProcessInfoDto> GetAllProcesses()
    {
        lock (_lock)
        {
            var result = new Dictionary<string, ProcessInfoDto>();
            foreach (var (key, managed) in _processes)
            {
                var alive = !managed.Process.HasExited;
                result[key] = new ProcessInfoDto
                {
                    ServiceKey = key,
                    Pid = alive ? managed.Process.Id : null,
                    IsRunning = alive,
                    StartedAt = managed.StartedAt,
                };
            }
            return result;
        }
    }

    private string GetProjectRoot()
    {
        // Поднимаемся от bin/Debug/net8.0/ к корню проекта MAGI
        var baseDir = AppContext.BaseDirectory;
        var root = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        return root;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var (key, managed) in _processes)
            {
                try
                {
                    if (!managed.Process.HasExited)
                        managed.Process.Kill(entireProcessTree: true);
                    managed.Process.Dispose();
                }
                catch { }
            }
            _processes.Clear();
        }
    }

    private sealed record ManagedProcess(Process Process, DateTime StartedAt);
}

public class ProcessInfoDto
{
    public string ServiceKey { get; set; } = "";
    public int? Pid { get; set; }
    public bool IsRunning { get; set; }
    public DateTime StartedAt { get; set; }
}

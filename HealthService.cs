using System.Text.Json;

public class HealthService
{
    private readonly object _lock = new();
    private DateTime _lastSuccessfulUpdate = DateTime.MinValue;
    private DateTime _lastUpdateAttempt = DateTime.MinValue;
    private string _currentIp = "unknown";
    private string _lastError = "";
    private bool _isHealthy = true;
    private DateTime _startTime = DateTime.UtcNow;

    public void UpdateSuccessful(string ipAddress)
    {
        lock (_lock)
        {
            _lastSuccessfulUpdate = DateTime.UtcNow;
            _currentIp = ipAddress;
            _lastError = "";
            _isHealthy = true;
        }
    }

    public void UpdateFailed(string error)
    {
        lock (_lock)
        {
            _lastUpdateAttempt = DateTime.UtcNow;
            _lastError = error;
            _isHealthy = false;
        }
    }

    public void UpdateAttempt()
    {
        lock (_lock)
        {
            _lastUpdateAttempt = DateTime.UtcNow;
        }
    }

    public HealthStatus GetStatus()
    {
        lock (_lock)
        {
            return new HealthStatus
            {
                IsHealthy = _isHealthy,
                StartTime = _startTime,
                LastSuccessfulUpdate = _lastSuccessfulUpdate,
                LastUpdateAttempt = _lastUpdateAttempt,
                CurrentIp = _currentIp,
                LastError = _lastError,
                Uptime = DateTime.UtcNow - _startTime
            };
        }
    }
}

public class HealthStatus
{
    public bool IsHealthy { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime LastSuccessfulUpdate { get; set; }
    public DateTime LastUpdateAttempt { get; set; }
    public string CurrentIp { get; set; } = "";
    public string LastError { get; set; } = "";
    public TimeSpan Uptime { get; set; }

    public int TimeSinceLastUpdateMinutes =>
        LastSuccessfulUpdate == DateTime.MinValue ? -1 :
        (int)(DateTime.UtcNow - LastSuccessfulUpdate).TotalMinutes;

    public int TimeSinceLastAttemptMinutes =>
        LastUpdateAttempt == DateTime.MinValue ? -1 :
        (int)(DateTime.UtcNow - LastUpdateAttempt).TotalMinutes;
}
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Linq;
using ArduinoFFBControlCenter.Helpers;
using ArduinoFFBControlCenter.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace ArduinoFFBControlCenter.Services;

public class DashboardHostService
{
    private readonly LoggerService _logger;
    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly DashboardLayoutService _layoutService;
    private readonly DashboardTelemetryService _telemetry;
    private readonly DeviceSettingsService _deviceSettings;
    private readonly ProfileService _profiles;
    private readonly CalibrationService _calibration;
    private readonly DeviceStateService _deviceState;
    private WebApplication? _app;
    private Task? _runTask;
    private string? _indexHtml;
    private string? _appJs;
    private string? _appCss;
    private readonly Dictionary<string, SessionInfo> _sessions = new();
    private readonly Dictionary<string, List<DateTime>> _rateLimits = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public DashboardHostState State { get; private set; } = new();
    public event Action<DashboardHostState>? StateChanged;

    public DashboardHostService(LoggerService logger,
        SettingsService settingsService,
        AppSettings settings,
        DashboardLayoutService layoutService,
        DashboardTelemetryService telemetry,
        DeviceSettingsService deviceSettings,
        ProfileService profiles,
        CalibrationService calibration,
        DeviceStateService deviceState)
    {
        _logger = logger;
        _settingsService = settingsService;
        _settings = settings;
        _layoutService = layoutService;
        _telemetry = telemetry;
        _deviceSettings = deviceSettings;
        _profiles = profiles;
        _calibration = calibration;
        _deviceState = deviceState;
    }

    public async Task StartAsync()
    {
        if (_app != null)
        {
            return;
        }

        var port = _settings.DashboardPort <= 0 ? 10500 : _settings.DashboardPort;
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        LoadAssets();

        app.MapGet("/", async ctx =>
        {
            if (!IsLanRequest(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(_indexHtml ?? "Dashboard not available");
        });

        app.MapGet("/app.js", async ctx =>
        {
            if (!IsLanRequest(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            ctx.Response.ContentType = "text/javascript";
            await ctx.Response.WriteAsync(_appJs ?? string.Empty);
        });

        app.MapGet("/app.css", async ctx =>
        {
            if (!IsLanRequest(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            ctx.Response.ContentType = "text/css";
            await ctx.Response.WriteAsync(_appCss ?? string.Empty);
        });

        app.MapGet("/api/state", ctx =>
        {
            if (!IsLanRequest(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            var state = BuildStatePayload(ctx);
            return ctx.Response.WriteAsJsonAsync(state);
        });

        app.MapGet("/api/layout", ctx =>
        {
            if (!IsLanRequest(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            var layout = _layoutService.Load();
            return ctx.Response.WriteAsJsonAsync(layout);
        });

        app.MapPost("/api/layout", async ctx =>
        {
            if (!IsLanRequest(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            if (_settings.DashboardRequirePin && !IsAuthorized(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            var layout = await JsonSerializer.DeserializeAsync<DashboardLayout>(ctx.Request.Body);
            if (layout == null)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            _layoutService.Save(layout);
            ctx.Response.StatusCode = StatusCodes.Status200OK;
        });

        app.MapGet("/api/profiles", ctx =>
        {
            if (!IsLanRequest(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            var list = _profiles.LoadProfiles().Select(p => p.Name).ToList();
            return ctx.Response.WriteAsJsonAsync(list);
        });

        app.MapPost("/api/auth", async ctx =>
        {
            if (!IsLanRequest(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            if (!_settings.DashboardRequirePin)
            {
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                return;
            }
            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            if (!doc.RootElement.TryGetProperty("pin", out var pinEl))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var pin = pinEl.GetString() ?? string.Empty;
            if (!string.Equals(pin, _settings.DashboardPin, StringComparison.Ordinal))
            {
                await Task.Delay(200);
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var session = CreateSession(ctx);
            ctx.Response.Cookies.Append("dash_session", session.Id, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict });
            await ctx.Response.WriteAsJsonAsync(new { token = session.Token });
        });

        app.MapPost("/api/control", async ctx =>
        {
            if (!IsLanRequest(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            if (!IsAuthorized(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
            if (IsRateLimited(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }

            if (_calibration.CurrentAssessment.NeedsCalibration && !_settings.DashboardAdvancedRemote)
            {
                ctx.Response.StatusCode = StatusCodes.Status409Conflict;
                await ctx.Response.WriteAsync("Calibration required.");
                return;
            }

            using var doc = await JsonDocument.ParseAsync(ctx.Request.Body);
            if (!doc.RootElement.TryGetProperty("action", out var actionEl))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var action = actionEl.GetString() ?? string.Empty;

            if (action == "tuning")
            {
                var cfg = _deviceSettings.CurrentConfig ?? new FfbConfig();
                if (doc.RootElement.TryGetProperty("strength", out var strengthEl))
                {
                    cfg.GeneralGain = ClampSafe(strengthEl.GetInt32(), 120);
                }
                if (doc.RootElement.TryGetProperty("damping", out var dampingEl))
                {
                    cfg.DamperGain = ClampSafe(dampingEl.GetInt32(), 120);
                }
                if (doc.RootElement.TryGetProperty("friction", out var frictionEl))
                {
                    cfg.FrictionGain = ClampSafe(frictionEl.GetInt32(), 120);
                }
                if (doc.RootElement.TryGetProperty("inertia", out var inertiaEl))
                {
                    cfg.InertiaGain = ClampSafe(inertiaEl.GetInt32(), 120);
                }

                await _deviceSettings.ApplyConfigAsync(cfg, CancellationToken.None);
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                return;
            }

            if (action == "profile")
            {
                if (!doc.RootElement.TryGetProperty("name", out var nameEl))
                {
                    ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
                var name = nameEl.GetString() ?? string.Empty;
                var profile = _profiles.LoadProfileByName(name);
                if (profile == null)
                {
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                await _deviceSettings.ApplyConfigAsync(profile.Config, CancellationToken.None);
                _deviceSettings.SaveToPc(profile);
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                return;
            }

            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        });

        app.MapGet("/api/telemetry", async ctx =>
        {
            if (!IsLanRequest(ctx))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");

            while (!ctx.RequestAborted.IsCancellationRequested)
            {
                var frame = _telemetry.GetSnapshot();
                var json = JsonSerializer.Serialize(frame, _jsonOptions);
                await ctx.Response.WriteAsync($"data: {json}\n\n");
                await ctx.Response.Body.FlushAsync();
                await Task.Delay(33, ctx.RequestAborted);
            }
        });

        _app = app;
        try
        {
            await app.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.Error($"Dashboard host failed to start: {ex.Message}");
            _app = null;
            throw;
        }
        _runTask = app.WaitForShutdownAsync();
        UpdateState(true);
    }

    public async Task StopAsync()
    {
        if (_app == null)
        {
            return;
        }

        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
        _runTask = null;
        UpdateState(false);
    }

    public void ReloadAssets()
    {
        LoadAssets();
    }

    private void LoadAssets()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Assets", "Dashboard");
        _indexHtml = File.Exists(Path.Combine(root, "index.html")) ? File.ReadAllText(Path.Combine(root, "index.html")) : "";
        _appJs = File.Exists(Path.Combine(root, "app.js")) ? File.ReadAllText(Path.Combine(root, "app.js")) : "";
        _appCss = File.Exists(Path.Combine(root, "app.css")) ? File.ReadAllText(Path.Combine(root, "app.css")) : "";
    }

    private bool IsLanRequest(HttpContext ctx)
    {
        var remote = ctx.Connection.RemoteIpAddress;
        if (remote == null)
        {
            return false;
        }
        if (IPAddress.IsLoopback(remote))
        {
            return true;
        }

        var localIps = GetLocalIpv4();
        if (remote.AddressFamily == AddressFamily.InterNetwork)
        {
            var remoteBytes = remote.GetAddressBytes();
            return localIps.Any(ip =>
            {
                var localBytes = ip.GetAddressBytes();
                return localBytes[0] == remoteBytes[0] && localBytes[1] == remoteBytes[1] && localBytes[2] == remoteBytes[2];
            });
        }

        return false;
    }

    private object BuildStatePayload(HttpContext ctx)
    {
        var hostState = State;
        var authorized = !_settings.DashboardRequirePin || IsAuthorized(ctx);
        var current = _deviceSettings.CurrentConfig;
        var calibrationText = _calibration.CurrentAssessment.IsSupported
            ? _calibration.CurrentAssessment.NeedsCalibration ? "Not calibrated" : "Calibrated"
            : "Unknown";
        var maxStrength = _settings.KidMode ? 80 : 120;
        return new
        {
            hostState.IsRunning,
            hostState.Port,
            hostState.PrimaryAddress,
            hostState.Urls,
            RequirePin = _settings.DashboardRequirePin,
            Authorized = authorized,
            AdvancedRemote = _settings.DashboardAdvancedRemote,
            Calibration = calibrationText,
            SaveStatus = _deviceSettings.PersistenceState.ToString(),
            Connection = _deviceState.CurrentDevice != null ? "Connected" : "Disconnected",
            SafeCaps = new { Strength = maxStrength, Damping = 120, Friction = 120, Inertia = 120 },
            CurrentTuning = new
            {
                Strength = current?.GeneralGain ?? 0,
                Damping = current?.DamperGain ?? 0,
                Friction = current?.FrictionGain ?? 0,
                Inertia = current?.InertiaGain ?? 0
            }
        };
    }

    private bool IsAuthorized(HttpContext ctx)
    {
        if (!_settings.DashboardRequirePin)
        {
            return true;
        }
        if (!ctx.Request.Cookies.TryGetValue("dash_session", out var sessionId))
        {
            return false;
        }
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return false;
        }
        if (session.ExpiresUtc < DateTime.UtcNow)
        {
            _sessions.Remove(sessionId);
            return false;
        }
        var csrf = ctx.Request.Headers["X-CSRF-Token"].ToString();
        return !string.IsNullOrWhiteSpace(csrf) && csrf == session.Token;
    }

    private SessionInfo CreateSession(HttpContext ctx)
    {
        var session = new SessionInfo
        {
            Id = Guid.NewGuid().ToString("N"),
            Token = Guid.NewGuid().ToString("N"),
            ExpiresUtc = DateTime.UtcNow.AddHours(6)
        };
        _sessions[session.Id] = session;
        return session;
    }

    private bool IsRateLimited(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (!_rateLimits.TryGetValue(ip, out var list))
        {
            list = new List<DateTime>();
            _rateLimits[ip] = list;
        }
        var now = DateTime.UtcNow;
        list.RemoveAll(t => (now - t).TotalSeconds > 5);
        if (list.Count >= 10)
        {
            return true;
        }
        list.Add(now);
        return false;
    }

    private void UpdateState(bool running)
    {
        var port = _settings.DashboardPort <= 0 ? 10500 : _settings.DashboardPort;
        var ips = GetLocalIpv4();
        var urls = ips.Select(ip => $"http://{ip}:{port}").ToList();
        State = new DashboardHostState
        {
            IsRunning = running,
            Port = port,
            PrimaryAddress = ips.FirstOrDefault()?.ToString(),
            Urls = urls,
            RequirePin = _settings.DashboardRequirePin,
            AdvancedRemote = _settings.DashboardAdvancedRemote
        };
        StateChanged?.Invoke(State);
    }

    private static List<IPAddress> GetLocalIpv4()
    {
        var list = new List<IPAddress>();
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }
            var ipProps = ni.GetIPProperties();
            foreach (var addr in ipProps.UnicastAddresses)
            {
                if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    list.Add(addr.Address);
                }
            }
        }
        return list;
    }

    private static int ClampSafe(int value, int max)
    {
        return Math.Clamp(value, 0, max);
    }

    private class SessionInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresUtc { get; set; }
    }
}



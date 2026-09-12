using Microsoft.Extensions.Logging;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using DotNet.Testcontainers.Networks;

namespace TcfOss.DatabaseManager.Core.IntegrationTests.FakeDatabaseTestContainer;

#pragma warning disable

public sealed class FakeDbContainer(FakeDbConfiguration configuration) : IDatabaseContainer
{
    // ReSharper disable once UnassignedGetOnlyAutoProperty
    public DateTime CreatedTime { get; }
    public DateTime StartedTime { get; private set; }
    public DateTime StoppedTime { get; private set; }
    public DateTime PausedTime { get; private set; }
    public DateTime UnpausedTime { get; private set; }

    public ILogger Logger => configuration.Logger;
    public string Id => _container.ID;
    public string Name => _container.Name;
    public string IpAddress => _container.NetworkSettings.Networks.First().Value.IPAddress;
    public string MacAddress => _container.NetworkSettings.Networks.First().Value.MacAddress;

    public string Hostname
    {
        get
        {
            if (!string.IsNullOrEmpty(TestcontainersSettings.DockerHostOverride))
            {
                return TestcontainersSettings.DockerHostOverride;
            }

            if (configuration.DockerEndpointAuthConfig == null)
            {
                return "localhost";
            }

            var dockerEndpoint = configuration.DockerEndpointAuthConfig.Endpoint;

            switch (dockerEndpoint.Scheme)
            {
                case "tcp":
                case "http":
                case "https":
                    return dockerEndpoint.Host;
                case "unix":
                case "npipe":
                    return "localhost";
                default:
                    throw new NotSupportedException($"The Docker endpoint '{dockerEndpoint}' is not supported.");
            }
        }
    }

    public IImage Image => configuration.Image;

    public TestcontainersStates State
    {
        get
        {
            if (Enum.TryParse<TestcontainersStates>(_container.State?.Status, out var state))
            {
                return state;
            }
            return TestcontainersStates.Undefined;
        }
    }

    public TestcontainersHealthStatus Health
    {
        get
        {
            if (Enum.TryParse<TestcontainersHealthStatus>(_container.State?.Health?.Status, out var health))
            {
                return health;
            }
            if (_container is { State.Health: null })
            {
                return TestcontainersHealthStatus.None;
            }
            return TestcontainersHealthStatus.Undefined;
        }
    }

    public long HealthCheckFailingStreak => 0;

    public event EventHandler? Creating;
    public event EventHandler? Starting;
    public event EventHandler? Stopping;
    public event EventHandler? Pausing;
    public event EventHandler? Unpausing;
    public event EventHandler? Created;
    public event EventHandler? Started;
    public event EventHandler? Stopped;
    public event EventHandler? Paused;
    public event EventHandler? Unpaused;

    private readonly ContainerInspectResponse _container = new();

    public Task ConnectAsync(string network, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task ConnectAsync(INetwork network, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task CopyAsync(byte[] fileContent, string filePath, uint uid = 0, uint gid = 0, UnixFileModes fileMode = UnixFileModes.OtherRead | UnixFileModes.GroupRead | UnixFileModes.UserWrite | UnixFileModes.UserRead, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task CopyAsync(string source, string target, uint uid = 0, uint gid = 0, UnixFileModes fileMode = UnixFileModes.OtherRead | UnixFileModes.GroupRead | UnixFileModes.UserWrite | UnixFileModes.UserRead, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task CopyAsync(DirectoryInfo source, string target, uint uid = 0, uint gid = 0, UnixFileModes fileMode = UnixFileModes.OtherRead | UnixFileModes.GroupRead | UnixFileModes.UserWrite | UnixFileModes.UserRead, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task CopyAsync(FileInfo source, string target, uint uid = 0, uint gid = 0, UnixFileModes fileMode = UnixFileModes.OtherRead | UnixFileModes.GroupRead | UnixFileModes.UserWrite | UnixFileModes.UserRead, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public Task<ExecResult> ExecAsync(IList<string> command, CancellationToken ct = default)
    {
        return Task.FromResult(new ExecResult("Executed.", "", 0));
    }

    public string GetConnectionString()
    {
        var properties = new Dictionary<string, string>
        {
            { "Server", Hostname },
            { "Port", GetMappedPublicPort(FakeDbBuilder.FakeDbPort).ToString() },
            { "Database", configuration.Database },
            { "Uid", configuration.Username },
            { "Pwd", configuration.Password }
        };
        return string.Join(";", properties.Select(property => string.Join("=", property.Key, property.Value)));
    }

    public string GetConnectionString(ConnectionMode connectionMode = ConnectionMode.Host)
    {
        return GetConnectionString();
    }

    public string GetConnectionString(string name, ConnectionMode connectionMode = ConnectionMode.Host)
    {
        return GetConnectionString(connectionMode);
    }

    public Task<long> GetExitCodeAsync(CancellationToken ct = default)
    {
        return Task.FromResult(0L);
    }

    public Task<(string Stdout, string Stderr)> GetLogsAsync(DateTime since = default, DateTime until = default, bool timestampsEnabled = true, CancellationToken ct = default)
    {
        return Task.FromResult(("", ""));
    }

    public ushort GetMappedPublicPort()
    {
        return 43001;
    }

    public ushort GetMappedPublicPort(int containerPort)
    {
        return 43001;
    }

    public ushort GetMappedPublicPort(string containerPort)
    {
        return 43001;
    }

    public IReadOnlyDictionary<ushort, ushort> GetMappedPublicPorts()
    {
        throw new NotImplementedException();
    }

    public Task PauseAsync(CancellationToken ct = default)
    {
        PausedTime = DateTime.UtcNow;
        Paused?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        StartedTime = DateTime.UtcNow;
        Started?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        StoppedTime = DateTime.UtcNow;
        Stopped?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task UnpauseAsync(CancellationToken ct = default)
    {
        UnpausedTime = DateTime.UtcNow;
        Unpaused?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task<byte[]> ReadFileAsync(string filePath, CancellationToken ct = default)
    {
        return Task.FromResult(Array.Empty<byte>());
    }
}

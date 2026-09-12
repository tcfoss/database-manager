namespace TcfOss.DatabaseManager.Core.Configuration.Parsing;

public class Credentials
{
    public required string Hostname { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? SocketPath { get; set; }
    public string? Port { get; set; }

    public int? ConnectionTimeout { get; set; }

    // MySQL/MariaDB-specific
    public string? DefaultCharset { get; set; }

    // SQL Server-specific
    public string? Instance { get; set; }
    public bool? Encrypt { get; set; }
    public bool? TrustServerCertificate { get; set; }
}

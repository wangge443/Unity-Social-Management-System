using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SocialSystem.Api.Data;

// Applies to every opened (including pooled) EF connection.
public sealed class UtcConnectionInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SET SESSION time_zone = '+00:00'";
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection,
        ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SET SESSION time_zone = '+00:00'";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}


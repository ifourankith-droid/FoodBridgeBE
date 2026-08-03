using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FoodBridge.Api.Common;

/// <summary>
/// Serialises every <see cref="DateTime"/> as an explicit UTC instant (<c>…Z</c>), and reads any
/// offset-tagged input back as UTC.
/// <para>
/// <b>The bug this fixes.</b> Every timestamp in this API is UTC by convention (the <c>…Utc</c> suffix
/// on each field), but Dapper hands back <c>datetime2</c> columns as
/// <see cref="DateTimeKind.Unspecified"/> — SQL Server stores no zone. System.Text.Json writes an
/// Unspecified value with <b>no suffix at all</b>, e.g. <c>"2026-08-03T11:00:00"</c>. Per ECMA-262 a
/// date-time string without an offset is parsed as <i>local</i> time, so an IST browser read that back
/// as 11:00 IST — 5½ hours earlier than the instant meant. Deadlines displayed 5½ hours early and
/// anything due within 5½ hours looked already expired.
/// </para>
/// <para>
/// It only showed on reads: a value that arrived through JSON (a POST body) already carried a Kind, so
/// it round-tripped with an offset and looked fine, which is why create responses were correct while
/// the list and detail endpoints were not.
/// </para>
/// <para>
/// Registered globally in <c>Program.cs</c> rather than fixed per mapper: there are dozens of
/// <c>…Utc</c> fields across listings, timelines, certificates, notifications and dashboards, and
/// stamping the Kind at each one is precisely the kind of drift that leaves a straggler behind.
/// </para>
/// </summary>
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    /// <summary>
    /// Both styles together give exactly the semantics this API wants, without inspecting the text for
    /// a suffix: <c>AssumeUniversal</c> reads an offset-less value as UTC (the <c>…Utc</c> convention),
    /// and <c>AdjustToUniversal</c> converts one that *does* carry an offset. The result is always
    /// <see cref="DateTimeKind.Utc"/>.
    /// <para>
    /// Note this cannot be done with the reader's own <c>TryGetDateTimeOffset</c>: for a string with no
    /// offset it succeeds by applying the <b>server's</b> local offset, which silently shifts the
    /// instant and makes the result depend on where the app happens to be hosted.
    /// </para>
    /// </summary>
    private const DateTimeStyles ReadStyles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new JsonException("Expected an ISO 8601 date-time string.");
        }

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, ReadStyles, out var parsed))
        {
            throw new JsonException($"'{text}' is not a valid ISO 8601 date-time.");
        }

        return parsed;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            // Straight from the database: UTC by convention, just missing its Kind.
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => value,
        };

        // "O" on a Utc DateTime always ends in Z — the unambiguous form every client parses alike.
        writer.WriteStringValue(utc.ToString("O", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Nullable companion to <see cref="UtcDateTimeConverter"/>. Needed because System.Text.Json does not
/// unwrap <c>DateTime?</c> onto a <c>JsonConverter&lt;DateTime&gt;</c> — without it, nullable fields
/// such as <c>preparedAtUtc</c> and <c>estimatedPickupAtUtc</c> would keep the broken bare format.
/// </summary>
public sealed class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly UtcDateTimeConverter Inner = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : Inner.Read(ref reader, typeof(DateTime), options);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        Inner.Write(writer, value.Value, options);
    }
}

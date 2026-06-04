namespace DueMap.Integrations.Notices;

/// <summary>
/// Provider-agnostic dispatch request. The caller has already rendered the
/// notice; this module's job is to hand it to the right provider and report
/// back the provider message id + initial status. For SMS the
/// <see cref="BodyText"/> is sent; <see cref="BodyHtml"/> and
/// <see cref="Attachments"/> are ignored.
/// </summary>
public sealed record DispatchRequest(
    DispatchChannel Channel,
    string To,
    string? ToDisplayName,
    string Subject,
    string BodyHtml,
    string BodyText,
    IReadOnlyList<DispatchAttachment>? Attachments = null);

/// <summary>
/// File attached to an email dispatch. Bytes are held verbatim (no base64
/// pre-encoding) — the SendGrid sender wraps + encodes as needed. Total
/// attachment payload should stay under ~25 MB to clear most inbox limits.
/// </summary>
public sealed record DispatchAttachment(
    string FileName,
    string ContentType,
    ReadOnlyMemory<byte> Content);

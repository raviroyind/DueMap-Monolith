namespace DueMap.Notices.Domain;

/// <summary>
/// Immutable audit row: the exact rendered content of a notice that was sent to
/// a tenant. The columns are deliberately denormalized — what was sent stays
/// readable even if the source template is later revised or deleted. The
/// database enforces append-only via INSTEAD OF UPDATE/DELETE triggers; this
/// type should never be mutated after persistence.
/// </summary>
public sealed class NoticeDelivery
{
    public long Id { get; set; }
    public int PropertyManagerId { get; set; }
    public int LeaseId { get; set; }
    public int TemplateVersionId { get; set; }

    public string RenderedSubject { get; set; } = default!;
    public string RenderedBodyHtml { get; set; } = default!;
    public string RenderedBodyText { get; set; } = default!;

    public NoticeChannel Channel { get; set; }
    public DateTime SentAt { get; set; }
    public string? ProviderMsgId { get; set; }
    public DeliveryStatus Status { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? BouncedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>NULL = rendered from system template only; non-null = PM custom copy was used.</summary>
    public int? PmTemplateOverrideId { get; set; }
}

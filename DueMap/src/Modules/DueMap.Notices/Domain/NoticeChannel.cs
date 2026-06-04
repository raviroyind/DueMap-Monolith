namespace DueMap.Notices.Domain;

public enum NoticeChannel
{
    Email,
    Sms,
    CertifiedMail,
    InApp
}

public enum DeliveryStatus
{
    Queued,
    Sent,
    Delivered,
    Bounced,
    Failed
}

public enum TemplateVersionStatus
{
    Draft,
    Approved,
    Superseded
}

internal static class NoticeEnumMapping
{
    public static string ToWire(this NoticeChannel value) => value switch
    {
        NoticeChannel.Email          => "email",
        NoticeChannel.Sms            => "sms",
        NoticeChannel.CertifiedMail  => "certified_mail",
        NoticeChannel.InApp          => "in_app",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static NoticeChannel ChannelFromWire(string value) => value switch
    {
        "email"          => NoticeChannel.Email,
        "sms"            => NoticeChannel.Sms,
        "certified_mail" => NoticeChannel.CertifiedMail,
        "in_app"         => NoticeChannel.InApp,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown channel")
    };

    public static string ToWire(this DeliveryStatus value) => value switch
    {
        DeliveryStatus.Queued    => "queued",
        DeliveryStatus.Sent      => "sent",
        DeliveryStatus.Delivered => "delivered",
        DeliveryStatus.Bounced   => "bounced",
        DeliveryStatus.Failed    => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static DeliveryStatus DeliveryStatusFromWire(string value) => value switch
    {
        "queued"    => DeliveryStatus.Queued,
        "sent"      => DeliveryStatus.Sent,
        "delivered" => DeliveryStatus.Delivered,
        "bounced"   => DeliveryStatus.Bounced,
        "failed"    => DeliveryStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown delivery status")
    };

    public static string ToWire(this TemplateVersionStatus value) => value switch
    {
        TemplateVersionStatus.Draft      => "draft",
        TemplateVersionStatus.Approved   => "approved",
        TemplateVersionStatus.Superseded => "superseded",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static TemplateVersionStatus TemplateVersionStatusFromWire(string value) => value switch
    {
        "draft"      => TemplateVersionStatus.Draft,
        "approved"   => TemplateVersionStatus.Approved,
        "superseded" => TemplateVersionStatus.Superseded,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown template status")
    };
}

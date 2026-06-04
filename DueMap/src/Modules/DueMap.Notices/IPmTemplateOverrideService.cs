using DueMap.Notices.Domain;

namespace DueMap.Notices;

/// <summary>
/// CRUD for the PM's custom copy. Resolution at render time goes through
/// <see cref="INoticeTemplateService.ResolveRenderableAsync"/>; this service is
/// the PM admin surface.
/// </summary>
public interface IPmTemplateOverrideService
{
    Task<IReadOnlyList<PmTemplateOverride>> ListForPmAsync(int propertyManagerId, CancellationToken ct);

    Task<PmTemplateOverride?> GetAsync(int id, CancellationToken ct);

    Task<PmTemplateOverride> UpsertAsync(PmTemplateOverride row, CancellationToken ct);

    Task DeactivateAsync(int id, CancellationToken ct);
}

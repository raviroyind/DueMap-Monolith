namespace DueMap.Billing.Domain;

public sealed record AssessmentPlan(
    int LeaseId,
    DateOnly CurrentDueDate,
    DateOnly AssessmentDate,
    int DaysRelativeToDue,
    IReadOnlyList<PlannedAction> Actions)
{
    public bool IsEmpty => Actions.Count == 0;
}

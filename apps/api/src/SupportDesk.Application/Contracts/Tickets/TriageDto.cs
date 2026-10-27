namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>
/// Why a new ticket ended up the way it did: one plain sentence for each decision triage made.
/// </summary>
public sealed record TriageDto(string PriorityReason, string SlaReason, string AssignmentReason);

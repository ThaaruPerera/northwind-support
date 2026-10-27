namespace SupportDesk.Domain.Aggregates.Categories;

/// <summary>What choosing an agent needs to know about a ticket's category.</summary>
public sealed record CategoryRequirement(int CategoryId, string Name, bool RequiresSpecialist);

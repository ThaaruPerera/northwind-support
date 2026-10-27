using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Infrastructure.Data.Configurations;

/// <remarks>
/// The escalation history of a ticket. Rows are only ever inserted, so every relationship is
/// Restrict: history is never silently removed with the ticket or an agent it mentions.
/// </remarks>
public sealed class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.ToTable("TicketEscalations", t =>
        {
            t.HasCheckConstraint("CK_TicketEscalations_FromPriority", "[FromPriority] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_TicketEscalations_ToPriority", "[ToPriority] BETWEEN 1 AND 4");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromPriority).HasConversion<int>();

        builder.Property(e => e.ToPriority).HasConversion<int>();

        builder.Property(e => e.Reason).HasMaxLength(TicketEscalation.ReasonMaxLength).IsRequired();

        builder.Property(e => e.EscalatedBy).HasMaxLength(TicketEscalation.EscalatedByMaxLength).IsRequired();

        builder.Property(e => e.FromDueAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.ToDueAtUtc).HasColumnType("datetime2(3)");

        builder.Property(e => e.EscalatedAtUtc).HasColumnType("datetime2(3)");

        builder.HasOne<Ticket>()
            .WithMany()
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional: a ticket can have had no owner before, or have none afterwards.
        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.FromAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(e => e.ToAgentId)
            .OnDelete(DeleteBehavior.Restrict);

        // The history is always read for one ticket, newest first. This one index serves the
        // foreign key on TicketId and that ordering, so no separate TicketId index is needed.
        builder.HasIndex(e => new { e.TicketId, e.EscalatedAtUtc })
            .IsDescending(false, true)
            .HasDatabaseName("IX_TicketEscalations_TicketId_EscalatedAtUtc");
    }
}

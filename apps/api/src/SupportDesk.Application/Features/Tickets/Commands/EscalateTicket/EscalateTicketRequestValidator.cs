using FluentValidation;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

public sealed class EscalateTicketRequestValidator : AbstractValidator<EscalateTicketRequest>
{
    public EscalateTicketRequestValidator()
    {
        // The length that counts is the length of what will be stored, so padding does not help.
        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("A reason is required.")
            .Must(reason => reason.Trim().Length >= TicketEscalation.ReasonMinLength)
            .WithMessage($"The reason must be at least {TicketEscalation.ReasonMinLength} characters.")
            .Must(reason => reason.Trim().Length <= TicketEscalation.ReasonMaxLength)
            .WithMessage($"The reason must be at most {TicketEscalation.ReasonMaxLength} characters.");

        RuleFor(x => x.EscalatedBy)
            .Cascade(CascadeMode.Stop)
            .Must(actor => !string.IsNullOrWhiteSpace(actor))
            .WithMessage("Who is escalating the ticket is required.")
            .Must(actor => actor.Trim().Length <= TicketEscalation.EscalatedByMaxLength)
            .WithMessage($"The name must be at most {TicketEscalation.EscalatedByMaxLength} characters.");
    }
}

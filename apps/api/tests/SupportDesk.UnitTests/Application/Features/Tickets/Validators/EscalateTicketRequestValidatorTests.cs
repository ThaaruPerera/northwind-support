using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Validators;

public class EscalateTicketRequestValidatorTests
{
    private readonly EscalateTicketRequestValidator _validator = new();

    private bool IsValid(string? reason) =>
        _validator.Validate(new EscalateTicketRequest(reason!)).IsValid;

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(499)]
    [InlineData(500)]
    public void A_reason_from_five_to_five_hundred_characters_is_accepted(int length) =>
        Assert.True(IsValid(new string('a', length)));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(501)]
    [InlineData(2000)]
    public void A_reason_outside_that_range_is_rejected(int length) =>
        Assert.False(IsValid(new string('a', length)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("      ")]
    public void A_missing_or_blank_reason_is_rejected(string? reason) =>
        Assert.False(IsValid(reason));

    [Fact]
    public void Padding_does_not_make_a_short_reason_long_enough() =>
        Assert.False(IsValid("   abc   "));

    [Fact]
    public void Padding_does_not_make_a_long_reason_too_long() =>
        Assert.True(IsValid("  " + new string('a', 500) + "  "));

    [Fact]
    public void The_errors_name_the_field_that_failed()
    {
        var result = _validator.Validate(new EscalateTicketRequest("abc"));

        Assert.Contains(result.Errors, e => e.PropertyName == "Reason");
    }
}

using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Commands.Login;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    private bool IsValid(string? email, string? password) =>
        _validator.Validate(new LoginRequest(email!, password!)).IsValid;

    [Fact]
    public void An_email_and_a_password_are_accepted() => Assert.True(IsValid("alex@support.test", "x"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_email_is_rejected(string? email) => Assert.False(IsValid(email, "x"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_missing_password_is_rejected(string? password) => Assert.False(IsValid("alex@support.test", password));

    [Fact]
    public void Absurdly_long_input_is_rejected_before_it_reaches_the_hasher()
    {
        Assert.False(IsValid(new string('a', 257), "x"));
        Assert.False(IsValid("alex@support.test", new string('a', 129)));
    }
}

using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public class IdentityPasswordHasherTests
{
    private readonly IdentityPasswordHasher _hasher = new();

    [Fact]
    public void The_password_that_was_hashed_verifies()
    {
        var hash = _hasher.Hash("Correct_Horse_1");

        Assert.True(_hasher.Verify(hash, "Correct_Horse_1"));
    }

    [Theory]
    [InlineData("correct_horse_1")]
    [InlineData("Correct_Horse_2")]
    [InlineData("Correct_Horse_1 ")]
    [InlineData("")]
    public void Any_other_password_fails(string attempt)
    {
        var hash = _hasher.Hash("Correct_Horse_1");

        Assert.False(_hasher.Verify(hash, attempt));
    }

    [Fact]
    public void The_hash_is_not_the_password_and_does_not_contain_it()
    {
        var hash = _hasher.Hash("Correct_Horse_1");

        Assert.NotEqual("Correct_Horse_1", hash);
        Assert.DoesNotContain("Correct_Horse_1", hash);
    }

    [Fact]
    public void The_same_password_hashes_differently_every_time_because_of_the_salt()
    {
        var first = _hasher.Hash("Correct_Horse_1");
        var second = _hasher.Hash("Correct_Horse_1");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify(first, "Correct_Horse_1"));
        Assert.True(_hasher.Verify(second, "Correct_Horse_1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a hash at all")]
    [InlineData("Correct_Horse_1")]
    public void Something_that_is_not_a_hash_never_matches_and_never_throws(string notAHash)
    {
        Assert.False(_hasher.Verify(notAHash, "Correct_Horse_1"));
    }

    [Fact]
    public void A_hash_made_with_a_different_password_fails()
    {
        var someoneElses = _hasher.Hash("Another_Password_9");

        Assert.False(_hasher.Verify(someoneElses, "Correct_Horse_1"));
    }
}

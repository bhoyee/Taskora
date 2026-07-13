using TodoApp.Domain.Collaboration;
using TodoApp.Domain.Common;
using Xunit;

namespace TodoApp.Domain.Tests.Collaboration;

public sealed class UserProfileTests
{
    [Fact]
    public void Create_NormalizesIdentityDetails()
    {
        var profile = UserProfile.Create(
            Guid.NewGuid(),
            "  Jadesola Aliu ",
            " JADESOLA@example.com ");

        Assert.Equal("Jadesola Aliu", profile.DisplayName);
        Assert.Equal("jadesola@example.com", profile.Email);
    }

    [Fact]
    public void Create_WhenEmailIsInvalid_IsRejected()
    {
        Assert.Throws<DomainValidationException>(
            () => UserProfile.Create(
                Guid.NewGuid(),
                "Jadesola",
                "not-an-email"));
    }
}

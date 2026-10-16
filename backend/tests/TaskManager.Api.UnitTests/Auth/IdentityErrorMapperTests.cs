using Microsoft.AspNetCore.Identity;
using TaskManager.Api.Auth;

namespace TaskManager.Api.UnitTests.Auth;

/// <summary>Identity errors -> validation_failed keys / email_taken (SPEC FR-A3).</summary>
public sealed class IdentityErrorMapperTests
{
    private static readonly IdentityErrorDescriber Describer = new();

    [Fact]
    public void DuplicateUserNameOrEmail_MapsToEmailTaken()
    {
        Assert.True(IdentityErrorMapper.Map([Describer.DuplicateUserName("a@b.co")], "password").EmailTaken);
        Assert.True(IdentityErrorMapper.Map([Describer.DuplicateEmail("a@b.co")], "password").EmailTaken);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("newPassword")]
    public void PasswordRuleErrors_AreKeyedByTheGivenPasswordField(string field)
    {
        var mapping = IdentityErrorMapper.Map(
            [Describer.PasswordTooShort(8), Describer.PasswordRequiresDigit(), Describer.PasswordRequiresUniqueChars(1)],
            field);

        Assert.False(mapping.EmailTaken);
        var errors = mapping.Errors.ToDictionary();
        Assert.Equal([field], errors.Keys);
        Assert.Equal(3, errors[field].Length);
    }

    [Fact]
    public void PasswordMismatch_IsKeyedCurrentPassword()
    {
        var errors = IdentityErrorMapper.Map([Describer.PasswordMismatch()], "newPassword").Errors.ToDictionary();

        Assert.Equal([IdentityErrorMapper.CurrentPasswordIncorrectMessage], errors["currentPassword"]);
    }

    [Fact]
    public void InvalidEmailOrUserName_IsKeyedEmail()
    {
        var errors = IdentityErrorMapper.Map([Describer.InvalidEmail("x"), Describer.InvalidUserName("x")], "password").Errors.ToDictionary();

        Assert.Equal(["email"], errors.Keys);
    }

    [Fact]
    public void UnknownErrors_AreKeyedBody()
    {
        var errors = IdentityErrorMapper.Map([Describer.ConcurrencyFailure()], "password").Errors.ToDictionary();

        Assert.Equal(["body"], errors.Keys);
    }
}

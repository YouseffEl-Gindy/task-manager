using Microsoft.Extensions.Time.Testing;
using TaskManager.Api.Dtos.Account;
using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.Entities;
using TaskManager.Api.Validation;

namespace TaskManager.Api.UnitTests.Validation;

/// <summary>Field rules from API.md section 5, keyed by camelCase field names.</summary>
public sealed class RequestValidatorTests
{
    private const string GoodPassword = "correcthorse1";

    private static IReadOnlyDictionary<string, string[]> Errors(ValidationErrors errors) => errors.ToDictionary();

    // ---------- Register ----------

    [Fact]
    public void Register_Valid_HasNoErrors()
    {
        var errors = new RegisterRequestValidator().Validate(new RegisterRequest(" Ada Lovelace ", "  Ada@Example.com ", GoodPassword));
        Assert.True(errors.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Register_BlankDisplayName_IsKeyedDisplayName(string? displayName)
    {
        var errors = new RegisterRequestValidator().Validate(new RegisterRequest(displayName!, "a@b.co", GoodPassword));
        Assert.Equal(["displayName"], Errors(errors).Keys);
    }

    [Fact]
    public void Register_DisplayNameLength_IsCheckedAfterTrim()
    {
        var validator = new RegisterRequestValidator();
        Assert.True(validator.Validate(new RegisterRequest("  " + new string('a', 50) + "  ", "a@b.co", GoodPassword)).IsValid);
        Assert.True(validator.Validate(new RegisterRequest(new string('a', 51), "a@b.co", GoodPassword)).Has("displayName"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plainaddress")]
    [InlineData("a@b")]
    [InlineData("a b@c.de")]
    [InlineData("a@@b.de")]
    [InlineData(null)]
    public void Register_InvalidEmail_IsKeyedEmail(string? email)
    {
        var errors = new RegisterRequestValidator().Validate(new RegisterRequest("Ada", email!, GoodPassword));
        Assert.Equal(["email"], Errors(errors).Keys);
    }

    [Fact]
    public void Register_EmailOver254AfterTrim_IsRejected()
    {
        var email = new string('a', 250) + "@b.co"; // 255 chars
        Assert.True(new RegisterRequestValidator().Validate(new RegisterRequest("Ada", email, GoodPassword)).Has("email"));
    }

    [Theory]
    [InlineData("short1")]               // too short
    [InlineData("onlyletters")]          // no digit
    [InlineData("1234567890")]           // no letter
    [InlineData("")]
    [InlineData(null)]
    public void Register_PasswordBreakingARule_IsKeyedPassword(string? password)
    {
        var errors = new RegisterRequestValidator().Validate(new RegisterRequest("Ada", "a@b.co", password!));
        Assert.Equal(["password"], Errors(errors).Keys);
    }

    [Fact]
    public void Register_PasswordLengthBoundaries()
    {
        var validator = new RegisterRequestValidator();
        Assert.True(validator.Validate(new RegisterRequest("Ada", "a@b.co", "abcdefg1")).IsValid);
        Assert.True(validator.Validate(new RegisterRequest("Ada", "a@b.co", new string('a', 127) + "1")).IsValid);
        Assert.True(validator.Validate(new RegisterRequest("Ada", "a@b.co", new string('a', 128) + "1")).Has("password"));
    }

    [Fact]
    public void Register_PasswordIsNotTrimmed()
    {
        // 7 visible chars plus surrounding spaces: valid only because spaces count.
        Assert.True(new RegisterRequestValidator().Validate(new RegisterRequest("Ada", "a@b.co", " abcde1 ")).IsValid);
    }

    // ---------- Login ----------

    [Fact]
    public void Login_RequiresEmailAndPassword_NoComplexityCheck()
    {
        var validator = new LoginRequestValidator();
        Assert.True(validator.Validate(new LoginRequest("not-an-email", "x")).IsValid);

        var errors = Errors(validator.Validate(new LoginRequest("  ", "")));
        Assert.Contains("email", errors.Keys);
        Assert.Contains("password", errors.Keys);
        Assert.True(validator.Validate(new LoginRequest("a@b.co", new string('a', 129))).Has("password"));
    }

    // ---------- Account ----------

    [Fact]
    public void UpdateProfile_DisplayNameRules()
    {
        var validator = new UpdateProfileRequestValidator();
        Assert.True(validator.Validate(new UpdateProfileRequest(" Ada L. ")).IsValid);
        Assert.True(validator.Validate(new UpdateProfileRequest("   ")).Has("displayName"));
        Assert.True(validator.Validate(new UpdateProfileRequest(new string('x', 51))).Has("displayName"));
    }

    [Fact]
    public void ChangePassword_NewPasswordRules_AndMustDiffer()
    {
        var validator = new ChangePasswordRequestValidator();
        Assert.True(validator.Validate(new ChangePasswordRequest(GoodPassword, "batterystaple2")).IsValid);

        Assert.Equal(["newPassword"], Errors(validator.Validate(new ChangePasswordRequest(GoodPassword, "weak"))).Keys);

        var same = Errors(validator.Validate(new ChangePasswordRequest(GoodPassword, GoodPassword)));
        Assert.Equal(["newPassword"], same.Keys);
        Assert.Contains(ChangePasswordRequestValidator.MustDifferMessage, same["newPassword"]);

        Assert.Equal(["currentPassword"], Errors(validator.Validate(new ChangePasswordRequest("", "batterystaple2"))).Keys);
    }

    [Fact]
    public void DeleteAccount_PasswordRequired()
    {
        var validator = new DeleteAccountRequestValidator();
        Assert.True(validator.Validate(new DeleteAccountRequest("anything")).IsValid);
        Assert.True(validator.Validate(new DeleteAccountRequest("")).Has("password"));
    }

    // ---------- Tasks ----------

    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static CreateTaskRequestValidator CreateValidator() => new(new FakeTimeProvider(Now));

    [Fact]
    public void CreateTask_OnlyTitle_IsValid()
    {
        Assert.True(CreateValidator().Validate(new CreateTaskRequest("Buy milk", null, null, null, null)).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateTask_MissingOrBlankTitle_IsKeyedTitle(string? title)
    {
        Assert.Equal(["title"], Errors(CreateValidator().Validate(new CreateTaskRequest(title!, null, null, null, null))).Keys);
    }

    [Fact]
    public void CreateTask_LengthLimits()
    {
        var validator = CreateValidator();
        Assert.True(validator.Validate(new CreateTaskRequest(new string('t', 200), new string('d', 2000), null, null, null)).IsValid);
        Assert.True(validator.Validate(new CreateTaskRequest(new string('t', 201), null, null, null, null)).Has("title"));
        Assert.True(validator.Validate(new CreateTaskRequest("t", new string('d', 2001), null, null, null)).Has("description"));
        Assert.True(validator.Validate(new CreateTaskRequest("  t  ", "  " + new string('d', 2000) + "  ", null, null, null)).IsValid);
    }

    [Fact]
    public void CreateTask_DueDate_MustBeStrictlyInTheFuture()
    {
        var validator = CreateValidator();
        Assert.True(validator.Validate(new CreateTaskRequest("t", null, null, null, Now.AddHours(1))).IsValid);

        var equal = Errors(validator.Validate(new CreateTaskRequest("t", null, null, null, Now)));
        Assert.Equal([DueDateRules.MustBeInFutureMessage], equal["dueDate"]);

        // Truncation to whole seconds happens before the comparison.
        Assert.True(validator.Validate(new CreateTaskRequest("t", null, null, null, Now.AddMilliseconds(900))).Has("dueDate"));
        Assert.True(validator.Validate(new CreateTaskRequest("t", null, null, null, Now.AddDays(-1))).Has("dueDate"));
    }

    [Fact]
    public void CreateTask_ReportsAllFieldErrorsTogether()
    {
        var errors = Errors(CreateValidator().Validate(new CreateTaskRequest("", new string('d', 2001), null, null, Now.AddDays(-1))));
        Assert.Equal(["description", "dueDate", "title"], errors.Keys.Order());
    }

    [Fact]
    public void UpdateTask_ChecksTitleAndDescription_NotDueDate()
    {
        var validator = new UpdateTaskRequestValidator();
        var past = Now.AddDays(-10);
        Assert.True(validator.Validate(new UpdateTaskRequest("t", null, TaskItemStatus.Done, TaskItemPriority.Low, past)).IsValid);
        Assert.True(validator.Validate(new UpdateTaskRequest(" ", null, TaskItemStatus.Done, TaskItemPriority.Low, null)).Has("title"));
    }

    [Fact]
    public void TaskListQuery_SearchMax100AfterTrim()
    {
        var validator = new TaskListQueryValidator();
        Assert.True(validator.Validate(new TaskListQuery { Search = "  " + new string('s', 100) + "  " }).IsValid);
        Assert.True(validator.Validate(new TaskListQuery { Search = new string('s', 101) }).Has("search"));
        Assert.True(validator.Validate(new TaskListQuery()).IsValid);
    }
}

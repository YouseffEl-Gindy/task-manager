using TaskManager.Api.Validation;

namespace TaskManager.Api.UnitTests.Validation;

/// <summary>SPEC FR-T9 / FR-T10, AC-DUE-2/3/5/6/7.</summary>
public sealed class DueDateRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToUtcWholeSeconds_ConvertsOffsetToUtc()
    {
        var value = DueDateRules.ToUtcWholeSeconds(new DateTimeOffset(2026, 10, 12, 17, 0, 0, TimeSpan.FromHours(2)));

        Assert.Equal(new DateTime(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc), value);
        Assert.Equal(DateTimeKind.Utc, value!.Value.Kind);
    }

    [Fact]
    public void ToUtcWholeSeconds_TruncatesFractionalSeconds()
    {
        var input = new DateTimeOffset(2026, 10, 12, 15, 0, 0, 789, TimeSpan.Zero).AddTicks(1234);

        Assert.Equal(new DateTime(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc), DueDateRules.ToUtcWholeSeconds(input));
    }

    [Fact]
    public void ToUtcWholeSeconds_Null_IsNull()
    {
        Assert.Null(DueDateRules.ToUtcWholeSeconds(null));
    }

    [Fact]
    public void Create_AllowsNullAndFuture()
    {
        Assert.True(DueDateRules.IsAllowedOnCreate(null, Now));
        Assert.True(DueDateRules.IsAllowedOnCreate(Now.AddSeconds(1), Now));
        Assert.True(DueDateRules.IsAllowedOnCreate(Now.AddHours(1), Now));
    }

    [Fact]
    public void Create_RejectsPastAndEqualToNow()
    {
        Assert.False(DueDateRules.IsAllowedOnCreate(Now, Now));
        Assert.False(DueDateRules.IsAllowedOnCreate(Now.AddSeconds(-1), Now));
    }

    [Fact]
    public void Update_NullIsAlwaysAllowed()
    {
        Assert.True(DueDateRules.IsAllowedOnUpdate(null, Now.AddDays(-1), Now));
        Assert.True(DueDateRules.IsAllowedOnUpdate(null, null, Now));
    }

    [Fact]
    public void Update_SameStoredInstant_IsAllowedEvenWhenPast()
    {
        var past = Now.AddDays(-2);
        Assert.True(DueDateRules.IsAllowedOnUpdate(past, past, Now));
    }

    [Fact]
    public void Update_SameInstantSentWithDifferentOffset_CountsAsUnchanged()
    {
        var stored = new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc);
        var resent = DueDateRules.ToUtcWholeSeconds(new DateTimeOffset(2026, 10, 1, 17, 0, 0, TimeSpan.FromHours(2)));

        Assert.True(DueDateRules.IsAllowedOnUpdate(resent, stored, Now));
    }

    [Fact]
    public void Update_ChangingToADifferentPastInstant_IsRejected()
    {
        Assert.False(DueDateRules.IsAllowedOnUpdate(Now.AddDays(-1), Now.AddDays(-2), Now));
    }

    [Fact]
    public void Update_FromNullToPast_IsRejected()
    {
        Assert.False(DueDateRules.IsAllowedOnUpdate(Now.AddMinutes(-1), null, Now));
    }

    [Fact]
    public void Update_ToNow_IsRejected_ToFuture_IsAllowed()
    {
        Assert.False(DueDateRules.IsAllowedOnUpdate(Now, Now.AddDays(-1), Now));
        Assert.True(DueDateRules.IsAllowedOnUpdate(Now.AddMinutes(1), Now.AddDays(-1), Now));
        Assert.True(DueDateRules.IsAllowedOnUpdate(Now.AddMinutes(1), null, Now));
    }
}

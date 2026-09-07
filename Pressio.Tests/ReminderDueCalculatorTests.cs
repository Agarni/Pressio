using System;
using Pressio.Models;
using Pressio.Services;
using Xunit;

namespace Pressio.Tests;

public class ReminderDueCalculatorTests
{
    private static readonly DateTime Wed = new(2026, 9, 9, 8, 0, 0); // uma quarta-feira

    [Fact]
    public void IsDue_WhenDayAndTimeMatch() =>
        Assert.True(ReminderDueCalculator.IsDue(true, ReminderDays.Wednesday, new TimeSpan(8, 0, 5), Wed));

    [Fact]
    public void IsDue_False_WhenDisabled() =>
        Assert.False(ReminderDueCalculator.IsDue(false, ReminderDays.Wednesday, Wed.TimeOfDay, Wed));

    [Fact]
    public void IsDue_False_WhenDayDoesNotMatch() =>
        Assert.False(ReminderDueCalculator.IsDue(true, ReminderDays.Monday, Wed.TimeOfDay, Wed));

    [Fact]
    public void IsDue_False_WhenTimeTooFar() =>
        Assert.False(ReminderDueCalculator.IsDue(true, ReminderDays.Wednesday, new TimeSpan(8, 0, 0), Wed.AddMinutes(3)));

    [Fact]
    public void IsDue_False_WhenNoDays()
    {
        Assert.False(ReminderDueCalculator.IsDue(true, ReminderDays.None, Wed.TimeOfDay, Wed));
    }

    [Fact]
    public void IsDue_WithinOneMinuteWindow() =>
        Assert.True(ReminderDueCalculator.IsDue(true, ReminderDays.Wednesday, new TimeSpan(7, 59, 30), new DateTime(2026, 9, 9, 8, 0, 10)));

    [Theory]
    [InlineData(DayOfWeek.Sunday, ReminderDays.Sunday)]
    [InlineData(DayOfWeek.Monday, ReminderDays.Monday)]
    [InlineData(DayOfWeek.Saturday, ReminderDays.Saturday)]
    public void DayFlag_MapsCorrectly(DayOfWeek day, ReminderDays expected) =>
        Assert.Equal(expected, ReminderDueCalculator.DayFlag(day));
}

using ParishRota.Domain;

namespace ParishRota.Domain.Tests;

public class LiturgicalCalendarTests
{
    // Expected dates are taken from published liturgical calendars rather than
    // derived here — a test that recomputes Easter the way the code does could
    // only ever agree with itself.
    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]
    public void EasterSunday_is_the_published_date(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), LiturgicalCalendar.EasterSunday(year));
    }

    // The First Sunday of Advent opens the liturgical year, and with it the
    // first Rota Period. 2028 is the interesting one: Christmas falls on a
    // Monday, so the Fourth Sunday of Advent is Christmas Eve itself.
    [Theory]
    [InlineData(2024, 12, 1)]
    [InlineData(2025, 11, 30)]
    [InlineData(2026, 11, 29)]
    [InlineData(2027, 11, 28)]
    [InlineData(2028, 12, 3)]
    public void AdventSunday_is_the_published_date(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), LiturgicalCalendar.AdventSunday(year));
    }

    // Ash Wednesday opens the Lent & Eastertide Rota Period.
    [Theory]
    [InlineData(2024, 2, 14)]
    [InlineData(2025, 3, 5)]
    [InlineData(2026, 2, 18)]
    [InlineData(2027, 2, 10)]
    public void AshWednesday_is_the_published_date(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), LiturgicalCalendar.AshWednesday(year));
    }

    // Pentecost closes Eastertide. The long stretch of Ordinary Time that
    // follows is the one ADR 0004 says must be subdivided.
    [Theory]
    [InlineData(2024, 5, 19)]
    [InlineData(2025, 6, 8)]
    [InlineData(2026, 5, 24)]
    [InlineData(2027, 5, 16)]
    public void Pentecost_is_the_published_date(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), LiturgicalCalendar.Pentecost(year));
    }

    // Baptism of the Lord closes Christmastide. England & Wales transfers
    // Epiphany to the Sunday between 2 and 8 January, and when that lands on
    // 7 or 8 January the Baptism moves to the Monday straight after — so 2023
    // and 2024 fall on a Monday, the rest on a Sunday.
    [Theory]
    [InlineData(2023, 1, 9)]
    [InlineData(2024, 1, 8)]
    [InlineData(2025, 1, 12)]
    [InlineData(2026, 1, 11)]
    [InlineData(2028, 1, 9)]
    public void BaptismOfTheLord_is_the_published_date(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), LiturgicalCalendar.BaptismOfTheLord(year));
    }
}

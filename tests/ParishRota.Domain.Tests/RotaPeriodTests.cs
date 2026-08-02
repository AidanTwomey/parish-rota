using ParishRota.Domain;

namespace ParishRota.Domain.Tests;

/// <summary>
/// Rota Period boundaries (ADR 0004). Every expected date here is read off a
/// published liturgical calendar, never derived from the code under test.
/// </summary>
public class RotaPeriodTests
{
    [Fact]
    public void A_date_in_Advent_falls_in_Advent_and_Christmastide()
    {
        // The Third Sunday of Advent 2025. The period runs from Advent Sunday
        // (30 Nov 2025) to the Baptism of the Lord (11 Jan 2026).
        var period = LiturgicalCalendar.PeriodContaining(new DateOnly(2025, 12, 14));

        Assert.Equal("Advent & Christmastide", period.Name);
        Assert.Equal(new DateOnly(2025, 11, 30), period.Start);
        Assert.Equal(new DateOnly(2026, 1, 11), period.End);
    }

    [Fact]
    public void A_date_in_Christmastide_belongs_to_the_previous_years_Advent()
    {
        // The period straddles New Year, so 4 Jan 2026 belongs to the Rota that
        // started on 30 Nov 2025 — not to anything in 2026.
        var period = LiturgicalCalendar.PeriodContaining(new DateOnly(2026, 1, 4));

        Assert.Equal("Advent & Christmastide", period.Name);
        Assert.Equal(new DateOnly(2025, 11, 30), period.Start);
        Assert.Equal(new DateOnly(2026, 1, 11), period.End);
    }

    [Fact]
    public void A_date_in_Lent_falls_in_Lent_and_Eastertide()
    {
        // The First Sunday of Lent 2026. The period opens on Ash Wednesday
        // (18 Feb 2026) and closes at Pentecost (24 May 2026).
        var period = LiturgicalCalendar.PeriodContaining(new DateOnly(2026, 2, 22));

        Assert.Equal("Lent & Eastertide", period.Name);
        Assert.Equal(new DateOnly(2026, 2, 18), period.Start);
        Assert.Equal(new DateOnly(2026, 5, 24), period.End);
    }

    [Fact]
    public void A_date_in_Eastertide_falls_in_the_same_period_as_Lent()
    {
        // Easter Sunday 2026 itself — one Rota covers Lent straight through to
        // Pentecost, so this must not open a new period.
        var period = LiturgicalCalendar.PeriodContaining(new DateOnly(2026, 4, 5));

        Assert.Equal("Lent & Eastertide", period.Name);
        Assert.Equal(new DateOnly(2026, 2, 18), period.Start);
        Assert.Equal(new DateOnly(2026, 5, 24), period.End);
    }

    [Fact]
    public void A_date_between_Christmastide_and_Lent_falls_in_Ordinary_Time()
    {
        // Ordinary Time opens the day after the Baptism of the Lord (11 Jan
        // 2026) and closes the day before Ash Wednesday (18 Feb 2026). Five
        // weeks in 2026, so it needs no subdividing.
        var period = LiturgicalCalendar.PeriodContaining(new DateOnly(2026, 2, 1));

        Assert.Equal("Ordinary Time (January–February)", period.Name);
        Assert.Equal(new DateOnly(2026, 1, 12), period.Start);
        Assert.Equal(new DateOnly(2026, 2, 17), period.End);
    }

    // Ordinary Time after Pentecost 2026 runs 25 May to 28 Nov — 26 Sundays,
    // too long for one Rota (ADR 0004). Four blocks of 7, 7, 6, 6 Sundays, each
    // running Monday to Sunday so a Saturday vigil is never split from the
    // Sunday it belongs to. The last block runs on to the day before Advent.
    [Theory]
    [InlineData(2026, 6, 1, "Ordinary Time (May–July)", 2026, 5, 25, 2026, 7, 12)]
    [InlineData(2026, 8, 1, "Ordinary Time (July–August)", 2026, 7, 13, 2026, 8, 30)]
    [InlineData(2026, 9, 20, "Ordinary Time (August–October)", 2026, 8, 31, 2026, 10, 11)]
    [InlineData(2026, 11, 1, "Ordinary Time (October–November)", 2026, 10, 12, 2026, 11, 28)]
    public void The_long_Ordinary_Time_stretch_is_split_into_blocks(
        int year, int month, int day,
        string name,
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay)
    {
        var period = LiturgicalCalendar.PeriodContaining(new DateOnly(year, month, day));

        Assert.Equal(name, period.Name);
        Assert.Equal(new DateOnly(startYear, startMonth, startDay), period.Start);
        Assert.Equal(new DateOnly(endYear, endMonth, endDay), period.End);
    }

    /// <summary>
    /// The property the individual cases above cannot establish: the periods
    /// tile the calendar. A gap would leave Masses on no Rota at all; an overlap
    /// would roster them twice. Walked day by day rather than at the boundaries
    /// alone, since a wrong boundary is exactly what this needs to catch.
    /// </summary>
    [Fact]
    public void Periods_tile_the_calendar_with_no_gaps_or_overlaps()
    {
        var from = new DateOnly(2024, 1, 15);
        var until = new DateOnly(2040, 12, 31);

        var previous = LiturgicalCalendar.PeriodContaining(from);

        for (var date = from; date <= until; date = date.AddDays(1))
        {
            var period = LiturgicalCalendar.PeriodContaining(date);

            Assert.True(
                period.Start <= date && date <= period.End,
                $"{date:yyyy-MM-dd} was placed in {period.Name}, which runs {period.Start:yyyy-MM-dd} to {period.End:yyyy-MM-dd}.");

            if (period != previous)
            {
                Assert.True(
                    period.Start == previous.End.AddDays(1),
                    $"{previous.Name} ends {previous.End:yyyy-MM-dd} but {period.Name} starts {period.Start:yyyy-MM-dd}.");

                previous = period;
            }
        }
    }

    /// <summary>
    /// The cap ADR 0004 places on Ordinary Time: no block asks a Reader to
    /// commit to more than eight Sundays. Counted in Sundays rather than days
    /// because Sundays are what a Rota actually rosters — the last block of the
    /// year runs a few days long to reach the eve of Advent, which costs a
    /// Reader nothing.
    ///
    /// Advent &amp; Christmastide and Lent &amp; Eastertide are deliberately outside
    /// this: they are single named seasons, never subdivided.
    /// </summary>
    [Fact]
    public void No_Ordinary_Time_block_covers_more_than_eight_Sundays()
    {
        for (var date = new DateOnly(2024, 1, 15); date <= new DateOnly(2040, 12, 31); date = date.AddDays(1))
        {
            var period = LiturgicalCalendar.PeriodContaining(date);

            if (!period.Name.StartsWith("Ordinary Time", StringComparison.Ordinal))
            {
                continue;
            }

            var sundays = 0;
            for (var day = period.Start; day <= period.End; day = day.AddDays(1))
            {
                if (day.DayOfWeek == DayOfWeek.Sunday)
                {
                    sundays++;
                }
            }

            Assert.True(
                sundays <= 8,
                $"{period.Name} ({period.Start:yyyy-MM-dd} to {period.End:yyyy-MM-dd}) covers {sundays} Sundays.");
        }
    }
}

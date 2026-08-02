using System.Globalization;

namespace ParishRota.Domain;

/// <summary>
/// Key dates of the liturgical year. Rota Periods are derived from these
/// (ADR 0004), so everything here is pure, deterministic date arithmetic.
/// </summary>
public static class LiturgicalCalendar
{
    /// <summary>
    /// Easter Sunday in the Gregorian calendar, by the anonymous Gregorian
    /// computus (Meeus/Jones/Butcher). Every movable feast hangs off this date.
    /// </summary>
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;

        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(year, month, day);
    }

    /// <summary>
    /// Ash Wednesday, which opens Lent — and with it the Lent &amp; Eastertide
    /// Rota Period. Lent is forty days plus the six Sundays it spans, which are
    /// not counted, putting it 46 days before Easter.
    /// </summary>
    public static DateOnly AshWednesday(int year) => EasterSunday(year).AddDays(-46);

    /// <summary>
    /// Pentecost, the fiftieth day of Eastertide counting Easter Sunday itself,
    /// and the last day of the Lent &amp; Eastertide Rota Period.
    /// </summary>
    public static DateOnly Pentecost(int year) => EasterSunday(year).AddDays(49);

    /// <summary>
    /// The Baptism of the Lord, the last day of Christmastide and so the end of
    /// the Advent &amp; Christmastide Rota Period.
    ///
    /// In England and Wales, Epiphany is transferred to the Sunday between 2 and
    /// 8 January. The Baptism normally follows a week later, but when Epiphany
    /// itself falls that late there is no room for another Sunday, so it moves
    /// to the Monday immediately after.
    /// </summary>
    public static DateOnly BaptismOfTheLord(int year)
    {
        var epiphany = EpiphanySunday(year);

        return epiphany.Day >= 7
            ? epiphany.AddDays(1)
            : epiphany.AddDays(7);
    }

    /// <summary>
    /// The Rota Period a given date falls in (ADR 0004).
    /// </summary>
    public static RotaPeriod PeriodContaining(DateOnly date)
    {
        // Advent & Christmastide straddles New Year, so a date belongs to it
        // either by being on or after this year's Advent Sunday, or by falling
        // in the Christmastide tail of the period that opened last year.
        if (date >= AdventSunday(date.Year))
        {
            return new RotaPeriod(
                "Advent & Christmastide",
                AdventSunday(date.Year),
                BaptismOfTheLord(date.Year + 1));
        }

        if (date <= BaptismOfTheLord(date.Year))
        {
            return new RotaPeriod(
                "Advent & Christmastide",
                AdventSunday(date.Year - 1),
                BaptismOfTheLord(date.Year));
        }

        if (date >= AshWednesday(date.Year) && date <= Pentecost(date.Year))
        {
            return new RotaPeriod(
                "Lent & Eastertide",
                AshWednesday(date.Year),
                Pentecost(date.Year));
        }

        if (date < AshWednesday(date.Year))
        {
            return OrdinaryTime(
                BaptismOfTheLord(date.Year).AddDays(1),
                AshWednesday(date.Year).AddDays(-1));
        }

        return LateOrdinaryTimeBlockContaining(date);
    }

    /// <summary>
    /// Ordinary Time after Pentecost runs to the eve of Advent — up to 27 weeks,
    /// far too long to ask a Reader to commit to (ADR 0004). It is split into the
    /// fewest blocks that keeps each within <see cref="MaxSundaysPerBlock"/>,
    /// sharing the Sundays out as evenly as possible and giving the remainder to
    /// the earliest blocks.
    ///
    /// Blocks run Monday to Sunday, so a Saturday vigil Mass always sits in the
    /// same block as the Sunday it belongs to.
    /// </summary>
    private static RotaPeriod LateOrdinaryTimeBlockContaining(DateOnly date)
    {
        var start = Pentecost(date.Year).AddDays(1);
        var end = AdventSunday(date.Year).AddDays(-1);

        var sundays = SundaysBetween(start, end);
        var blockCount = (sundays.Count + MaxSundaysPerBlock - 1) / MaxSundaysPerBlock;
        var baseSundays = sundays.Count / blockCount;
        var longBlocks = sundays.Count % blockCount;

        var blockStart = start;
        var taken = 0;

        for (var block = 0; block < blockCount; block++)
        {
            taken += baseSundays + (block < longBlocks ? 1 : 0);

            // Every block but the last ends on its final Sunday. The last one
            // runs on through the stray weekdays before Advent Sunday.
            var blockEnd = block == blockCount - 1 ? end : sundays[taken - 1];

            if (date <= blockEnd)
            {
                return OrdinaryTime(blockStart, blockEnd);
            }

            blockStart = blockEnd.AddDays(1);
        }

        throw new ArgumentOutOfRangeException(
            nameof(date),
            date,
            "Date does not fall in any Rota Period, which should be impossible.");
    }

    private const int MaxSundaysPerBlock = 8;

    private static List<DateOnly> SundaysBetween(DateOnly start, DateOnly end)
    {
        var sundays = new List<DateOnly>();
        var firstSunday = start.AddDays(((int)DayOfWeek.Sunday - (int)start.DayOfWeek + 7) % 7);

        for (var sunday = firstSunday; sunday <= end; sunday = sunday.AddDays(7))
        {
            sundays.Add(sunday);
        }

        return sundays;
    }

    /// <summary>
    /// Ordinary Time blocks are named by the months they span, because that is
    /// what means something to a Reader being asked for unavailable dates —
    /// "Ordinary Time (May–July)" rather than a block number or a week range.
    /// Month names are invariant so the name never shifts with server locale.
    /// </summary>
    private static RotaPeriod OrdinaryTime(DateOnly start, DateOnly end)
    {
        var from = start.ToString("MMMM", CultureInfo.InvariantCulture);
        var to = end.ToString("MMMM", CultureInfo.InvariantCulture);

        return new RotaPeriod($"Ordinary Time ({from}–{to})", start, end);
    }

    private static DateOnly EpiphanySunday(int year)
    {
        var secondOfJanuary = new DateOnly(year, 1, 2);
        var daysUntilSunday = (7 - (int)secondOfJanuary.DayOfWeek) % 7;

        return secondOfJanuary.AddDays(daysUntilSunday);
    }

    /// <summary>
    /// The First Sunday of Advent, which opens the liturgical year.
    /// Advent has four Sundays, the last being the Sunday on or before
    /// Christmas Eve — so counting back three weeks from that Sunday gives the
    /// first. This handles Christmas Day falling on a Monday, where the Fourth
    /// Sunday of Advent and Christmas Eve are the same day.
    /// </summary>
    public static DateOnly AdventSunday(int year)
    {
        var christmasEve = new DateOnly(year, 12, 24);
        var fourthSunday = christmasEve.AddDays(-(int)christmasEve.DayOfWeek);

        return fourthSunday.AddDays(-21);
    }
}

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

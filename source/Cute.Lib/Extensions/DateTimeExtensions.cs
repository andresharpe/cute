namespace Cute.Lib.Extensions;

public static class DateTimeExtensions
{
    public static DateTime StripMilliseconds(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, dateTime.Second, dateTime.Kind);
    }

    public static DateTime FromUnix(int unixTime)
    {
        return DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime.StripMilliseconds();
    }
}
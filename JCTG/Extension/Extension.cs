namespace JCTG
{
    public static class Extension
    {
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToUnixTimestamp(this DateTime dateTime)
        {
            TimeSpan elapsedTime = dateTime.ToUniversalTime() - epoch;
            return (long)elapsedTime.TotalMilliseconds;
        }

        public static DateTime FromUnixTime(this long unixTime)
        {
            return epoch.AddSeconds(unixTime);
        }
    }
}

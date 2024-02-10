namespace JCTG
{
    public static class Extension
    {
        public static long ToUnixTimestamp(this DateTime dateTime)
        {
            DateTime epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan elapsedTime = dateTime.ToUniversalTime() - epoch;
            return (long)elapsedTime.TotalMilliseconds;
        }
    }
}

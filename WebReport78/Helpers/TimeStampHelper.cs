namespace WebReport78.Services
{
    public static class TimeStampHelper
    {
        public static string ConvertTimestamp(long timestamp)
        {
            // chuyển sang datetime
            DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(timestamp)
                                              .ToLocalTime()
                                              .DateTime;

            // đinh dang dd/mm/yyyy HH:mm
            string formattedDate = dateTime.ToString("dd/MM/yyyy HH:mm");
            return formattedDate;
        }
        public static long ConvertToUnixTimestamp(DateTime dateTime)
        {
            // Chuyển đổi DateTime sang Unix timestamp 
            return (long)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }
    }
}

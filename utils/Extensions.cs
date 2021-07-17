using System;
namespace ExhaleCreativity
{
    internal static class Extensions
    {
        internal static string ToJoinedString(this DateTime? joinedDate)
        {
            if (joinedDate == null)
                return string.Empty;

            // looking for something like "Jan, 17'
            return joinedDate.Value.ToString("MMM, yy");
        }

        internal static int ToYearsSince(this DateTime from, DateTime? to = null)
        {
            // Save today's date.
            to ??= DateTime.Today;
            // Calculate the age.
            var age = to.Value.Year - from.Year;
            // Go back to the year the person joined in case of a leap year
            if (from.Date > to.Value.AddYears(-age)) age--;

            return age;
        }

        internal static int ToDaysSince(this DateTime from, DateTime? to = null)
        {
            to ??= DateTime.Now;
            return (to.Value - from).Days;
        }

        internal static string GetElapsedTime(this TimeSpan ts)
        {
            // Format and display the TimeSpan value.
            return string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
        }
    }
}
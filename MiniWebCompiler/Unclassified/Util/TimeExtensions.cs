using System;
using System.Collections.Generic;

namespace Unclassified.Util
{
	/// <summary>
	/// Provides extension methods for DateTime and TimeSpan values.
	/// </summary>
	public static class TimeExtensions
	{
		/// <summary>
		/// Returns a verbose description of the TimeSpan value.
		/// </summary>
		/// <param name="value">The value to describe.</param>
		/// <returns></returns>
		public static string ToVerbose(this TimeSpan value)
		{
			var words = new Dictionary<string, string>
			{
				{ "joiner", " " },
				{ "second", "sec" },
				{ "seconds", "sec" },
				{ "minute", "min" },
				{ "minutes", "min" },
				{ "hour", "h" },
				{ "hours", "h" },
				{ "day", "d" },
				{ "days", "d" },
				{ "week", "wk" },
				{ "weeks", "wk" },
				{ "month", "mon" },
				{ "months", "mon" },
				{ "year", "y" },
				{ "years", "y" }
			};

			Func<long, string, string> word = (long number, string key) =>
			{
				return number + " " + (number == 1 ? words[key] : words[key + "s"]);
			};

			long secs = (long)value.TotalSeconds;
			int minute = 60;
			int hour = 60 * minute;
			int day = 24 * hour;
			int week = 7 * day;
			int month = 30 * day;
			int year = 365 * day;

			if (secs < 60)
				return word(secs, "second");
			if (secs < 5 * minute)
				return word(secs / minute, "minute") + words["joiner"] + word(secs % minute, "second");
			if (secs < hour)
				return word(secs / minute, "minute");
			if (secs < 6 * hour)
				return word(secs / hour, "hour") + words["joiner"] + word(secs % hour / minute, "minute");
			if (secs < day)
				return word(secs / hour, "hour");
			if (secs < 3 * day)
				return word(secs / day, "day") + words["joiner"] + word(secs % day / hour, "hour");
			if (secs < week)
				return word(secs / day, "day");
			if (secs < 3 * week)
				return word(secs / week, "week") + words["joiner"] + word(secs % week / day, "day");
			if (secs < 2 * month)
				return word(secs / week, "week");
			if (secs < year)
				return word(secs / month, "month");
			if (secs < 2 * year)
				return word(secs / year, "year") + words["joiner"] + word(secs % year / month, "month");
			return word(secs / year, "year");
		}
	}
}

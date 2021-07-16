using System;

namespace Rumble.Platform.ChatService.Utilities
{
	public static class Log
	{
		public static string Timestamp => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss\t");
		public static readonly DateTime Start = DateTime.Now;

		public static string ElapsedTime
		{
			get
			{
				TimeSpan time = DateTime.Now.Subtract(Start);
				long ms = (long)(time.TotalMilliseconds);
				string output = ms.ToString().PadLeft(9, ' ');

				return $"| {output}ms  ";
			}
		}

		public static void Write(string message)
		{
			message = ElapsedTime + message;
			Console.WriteLine(message);
		}
	}
}
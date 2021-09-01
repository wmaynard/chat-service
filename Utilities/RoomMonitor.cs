using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;

namespace Rumble.Platform.ChatService.Utilities
{
	public class RoomMonitor
	{
		private const string ENVIRONMENT_LAST_READ = "CHAT_MONITOR_LAST_READ";
		private const int THRESHOLD = Room.MESSAGE_CAPACITY / 2;
		private long LastRead { get; set; }
		private Dictionary<string, int> MessageCount { get; set; }
		private const int FREQUENCY_IN_MS = 300_000;
		private readonly Timer Timer;
		public event EventHandler<MonitorEventArgs> OnFlush;
		public RoomMonitor(EventHandler<MonitorEventArgs> onFlush)
		{
			LastRead = int.Parse(RumbleEnvironment.Variable(ENVIRONMENT_LAST_READ) ?? "0"); // TODO: SetEnvironmentVariable to store timestamp?
			MessageCount = new Dictionary<string, int>();
			Timer = new Timer(FREQUENCY_IN_MS) { AutoReset = true};
			Timer.Elapsed += Flush;
			OnFlush += onFlush;
			Flush(this, new MonitorEventArgs(null, LastRead, true));
			Room.OnMessageAdded += Increment;
		}

		private void Increment(object sender, Room.RoomEventArgs args)
		{
			Room room = (Room) sender;
			try
			{
				if (++MessageCount[room.Id] > THRESHOLD)
					Flush();
			}
			catch (KeyNotFoundException)
			{
				MessageCount.Add(room.Id, 1);
			}
		}

		private void Flush(object sender = null, EventArgs args = null)
		{
			Timer.Stop();
			try
			{
				OnFlush?.Invoke(this, new MonitorEventArgs(
					roomIds: MessageCount.Select(kvp => kvp.Key).ToArray(),
					lastRead: LastRead,
					restarted: args is MonitorEventArgs eventArgs && eventArgs.Restarted
				));
				LastRead = DateTimeOffset.Now.ToUnixTimeSeconds();
				MessageCount = new Dictionary<string, int>();
			}
			catch (Exception e)
			{
				Console.WriteLine(e); // TODO: This occasionally failed with a null reference when compressing attachments
				throw;
			}
			Timer.Start();
		}

		public class MonitorEventArgs : EventArgs
		{
			public string[] RoomIds { get; private set; }
			public long LastRead { get; private set; }
			public bool Restarted { get; private set; }
			public MonitorEventArgs(string[] roomIds, long lastRead, bool restarted)
			{
				RoomIds = roomIds;
				LastRead = lastRead;
				Restarted = restarted;
			}
		}
	}
}
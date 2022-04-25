using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Interop;

namespace Rumble.Platform.ChatService.Utilities;

// TODO: This should be converted to a PlatformTimerService
public class RoomMonitor
{
	private const int THRESHOLD = Room.MESSAGE_CAPACITY / 2;
	private long LastRead { get; set; }
	private Dictionary<string, int> MessageCount { get; set; }
	
	private readonly int FREQUENCY_IN_MS = int.Parse(PlatformEnvironment.Variable("SLACK_MONITOR_FREQUENCY_SECONDS") ?? "300") * 1_000;
	private readonly Timer Timer;
	public event EventHandler<MonitorEventArgs> OnFlush;
	public RoomMonitor(EventHandler<MonitorEventArgs> onFlush)
	{
		LastRead = 0;
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
		if (room.Id == null)
			return;
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
			Log.Local(Owner.Will, "Flushing the room monitor");
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
			Log.Error(Owner.Will, message: "Couldn't flush the room monitor.", exception: e);
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
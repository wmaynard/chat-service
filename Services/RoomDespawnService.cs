using System.Collections.Generic;
using System.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;

namespace Rumble.Platform.ChatService.Services;

public class RoomDespawnService : PlatformTimerService
{
	public const int DESPAWN_THRESHOLD_SECONDS = 3_600;
	private readonly RoomService _roomService;
	private Dictionary<string, long> _lifeSupport;
	private SlackMessageClient _slack;
	private int _roomsDestroyed;

	public RoomDespawnService(RoomService roomService) : base(intervalMS: 60_000)
	{
		_roomService = roomService;
		_lifeSupport = new Dictionary<string, long>();
		_roomsDestroyed = 0;

		_roomService.OnEmptyRoomsFound += TrackEmptyRooms;
		_slack = new SlackMessageClient(
			channel: PlatformEnvironment.Variable("SLACK_MONITOR_CHANNEL"),
			token: PlatformEnvironment.Variable("SLACK_CHAT_TOKEN")
		);
	}

	internal void TrackEmptyRooms(object sender, RoomService.EmptyRoomEventArgs args)
	{
		// Keep the previous timestamps, but forget about rooms that now have users in them.
		// This way, we only clear rooms that have been empty for the duration of the timestamp
		// and seeing no activity.
		_lifeSupport = args.roomIds
			.ToDictionary(
				keySelector: id => id,
				elementSelector: id => _lifeSupport.TryGetValue(id, out long idleSince) ? idleSince : UnixTime
		);
	}

	protected override void OnElapsed()
	{
		string[] roomsToDestroy = _lifeSupport
			.Where(pair => UnixTime - pair.Value > DESPAWN_THRESHOLD_SECONDS)
			.Select(pair => pair.Key)
			.ToArray();

		if (!roomsToDestroy.Any())
			return;
		
		Log.Info(Owner.Default, "Deleting empty rooms", data: new
		{
			RoomIds = roomsToDestroy
		});
		foreach (string id in roomsToDestroy)
			_roomService.Delete(id);
		_roomsDestroyed += roomsToDestroy.Length;
		UpdateSlack(roomsToDestroy);
		Graphite.Track("global-rooms-despawned", roomsToDestroy.Length, type: Graphite.Metrics.Type.FLAT);
		_lifeSupport.Clear();
	}

	public void UpdateSlack(string[] roomIds)
	{
		if (!roomIds.Any())
			return;
		
		List<SlackBlock> content = new List<SlackBlock>()
		{
			SlackBlock.Header($"chat-service-{PlatformEnvironment.Variable("RUMBLE_DEPLOYMENT")} | Global Rooms Despawned"),
			SlackBlock.Markdown($"The following rooms have had no active users for at least {(int)(DESPAWN_THRESHOLD_SECONDS / 60)} minutes."),
			SlackBlock.Divider(),
			SlackBlock.Markdown("*Affected Rooms*"),
			SlackBlock.Markdown("```")
		};
		
		content.AddRange(roomIds.Select(SlackBlock.Markdown));
		content.Add(SlackBlock.Markdown("```"));
		
		_slack.Send(content);
	}

	public override object HealthCheckResponseObject => GenerateHealthCheck(new GenericData()
	{
		["roomsTracked"] = _lifeSupport.Count,
		["roomsDestroyed"] = _roomsDestroyed
	});
}
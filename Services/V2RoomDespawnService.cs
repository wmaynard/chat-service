using System.Collections.Generic;
using System.Linq;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Services;

namespace Rumble.Platform.ChatService.Services;

public class V2RoomDespawnService : PlatformTimerService
{
	public const     int                DESPAWN_THRESHOLD_SECONDS = 3_600;
	private readonly V2RoomService      _roomService;
	private readonly DynamicConfig      _config;
	private          List<V2Room>       _roomsToDestroy;
	private          SlackMessageClient _slack;
	private          List<string>       _roomsDestroyed;

	public V2RoomDespawnService(V2RoomService roomService) : base(intervalMS: 60_000)
	{
		_roomService = roomService;
		_roomsDestroyed = new List<string>();

		long inactiveBuffer = _config.Require<long>(key: "inactiveBuffer");
		long cutoff = UnixTime - inactiveBuffer;

		_roomsToDestroy = _roomService.Find(room => room.LastUpdated < cutoff).ToList();

		_slack = new SlackMessageClient(
			channel: PlatformEnvironment.Require<string>("monitorChannel"),
			token: PlatformEnvironment.SlackLogBotToken
		);
	}

	protected override void OnElapsed()
	{
		if (!_roomsToDestroy.Any())
			return;
		
		Log.Info(Owner.Default, "Deleting empty rooms", data: new
		{
			RoomIds = _roomsToDestroy
		});
		foreach (V2Room room in _roomsToDestroy)
		{
			_roomService.Delete(room.Id);
			_roomsDestroyed.Add(room.Id);
		}
		
		UpdateSlack(_roomsDestroyed);
		Graphite.Track("global-rooms-despawned", _roomsDestroyed.Count, type: Graphite.Metrics.Type.FLAT);
	}

	public void UpdateSlack(List<string> roomIds)
	{
		if (!roomIds.Any())
			return;
		
		List<SlackBlock> content = new List<SlackBlock>()
		{
			SlackBlock.Header($"chat-service-{PlatformEnvironment.Deployment} | Global Rooms Despawned"),
			SlackBlock.Markdown($"The following rooms have had no active users for at least {(int)(DESPAWN_THRESHOLD_SECONDS / 60)} minutes."),
			SlackBlock.Divider(),
			SlackBlock.Markdown("*Affected Rooms*"),
			SlackBlock.Markdown("```")
		};
		
		content.AddRange(roomIds.Select(SlackBlock.Markdown));
		content.Add(SlackBlock.Markdown("```"));
		
		_slack.Send(content);
	}

	// public override object HealthCheckResponseObject => GenerateHealthCheck(new RumbleJson()
	// {
	// 	["roomsTracked"] = _lifeSupport.Count,
	// 	["roomsDestroyed"] = _roomsDestroyed
	// });
}
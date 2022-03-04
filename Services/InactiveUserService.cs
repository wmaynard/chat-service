using System;
using System.Collections.Generic;
using System.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;

namespace Rumble.Platform.ChatService.Services;

public class InactiveUserService : PlatformTimerService
{
	public const long FORCED_LOGOUT_THRESHOLD_S = 1_800; // 30 minutes
	// public const long FORCED_LOGOUT_THRESHOLD_S = 300; // 5 minutes
	private readonly Dictionary<string, long> _activity;
	private readonly RoomService _roomService;
	private readonly SlackMessageClient _slack;

	private int _forceLogouts;

	public InactiveUserService(RoomService roomService) : base(intervalMS: 60_000)
	{
		_roomService = roomService;
		_forceLogouts = 0;
		
		_slack = new SlackMessageClient(
			channel: PlatformEnvironment.Variable("SLACK_MONITOR_CHANNEL"),
			token: PlatformEnvironment.Variable("SLACK_CHAT_TOKEN")
		);
		_activity = _roomService.GetGlobals()
			.SelectMany(room => room.Members)
			.ToDictionary(
				keySelector: player => player.AccountId,
				elementSelector: player => UnixTime
			);
	}

	protected override void OnElapsed()
	{
		string[] inactiveAccountIDs = _activity
			.Where(kvp => UnixTime - kvp.Value > FORCED_LOGOUT_THRESHOLD_S)
			.Select(kvp => kvp.Key)
			.ToArray();
		if (!inactiveAccountIDs.Any())
			return;
		
		Log.Info(Owner.Default, "Inactive AccountIDs Found", data: new
		{
			AccountIDs = inactiveAccountIDs,
			InactiveCount = inactiveAccountIDs.Length
		});

		Dictionary<string, int> affectedRooms = new Dictionary<string, int>();
		HashSet<string> affectedAccounts = new HashSet<string>();
		foreach (string aid in inactiveAccountIDs)
		{
			// This *should* only have one result, if any at all, but just in case...
			Room[] globals = _roomService
				.GetRoomsForUser(aid)
				.Where(room => room.Type == Room.TYPE_GLOBAL)
				.ToArray();
			_activity.Remove(aid);
			
			if (!globals.Any()) 
				continue;

			foreach (Room global in globals)
			{
				global.RemoveMember(aid);
				_roomService.Update(global);
				
				if (!affectedRooms.TryAdd(global.Id, 1))
					affectedRooms[global.Id]++;
			}
			affectedAccounts.Add(aid);
		}

		if (!affectedAccounts.Any())
			return;

		_forceLogouts += affectedAccounts.Count;
		Graphite.Track("force-logouts", affectedAccounts.Count, type: Graphite.Metrics.Type.FLAT);
		// UpdateSlack(affectedAccounts, affectedRooms);
		
		Log.Info(Owner.Default, "Inactive accountIDs purged from chat rooms.", data: new
		{
			AffectedRooms = affectedRooms
		});
	}

	public void UpdateSlack(HashSet<string> aids, Dictionary<string, int> affectedRooms)
	{
		if (!aids.Any() && !affectedRooms.Any())
			return;
		string[] messages = affectedRooms.Select(pair => $"{pair.Key}: {pair.Value} users removed.").ToArray();

		List<SlackBlock> content = new List<SlackBlock>()
		{
			SlackBlock.Header($"chat-service-{PlatformEnvironment.Variable("RUMBLE_DEPLOYMENT")} | Inactive Users Removed"),
			SlackBlock.Divider(),
			SlackBlock.Markdown("*Affected Rooms*")
		};
		if (affectedRooms.Any())
			content.AddRange(affectedRooms
				.Select(pair => SlackBlock.Markdown($"*{pair.Key}*: `{pair.Value}` users removed."))
			);

		if (aids.Any())
		{
			content.AddRange(new List<SlackBlock>()
			{
				SlackBlock.Divider(),
				SlackBlock.Markdown($"*Offline Account IDs (more than {(int)(FORCED_LOGOUT_THRESHOLD_S / 60)} minutes idle)*")
			});
			content.Add(SlackBlock.Markdown("```" + string.Join('\n', aids) + "```"));
		}
		
		_slack.Send(content);
	}

	public void Track(string aid)
	{
		try
		{
			if (aid == null)
				throw new NullReferenceException();
			_activity[aid] = UnixTime;
		}
		catch (Exception e)
		{
			Log.Error(Owner.Default, "Invalid Account ID.  Unable to track _activity.", data: new
			{
				AccountId = aid
			}, exception: e);
		}
		
	}
	public void Track(PlayerInfo info) => Track(info?.AccountId);
	public void Track(TokenInfo token) => Track(token?.AccountId);

	public override object HealthCheckResponseObject
	{
		get
		{
			GenericData output = new GenericData();
			output["trackedUsers"] = _activity.Count;
			output["forcedLogouts"] = _forceLogouts;
			
			return GenerateHealthCheck(output);
		}
	}
}
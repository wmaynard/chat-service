using System;
using System.Collections.Generic;
using System.Linq;
using RCL.Logging;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Services;

public class InactiveUserService : PlatformTimerService
{
	public const long FORCED_LOGOUT_THRESHOLD_S = 1_800; // 30 minutes
	// public const long FORCED_LOGOUT_THRESHOLD_S = 300; // 5 minutes
	private readonly Dictionary<string, long> _activity;
	private readonly RoomService              _roomService;
	private readonly ApiService               _apiService;
	private readonly SlackMessageClient       _slack;


	private int _forceLogouts;

	public InactiveUserService(RoomService roomService) : base(intervalMS: 60_000)
	{
		_roomService = roomService;
		_forceLogouts = 0;

		try
		{
			_slack = new SlackMessageClient(
				channel: PlatformEnvironment.Require<string>("monitorChannel"),
				token: PlatformEnvironment.SlackLogBotToken
			);
			_activity = _roomService
				.GetGlobals()
				.SelectMany(room => room.Members)
				.ToDictionary(
					keySelector: player => player.AccountId,
					elementSelector: player => Timestamp.Now
				);

		}
		catch (Exception e)
		{
			Log.Error(Owner.Will, $"Could not instantiate {this.GetType().Name}.", exception: e);
			
			_apiService.Alert(
				title: $"Could not instantiate {this.GetType().Name}.",
				message: $"Could not instantiate {this.GetType().Name}.",
				countRequired: 1,
				timeframe: 300,
				data: new RumbleJson
				    {
				        { "Exception", e }
				    } 
			);

		}
	}

	protected override void OnElapsed()
	{
		if (_activity == null || !_activity.Any())
			return;
		string[] inactiveAccountIDs = _activity
			.Where(pair => Timestamp.Now - pair.Value > FORCED_LOGOUT_THRESHOLD_S)
			.Select(pair => pair.Key)
			.ToArray();
		if (!inactiveAccountIDs.Any())
			return;
		
		Log.Verbose(Owner.Default, "Inactive AccountIDs Found", data: new
		{
			AccountIDs = inactiveAccountIDs,
			InactiveCount = inactiveAccountIDs.Length
		});

		Dictionary<string, int> affectedRooms = new Dictionary<string, int>();
		HashSet<string> affectedAccounts = new HashSet<string>();
		foreach (string aid in inactiveAccountIDs)
		{
			Room[] globals = Array.Empty<Room>();
			
			// [PLATF-6076]: Where is sometimes receiving null inputs, throwing runtime exceptions in 308 exclusively.
			try
			{
				globals = _roomService
					.GetRoomsForUser(aid)
					.Where(room => room.Type == Room.TYPE_GLOBAL)
					.ToArray();
			}
			catch { }
			
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
		
		Log.Verbose(Owner.Default, "Inactive accountIDs purged from chat rooms.", data: new
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
			SlackBlock.Header($"chat-service-{PlatformEnvironment.Deployment} | Inactive Users Removed"),
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
			_activity[aid] = Timestamp.Now;
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

	// public override object HealthCheckResponseObject
	// {
	// 	get
	// 	{
	// 		RumbleJson output = new RumbleJson();
	// 		output["trackedUsers"] = _activity.Count;
	// 		output["forcedLogouts"] = _forceLogouts;
	// 		
	// 		return GenerateHealthCheck(output);
	// 	}
	// }
}
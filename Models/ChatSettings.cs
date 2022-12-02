using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

// The class name "Settings" appears to be reserved; "ChatSettings" is used to avoid "Models.Settings"
public class ChatSettings : PlatformCollectionDocument
{
	internal const string DB_KEY_ACCOUNT_ID = "aid";
	internal const string DB_KEY_MUTED_PLAYERS = "mp";

	public const string FRIENDLY_KEY_ACCOUNT_ID = "aid";
	public const string FRIENDLY_KEY_MUTED_PLAYERS = "mutedPlayers";
	
	#region MONGO
	[SimpleIndex]
	[BsonElement(DB_KEY_ACCOUNT_ID)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_ACCOUNT_ID)]
	public string AccountId { get; set; }
	
	[BsonElement(DB_KEY_MUTED_PLAYERS)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_MUTED_PLAYERS)]
	public List<PlayerInfo> MutedPlayers { get; private set; }
	#endregion MONGO
	
	#region INTERNAL
	// Since the class can't be named "Settings", we'll override it in the ResponseObject.
	// [BsonIgnore]
	// [JsonIgnore]
	// public override object ResponseObject => new { Settings = this };
	#endregion INTERNAL

	public ChatSettings(string accountId)
	{
		AccountId = accountId;
		MutedPlayers = new List<PlayerInfo>();
	}

	public void AddMutedPlayer(PlayerInfo muted)
	{
		if (MutedPlayers.Any(p => p.AccountId == muted.AccountId))
			return;
		MutedPlayers.Add(muted);
	}
	
	public void RemoveMutedPlayer(PlayerInfo muted)
	{
		try
		{
			MutedPlayers.Remove(MutedPlayers.First(p => p.AccountId == muted.AccountId));
		}
		catch (InvalidOperationException) { }	// We don't need to do anything since the player was no longer muted.
	}
}
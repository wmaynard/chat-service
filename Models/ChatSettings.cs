using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Rumble.Platform.ChatService.Models
{ // TODO: Update all models with JsonProperty(Name = ... )
	public class ChatSettings	// "Settings" appears to be reserved; "ChatSettings" is used to avoid "Models.Settings"
	{
		private const string DB_KEY_ACCOUNT_ID = "aid";
		private const string DB_KEY_MUTED_PLAYERS = "mp";

		public const string FRIENDLY_KEY_ACCOUNT_ID = "accountId";
		public const string FRIENDLY_KEY_MUTED_PLAYERS = "mutedPlayers";
		
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
		[BsonElement(DB_KEY_ACCOUNT_ID)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_ACCOUNT_ID)]
		public string AccountId { get; set; }
		[BsonElement(DB_KEY_MUTED_PLAYERS)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_MUTED_PLAYERS)]
		private List<PlayerInfo> MutedPlayers { get; set; }

		[BsonIgnore]
		[JsonIgnore]	// Required to avoid calling circular reference when serializing
		public object ResponseObject => new { Settings = this };

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

		public void UnmuteAll()
		{
			MutedPlayers = new List<PlayerInfo>();
		}

		public void RemoveMutedPlayer(PlayerInfo muted)
		{
			try
			{
				MutedPlayers.Remove(MutedPlayers.First(p => p.AccountId == muted.AccountId));
			}
			catch (InvalidOperationException)	// We don't need to do anything since the player was no longer muted.
			{
			}
		}
	}
}
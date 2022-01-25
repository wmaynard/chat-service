using System.Text.Json;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;

namespace Rumble.Platform.ChatService.Models
{
	public class PlayerInfo : PlatformDataModel
	{
		internal const string DB_KEY_ACCOUNT_ID = "aid";
		internal const string DB_KEY_AVATAR = "pic";
		internal const string DB_KEY_DISCRIMINATOR = "disc";
		internal const string DB_KEY_LEVEL = "lv";
		internal const string DB_KEY_MEMBER_SINCE = "ms";
		internal const string DB_KEY_POWER = "pwr";
		internal const string DB_KEY_SCREENNAME = "sn";
		
		public const string FRIENDLY_KEY_ACCOUNT_ID = "aid";
		public const string FRIENDLY_KEY_AVATAR = "avatar";
		public const string FRIENDLY_KEY_DISCRIMINATOR = "discriminator";
		public const string FRIENDLY_KEY_LEVEL = "level";
		public const string FRIENDLY_KEY_MEMBER_SINCE = "memberSince";
		public const string FRIENDLY_KEY_POWER = "power";
		public const string FRIENDLY_KEY_SCREENNAME = "sn";
		public const string FRIENDLY_KEY_SELF = "playerInfo";	// Special key for parsing playerInfo from the Client
		public const string FRIENDLY_KEY_UNIQUE_SCREENNAME = "screenname";

		#region MONGO
		[BsonElement(DB_KEY_ACCOUNT_ID)]
		[JsonInclude, JsonPropertyName(FRIENDLY_KEY_ACCOUNT_ID)]
		public string AccountId { get; set; }
		
		[BsonElement(DB_KEY_AVATAR)]
		[JsonInclude, JsonPropertyName(FRIENDLY_KEY_AVATAR)]
		public string Avatar { get; set; }
		
		[BsonElement(DB_KEY_DISCRIMINATOR)]
		[JsonInclude, JsonPropertyName(FRIENDLY_KEY_DISCRIMINATOR), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
		public int Discriminator { get; set; }
		
		[BsonElement(DB_KEY_MEMBER_SINCE), BsonIgnoreIfNull]
		[JsonInclude, JsonPropertyName(FRIENDLY_KEY_MEMBER_SINCE), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
		public long InRoomSince { get; set; }
		
		[BsonElement(DB_KEY_LEVEL)]
		[JsonInclude, JsonPropertyName(FRIENDLY_KEY_LEVEL)]
		public int Level { get; set; }
		
		[BsonElement(DB_KEY_POWER)]
		[JsonInclude, JsonPropertyName(FRIENDLY_KEY_POWER), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
		public int Power { get; set; }
		
		[BsonElement(DB_KEY_SCREENNAME)]
		[JsonInclude, JsonPropertyName(FRIENDLY_KEY_SCREENNAME)]
		public string ScreenName { get; set; }
		#endregion MONGO

		#region INTERNAL
		[BsonIgnore]
		[JsonIgnore]
		public string SlackLink => SlackFormatter.Link(
			url: $"{PlatformEnvironment.Variable("RUMBLE_PUBLISHING_PLAYER_URL")}?gukey={AccountId}", 
			text: UniqueScreenname
		);
		
		[BsonIgnore]
		[JsonIgnore]
		public string UniqueScreenname => $"{ScreenName}#{Discriminator.ToString().PadLeft(4, '0')}";
		#endregion INTERNAL

		public static PlayerInfo FromRequest(GenericData body, TokenInfo token)
		{
			PlayerInfo output = body.Require<PlayerInfo>(FRIENDLY_KEY_SELF);
			output.AccountId = token.AccountId;
			output.Discriminator = token.Discriminator;
			output.ScreenName = token.ScreenName;
			
			return output;
		}
		
		public static PlayerInfo FromJsonElement(JsonElement input)
		{
			// TODO: This should be abandoned, instead using the other overload (TokenInfo for AccountId / sn / discriminator)
			return new PlayerInfo()
			{
				AccountId = JsonHelper.Optional<string>(input, FRIENDLY_KEY_ACCOUNT_ID),
				Avatar = JsonHelper.Optional<string>(input, FRIENDLY_KEY_AVATAR),
				ScreenName = JsonHelper.Optional<string>(input, FRIENDLY_KEY_SCREENNAME),
				InRoomSince = UnixTime,
				Level = JsonHelper.Optional<int?>(input, FRIENDLY_KEY_LEVEL) ?? 0,
				Power = JsonHelper.Optional<int?>(input, FRIENDLY_KEY_POWER) ?? 0,
				Discriminator = JsonHelper.Optional<int?>(input, FRIENDLY_KEY_DISCRIMINATOR) ?? 0
			};
		}
		public static PlayerInfo FromJsonElement(JsonElement input, TokenInfo token)
		{
			return new PlayerInfo()
			{
				AccountId = token.AccountId,
				Avatar = JsonHelper.Optional<string>(input, FRIENDLY_KEY_AVATAR),
				ScreenName = token.ScreenName,
				InRoomSince = UnixTime,
				Level = JsonHelper.Optional<int?>(input, FRIENDLY_KEY_LEVEL) ?? 0,
				Power = JsonHelper.Optional<int?>(input, FRIENDLY_KEY_POWER) ?? 0,
				Discriminator = token.Discriminator
			};
		}

		public void AddTokenInfo(TokenInfo token)
		{
			AccountId = token.AccountId;
			ScreenName = token.ScreenName;
			Discriminator = token.Discriminator;
		}

		public void Validate()
		{
			if (AccountId == null)
				throw new InvalidPlayerInfoException(this, "AccountId");
			if (ScreenName == null)
				throw new InvalidPlayerInfoException(this, "ScreenName");
		}

		// TODO: This is a super janky kluge to allow the server to send messages to rooms.
		// This really should be done by allowing Rooms to allow messages from non-members in special circumstances.
		public static readonly PlayerInfo Admin = new PlayerInfo()
		{
			AccountId = "RumbleAdmin",
			Avatar = null,
			Discriminator = 10000,
			InRoomSince = 0,
			Level = 99,
			Power = 9001,
			ScreenName = "Administrator"
		};
	}
}
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rumble.Platform.ChatService.Utilities;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;

namespace Rumble.Platform.ChatService.Models
{
	public class PlayerInfo : RumbleModel
	{
		internal const string DB_KEY_ACCOUNT_ID = "aid";
		internal const string DB_KEY_AVATAR = "pic";
		internal const string DB_KEY_SCREENNAME = "sn";
		internal const string DB_KEY_MEMBER_SINCE = "ms";
		internal const string DB_KEY_LEVEL = "lv";
		internal const string DB_KEY_POWER = "pwr";
		internal const string DB_KEY_DISCRIMINATOR = "disc";
		
		public const string FRIENDLY_KEY_ACCOUNT_ID = "aid";
		public const string FRIENDLY_KEY_AVATAR = "avatar";
		public const string FRIENDLY_KEY_SCREENNAME = "sn";
		public const string FRIENDLY_KEY_MEMBER_SINCE = "memberSince";
		public const string FRIENDLY_KEY_LEVEL = "level";
		public const string FRIENDLY_KEY_POWER = "power";
		public const string FRIENDLY_KEY_DISCRIMINATOR = "discriminator";
		public const string FRIENDLY_KEY_UNIQUE_SCREENNAME = "screenname";

		[BsonElement(DB_KEY_ACCOUNT_ID)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_ACCOUNT_ID)]
		public string AccountId { get; set; }
		
		[BsonElement(DB_KEY_AVATAR)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_AVATAR)]
		public string Avatar { get; set; }
		[BsonElement(DB_KEY_MEMBER_SINCE), BsonIgnoreIfNull]
		[JsonProperty(FRIENDLY_KEY_MEMBER_SINCE, NullValueHandling = NullValueHandling.Ignore)]
		public long InRoomSince { get; set; }
		
		[BsonElement(DB_KEY_SCREENNAME)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_SCREENNAME)]
		public string ScreenName { get; set; }
		[BsonElement(DB_KEY_LEVEL)]
		[JsonProperty(FRIENDLY_KEY_LEVEL)]
		public int Level { get; set; }
		[BsonElement(DB_KEY_POWER)]
		[JsonProperty(FRIENDLY_KEY_POWER, DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int Power { get; set; }
		[BsonElement(DB_KEY_DISCRIMINATOR)]
		[JsonProperty(PropertyName = FRIENDLY_KEY_DISCRIMINATOR, DefaultValueHandling = DefaultValueHandling.Ignore)]
		public int Discriminator { get; set; }
		[BsonIgnore]
		[JsonIgnore]
		public string UniqueScreenname => $"{ScreenName}#{Discriminator.ToString().PadLeft(4, '0')}";

		public static PlayerInfo FromJToken(JToken input)
		{
			return new PlayerInfo()
			{
				AccountId = input[FRIENDLY_KEY_ACCOUNT_ID]?.ToObject<string>(),
				Avatar = input[FRIENDLY_KEY_AVATAR]?.ToObject<string>(),
				ScreenName = input[FRIENDLY_KEY_SCREENNAME]?.ToObject<string>(),
				InRoomSince = UnixTime,
				Level = input[FRIENDLY_KEY_LEVEL]?.ToObject<int>() ?? 0,
				Power = input[FRIENDLY_KEY_POWER]?.ToObject<int>() ?? 0,
				Discriminator = input[FRIENDLY_KEY_DISCRIMINATOR]?.ToObject<int>() ?? 0,
			};
		}

		public static PlayerInfo FromJToken(JToken input, TokenInfo token)
		{
			return new PlayerInfo()
			{
				AccountId = token.AccountId,
				Avatar = input[FRIENDLY_KEY_AVATAR]?.ToObject<string>(),
				ScreenName = token.ScreenName,
				InRoomSince = UnixTime,
				Level = input[FRIENDLY_KEY_LEVEL]?.ToObject<int>() ?? 0,
				Power = input[FRIENDLY_KEY_POWER]?.ToObject<int>() ?? 0,
				Discriminator = token.Discriminator
			};
		}

		public void Validate()
		{
			if (AccountId == null)
				throw new InvalidPlayerInfoException("AccountId cannot be null.");
			if (ScreenName == null)
				throw new InvalidPlayerInfoException("ScreenName cannot be null.");
		}

		[BsonIgnore]
		[JsonIgnore]
		public string SlackLink => SlackFormatter.Link(
			url: $"{RumbleEnvironment.Variable("RUMBLE_PUBLISHING_PLAYER_URL")}?gukey={AccountId}", 
			text: UniqueScreenname
		);
	}
}
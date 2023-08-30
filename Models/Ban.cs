using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

public class Ban : PlatformCollectionDocument
{
	internal const string DB_KEY_ACCOUNT_ID = "aid";
	internal const string DB_KEY_EXPIRATION = "exp";
	internal const string DB_KEY_ISSUED = "iss";
	internal const string DB_KEY_REASON = "why";
	internal const string DB_KEY_SNAPSHOT = "snap";

	public const string FRIENDLY_KEY_ACCOUNT_ID = "accountId";
	public const string FRIENDLY_KEY_EXPIRATION = "expiration";
	public const string FRIENDLY_KEY_ISSUED = "issued";
	public const string FRIENDLY_KEY_REASON = "reason";
	public const string FRIENDLY_KEY_SNAPSHOT = "snapshot";
	public const string FRIENDLY_KEY_TIME_REMAINING = "timeRemaining";
	
	#region MONGO
	[SimpleIndex]
	[BsonElement(DB_KEY_ACCOUNT_ID)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_ACCOUNT_ID)]
	public string AccountId { get; private set; }
	
	[BsonElement(DB_KEY_EXPIRATION), BsonIgnoreIfNull]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_EXPIRATION), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? Expiration { get; private set; }
	
	[BsonElement(DB_KEY_ISSUED)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_ISSUED)]
	public long IssuedOn { get; private set; }
	
	[BsonElement(DB_KEY_REASON), BsonIgnoreIfNull]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_REASON)]
	public string Reason { get; private set; }
	
	[BsonElement(DB_KEY_SNAPSHOT)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_SNAPSHOT), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public Room[] Snapshot { get; private set; }
	#endregion MONGO

	#region CLIENT
	[BsonIgnore]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_TIME_REMAINING)]
	public string TimeRemaining
	{
		get
		{
			DateTime exp = ExpirationDate;
			if (exp.Equals(DateTime.MaxValue))
				return "permanent";
			TimeSpan x = exp.Subtract(DateTime.UtcNow);
			return x.TotalMilliseconds < 0
				? "0h00m00s"
				: $"{x.Hours}h{x.Minutes.ToString().PadLeft(2, '0')}m{x.Seconds.ToString().PadLeft(2, '0')}s";
		}
	}
	#endregion
	
	#region INTERNAL
	[BsonIgnore]
	[JsonIgnore]
	public DateTime ExpirationDate => Expiration == null ? DateTime.MaxValue : DateTime.UnixEpoch.AddSeconds((double)Expiration);
	
	[BsonIgnore]
	[JsonIgnore]
	public bool IsExpired => ExpirationDate.Subtract(DateTime.UtcNow).TotalMilliseconds <= 0;
	
	[BsonIgnore]
	[JsonIgnore]
	private DateTime IssuedOnDate => DateTime.UnixEpoch.AddSeconds((double)IssuedOn);
	#endregion INTERNAL

	public Ban(string accountId, string reason, long? expiration, IEnumerable<Room> rooms)
	{
		AccountId = accountId;
		Reason = reason;
		IssuedOn = Timestamp.UnixTime;
		Expiration = expiration;
		Snapshot = rooms?.ToArray();
	}

	public void PurgeSnapshot() => Snapshot = null;
}
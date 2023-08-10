using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using RCL.Logging;
using Rumble.Platform.ChatService.Exceptions;
using Rumble.Platform.Common.Attributes;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.Common.Interop;
using Rumble.Platform.Common.Models;
using Rumble.Platform.Common.Services;
using Rumble.Platform.Data;

namespace Rumble.Platform.ChatService.Models;

[BsonIgnoreExtraElements]
public class V2Room : PlatformCollectionDocument
{
	internal const string DB_KEY_CAPACITY          = "cap";
	internal const string DB_KEY_CREATED_TIMESTAMP = "ts";
	internal const string DB_KEY_GUILD_ID          = "gid";
	internal const string DB_KEY_LANGUAGE          = "lang";
	internal const string DB_KEY_MEMBERS           = "who";
	internal const string DB_KEY_MESSAGES          = "msg";
	internal const string DB_KEY_TYPE              = "t";
	internal const string DB_KEY_LAST_UPDATED      = "last";

	public const string FRIENDLY_KEY_CAPACITY               = "capacity";
	public const string FRIENDLY_KEY_CREATED_TIMESTAMP      = "created";
	public const string FRIENDLY_KEY_GUILD_ID               = "guildId";
	public const string FRIENDLY_KEY_HAS_STICKY             = "hasSticky";
	public const string FRIENDLY_KEY_LANGUAGE               = "language";
	public const string FRIENDLY_KEY_LANGUAGE_DISCRIMINATOR = "listingId";
	public const string FRIENDLY_KEY_MEMBERS                = "members";
	public const string FRIENDLY_KEY_MESSAGES               = "messages";
	public const string FRIENDLY_KEY_TYPE                   = "type";
	public const string FRIENDLY_KEY_VACANCIES              = "vacancies";
	public const string FRIENDLY_KEY_LAST_UPDATED           = "lastUpdated";

	public enum V2RoomType
	{
		Global,
		Sticky,
		Guild,
		Dm,
		Unknown
	}

	public const int MESSAGE_CAPACITY = 200;

	public static readonly string ENVIRONMENT = PlatformEnvironment.Deployment;
	public static int GlobalPlayerCapacity => DynamicConfig.Instance?.Optional<int?>("roomCapacity") ?? 200;

	public static event EventHandler<RoomEventArgs> OnMessageAdded;
	private static Dictionary<string, List<string>> IDMap; // TODO: This could be a separate service if there's a good way to bring a singleton into models
	
	private int _memberCapacity;
	
	#region MONGO
	[BsonElement(DB_KEY_CREATED_TIMESTAMP)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_CREATED_TIMESTAMP)]
	public long CreatedTimestamp { get; set; }
	
	[BsonElement(DB_KEY_GUILD_ID), BsonIgnoreIfNull]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_GUILD_ID)]
	[SimpleIndex]
	public string GuildId { get; set; }
	
	[SimpleIndex]
	[BsonElement(DB_KEY_LANGUAGE), BsonIgnoreIfNull]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_LANGUAGE), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string Language { get; set; }
	
	[BsonElement(DB_KEY_CAPACITY)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_CAPACITY)]
	public int MemberCapacity
	{
		get => Type == V2RoomType.Global 
			? GlobalPlayerCapacity 
			: _memberCapacity;
		set => _memberCapacity = value;
	}
	
	[BsonElement(DB_KEY_MEMBERS)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_MEMBERS)]
	public HashSet<string> Members { get; set; }
	
	[BsonElement(DB_KEY_MESSAGES)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_MESSAGES)]
	public List<V2Message> Messages { get; set; }
	
	[BsonElement(DB_KEY_TYPE)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_TYPE)]
	public V2RoomType Type { get; set; }
	
	[BsonElement(DB_KEY_LAST_UPDATED)]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_LAST_UPDATED)]
	public long LastUpdated { get; set; }
	#endregion MONGO
	
	#region CLIENT
	[BsonIgnore]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_LANGUAGE_DISCRIMINATOR), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public int? Discriminator
	{
		get
		{
			if (Type != V2RoomType.Global)
				return null;
			IDMap ??= new Dictionary<string, List<string>>();
			if (!IDMap.ContainsKey(Language))
				IDMap.Add(Language, new List<string>());
			int output = IDMap[Language].IndexOf(Id);
			if (output >= 0)
				return output + 1;
			IDMap[Language].Add(Id);
			return IDMap[Language].IndexOf(Id) + 1;
		}
	}

	[BsonIgnore]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_HAS_STICKY), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool HasSticky => Messages.Any(message => message.Type == V2Message.V2MessageType.Sticky);
	
	[BsonIgnore]
	[JsonInclude, JsonPropertyName(FRIENDLY_KEY_VACANCIES)]
	public int Vacancies => MemberCapacity - Members.Count;
	#endregion CLIENT
	
	#region INTERNAL
	[BsonIgnore]
	[JsonIgnore]
	public bool IsFull => Members.Count >= MemberCapacity;

	[BsonIgnore]
	[JsonIgnore]
	public bool IsStickyRoom => Type == V2RoomType.Sticky;
	
	[BsonIgnore]
	[JsonIgnore]
	public string SlackColor => Id[^6..];
	#endregion INTERNAL
	
	public V2Room ()
	{
		CreatedTimestamp = Timestamp.UnixTime;
		Messages = new List<V2Message>();
		Members = new HashSet<string>();
	}

	/// <summary>
	/// Adds an account ID to the Room.  If the player already exists in the room, an AlreadyInRoomException is
	/// thrown.  If the Room is at capacity, a RoomFullException is thrown.
	/// </summary>
	/// <param name="accountId">An accountId string, parsed from a JToken.</param>
	/// <exception cref="AlreadyInRoomException">Indicates the account ID is already in the Room.</exception>
	/// <exception cref="RoomFullException">Indicates the Room is already full, or became full as the account was joining.</exception>
	public void AddMember(string accountId)
	{
		if (accountId == null)
			throw new V2InvalidPlayerInfoException(accountId, "AccountId");
		if (HasMember(accountId))
			throw new V2AlreadyInRoomException(this, accountId);
		if (Members.Count >= MemberCapacity)
			throw new V2RoomFullException(this, accountId);
		Members.Add(accountId);
	}

	/// <summary>
	/// Adds a Message to the room.  If the author of the message is not a Member of the Room, a NotInRoomException
	/// is thrown.
	/// </summary>
	/// <param name="msg">The Message to add.</param>
	public void AddMessage(V2Message msg)
	{
		if (msg.Type != V2Message.V2MessageType.Sticky)
		{
			RequireMember(msg.AccountId);
		}

		Messages.Add(msg);
		OnMessageAdded?.Invoke(this, new RoomEventArgs(msg));
		Messages = Messages.OrderBy(m => m.Timestamp).ToList();
		if (Messages.Count(m => m.Type != V2Message.V2MessageType.Sticky) <= MESSAGE_CAPACITY)
			return;

		V2Message[] stickies = Messages.Where(m => m.Type == V2Message.V2MessageType.Sticky).ToArray();
		Messages.RemoveRange(0, Messages.Count - MESSAGE_CAPACITY);
		if (stickies.Any())	// Track the stickies back in
			Messages = Messages.Union(stickies).OrderBy(m => m.Timestamp).ToList();
	}

	public int RemoveStickies(bool expiredOnly = false)
	{
		int before = Messages.Count;
		Messages = expiredOnly
			? Messages = Messages.Where(m => m.Type != V2Message.V2MessageType.Sticky || !m.IsExpired).ToList()
			: Messages = Messages.Where(m => m.Type != V2Message.V2MessageType.Sticky).ToList();
		int after = Messages.Count;
		if (before > after)
			Log.Local(Owner.Will, $"{before - after} stickies deleted from {Language ?? (Type == V2RoomType.Sticky ? "Stickies" : "")} | {Id}");
		return before - after;
	}
	/// <summary>
	/// Adds a Message to the room.  If the author of the message is not a Member of the Room, a NotInRoomException
	/// is thrown.
	/// </summary>
	/// <param name="msg">The Message to add.</param>
	/// <param name="lastReadTimestamp">The timestamp from the last read message of the client.</param>
	/// <returns>An IEnumerable of Messages.  Deprecated with the use of RoomUpdate.</returns>
	public IEnumerable<V2Message> AddMessage(V2Message msg, long lastReadTimestamp)
	{
		AddMessage(msg);
		return MessagesSince(lastReadTimestamp);
	}

	/// <summary>
	/// Removes an account ID from the Room.  If the account is not a Member, a NotInRoomException is thrown.
	/// </summary>
	/// <param name="accountId">The account ID to remove.</param>
	public void RemoveMember(string accountId)
	{
		if (Members.FirstOrDefault(aid => aid == accountId) == null)
		{
			throw new V2NotInRoomException(this, accountId);
		}
		Members.Remove(accountId);
	}

	public void RemoveMembers(params string[] accountIds)
	{
		foreach (string aid in accountIds)
		{
			RemoveMember(aid);
		}
	}

	/// <summary>
	/// Wrapper for a LINQ query to look for an accountID in the Room.
	/// </summary>
	/// <param name="accountId">The account ID to look for.</param>
	/// <returns>True if the account is found, otherwise false.</returns>
	public bool HasMember(string accountId) => Members.Any(m => m == accountId);
	public bool HasMember(IEnumerable<string> accountIds) => Members.Any(accountIds.Contains);

	/// <summary>
	/// Checks to make sure an account exists as a Member.  If the account ID isn't found, a NotInRoomException
	/// is thrown.
	/// </summary>
	/// <param name="accountId">The account ID to look for.</param>
	/// <exception cref="NotInRoomException">Indicates that the account does not exist in the given room.</exception>
	private void RequireMember(string accountId)
	{
		if (!(HasMember(accountId)))
		{
			throw new V2NotInRoomException(this, accountId);
		}
	}

	/// <summary>
	/// Retrieve all Messages that occur after a provided timestamp.  Used in generating RoomUpdates.
	/// </summary>
	/// <param name="timestamp">A Unix timestamp.</param>
	/// <returns>An IEnumerable of all Messages after the timestamp.</returns>
	public IEnumerable<V2Message> MessagesSince(long timestamp) => Messages.Where(m => m.Timestamp > timestamp);

	public IEnumerable<V2Message> Snapshot(string messageId, int before, int after)
	{
		List<V2Message> ordered = Messages.OrderBy(m => m.Timestamp).ToList();
		int position = ordered.IndexOf(ordered.First(m => m.Id == messageId));
		int start = position > before ? position - before : 0;

		return ordered.Skip(start).Take(after + before);
	}

	/// <summary>
	/// Converts a Room to a SlackAttachment for the Room Monitor.
	/// </summary>
	/// <param name="timestamp"></param>
	/// <returns>A SlackAttachment, or null if there are no new messages.  Null attachments should be removed during the Compress stage.</returns>
	public SlackAttachment ToSlackAttachment(long timestamp)
	{
		// 2021.09.30: Omit Broadcasts from the chat monitor
		List<V2Message> news = Messages.Where(message => message.Timestamp >= timestamp && message.Type != V2Message.V2MessageType.Broadcast).ToList();
		if (!news.Any())
			return null;
		string title = Type == V2RoomType.Sticky ? "Stickies" : $"{Language}-{Discriminator}";
		List<SlackBlock> blocks = new List<SlackBlock>()
		{
			new(SlackBlock.BlockType.HEADER, $"{title} | {ENVIRONMENT}.{Id}")
		};
		
		// Process normal (non-sticky) messages.
		V2Message[] nonStickies = news.Where(message => message.Type != V2Message.V2MessageType.Sticky).ToArray();
		if (nonStickies.Any())
		{
			string aid = nonStickies.First().AccountId;
			DateTime date = nonStickies.First().Date;
			string entries = "";

			void CreateBlock(V2Message msg)
			{
				blocks.Add(new SlackBlock(text: $"`{date:HH:mm}` *{Members.First(p => p == msg.AccountId)}*\n{entries}"));
				aid = msg.AccountId;
				date = msg.Date;
				entries = "";
			}

			foreach (V2Message msg in nonStickies)
			{
				if (msg.AccountId != aid)
				{
					// Track the built block, then reset the values to the next message author / date.
					// blocks.Track(new (
					// 	$"`{date:HH:mm}` *{AllMembers.First(p => p.AccountId == aid).SlackLink}*\n{entries}"
					// ));
					// aid = msg.AccountId;
					// date = msg.Date;
					// entries = "";
					CreateBlock(msg);
				}
				// Clean the text for Slack and add it to the entries.
				string clean = msg.Text.Replace('\n', ' ').Replace('`', '\'');
				string toAdd = msg.Type switch
				{
					V2Message.V2MessageType.Chat => clean,
					V2Message.V2MessageType.Broadcast => $"```Broadcast: {clean}```",
					_ => $":question: {clean}"
				} + '\n';
				if (SlackBlock.WouldOverflow(toAdd))
					Log.Error(Owner.Will, "Impossible to create a SlackBlock.  Text is too long.", data: new { ToAdd = toAdd });
				else if (SlackBlock.WouldOverflow(entries + toAdd))
					CreateBlock(msg);
				entries += toAdd;
			}
			blocks.Add(new (
				text: $"`{date:HH:mm}` *{Members.First(p => p == aid)}*\n{entries}"
			));
		}
		
		// Process the sticky messages
		blocks.AddRange((IsStickyRoom ? Messages : news)
			.Where(m => m.Type == V2Message.V2MessageType.Sticky)
			.Select(m => new SlackBlock(
				text: $"`Sticky Message on {m.Date:HH:mm}` > {m.Text}"
		)));

		return new SlackAttachment(SlackColor, blocks);
	}
	
	public class RoomEventArgs : EventArgs
	{
		public V2Message Message { get; private set; }

		public RoomEventArgs(V2Message msg) => Message = msg;
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RestSharp.Validation;
using Rumble.Platform.ChatService.Utilities;

namespace Rumble.Platform.ChatService.Models
{
	[BsonIgnoreExtraElements]
	public class Room
	{
		public const string KEY_ID = "id";
		public const string KEY_CAPACITY = "capacity";
		public const string KEY_CREATED_TIMESTAMP = "created";
		public const string KEY_GUILD_ID = "guildId";
		public const string KEY_LANGUAGE = "language";
		public const string KEY_MESSAGES = "messages";
		public const string KEY_MEMBERS = "members";
		public const string KEY_PREVIOUS_MEMBERS = "previousMembers";
		public const string KEY_TYPE = "type";
	
		public const string TYPE_GLOBAL = "global";
		public const string TYPE_DIRECT_MESSAGE = "dm";
		public const string TYPE_GUILD = "guild";
		public const string TYPE_UNKNOWN = "unknown";
		public const string TYPE_STICKY = "sticky";

		public const int MESSAGE_CAPACITY = 200;
		public const int GLOBAL_PLAYER_CAPACITY = 50;
		
		[BsonId, BsonRepresentation(BsonType.ObjectId)]
		public string Id { get; set; }
		[BsonElement(KEY_CAPACITY)]
		public int Capacity { get; set; }
		[BsonElement(KEY_CREATED_TIMESTAMP)]
		public long CreatedTimestamp { get; set; }
		[BsonElement(KEY_GUILD_ID), BsonIgnoreIfNull]
		public string GuildId { get; set; }
		[BsonElement(KEY_LANGUAGE)]
		public string Language { get; set; }
		[BsonElement(KEY_MESSAGES)]
		public List<Message> Messages { get; set; }
		[BsonElement(KEY_MEMBERS)]
		public HashSet<PlayerInfo> Members { get; set; }
		[BsonElement(KEY_PREVIOUS_MEMBERS)]
		public HashSet<PlayerInfo> PreviousMembers { get; set; }

		public Room ()
		{
			CreatedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
			Messages = new List<Message>();
			Members = new HashSet<PlayerInfo>();
			PreviousMembers = new HashSet<PlayerInfo>();
		}

		[BsonElement(KEY_TYPE)]
		public string Type { get; set; }

		/// <summary>
		/// Adds an account ID to the Room.  If the player already exists in the room, an AlreadyInRoomException is
		/// thrown.  If the Room is at capacity, a RoomFullException is thrown.
		/// </summary>
		/// <param name="playerInfo">A PlayerInfo object, parsed from a JToken.</param>
		/// <exception cref="AlreadyInRoomException">Indicates the account ID is already in the Room.</exception>
		/// <exception cref="RoomFullException">Indicates the Room is already full, or became full as the account was joining.</exception>
		public void AddMember(PlayerInfo playerInfo)
		{
			playerInfo.Validate();
			if (playerInfo.ScreenName == null)
				throw new InvalidPlayerInfoException("Screenname cannot be null.");
			if (Members.Any(m => m.AccountId == playerInfo.AccountId))
				throw new AlreadyInRoomException();
			PreviousMembers.RemoveWhere(m => m.AccountId == playerInfo.AccountId);
			if (Members.Count >= Capacity)
				throw new RoomFullException();
			Members.Add(playerInfo);
		}

		/// <summary>
		/// Adds a Message to the room.  If the author of the message is not a Member of the Room, a NotInRoomException
		/// is thrown.
		/// </summary>
		/// <param name="msg">The Message to add.</param>
		public void AddMessage(Message msg)
		{
			RequireMember(msg.AccountId);
			Messages.Add(msg);
			
			Messages = Messages.OrderBy(m => m.Timestamp).ToList();
			if (Messages.Count <= MESSAGE_CAPACITY)
				return;
			Messages.RemoveRange(0, Messages.Count - MESSAGE_CAPACITY);
			PreviousMembers.RemoveWhere(p => !Messages.Any(m => m.AccountId == p.AccountId));
		}
		/// <summary>
		/// Adds a Message to the room.  If the author of the message is not a Member of the Room, a NotInRoomException
		/// is thrown.
		/// </summary>
		/// <param name="msg">The Message to add.</param>
		/// <param name="lastReadTimestamp">The timestamp from the last read message of the client.</param>
		/// <returns>An IEnumerable of Messages.  Deprecated with the use of RoomUpdate.</returns>
		public IEnumerable<Message> AddMessage(Message msg, long lastReadTimestamp)
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
			RequireMember(accountId);
			PlayerInfo ciao = Members.First(m => m.AccountId == accountId);
			Members.Remove(ciao);
			if (Messages.Any(m => m.AccountId == ciao.AccountId))
				PreviousMembers.Add(ciao);
			// Members = Members.Where(m => m.AccountId != accountId).ToHashSet();
		}

		/// <summary>
		/// Wrapper for a LINQ query to look for an accountID in the Room.
		/// </summary>
		/// <param name="accountId">The account ID to look for.</param>
		/// <returns>True if the account is found, otherwise false.</returns>
		public bool HasMember(string accountId)
		{
			return Members.Any(m => m.AccountId == accountId);
		}

		/// <summary>
		/// Checks to make sure an account exists as a Member.  If the account ID isn't found, a NotInRoomException
		/// is thrown.
		/// </summary>
		/// <param name="accountId">The account ID to look for.</param>
		/// <exception cref="NotInRoomException">Indicates that the account does not exist in the given room.</exception>
		private void RequireMember(string accountId)
		{
			if (!HasMember(accountId))
				throw new NotInRoomException();
		}

		/// <summary>
		/// Retrieve all Messages that occur after a provided timestamp.  Used in generating RoomUpdates.
		/// </summary>
		/// <param name="timestamp">A Unix timestamp.</param>
		/// <returns>An IEnumerable of all Messages after the timestamp.</returns>
		public IEnumerable<Message> MessagesSince(long timestamp)
		{
			return Messages.Where(m => m.Timestamp > timestamp);
		}

		[BsonIgnore]
		public bool IsFull => Members.Count >= Capacity;

		public object ToResponseObject()
		{
			return new { Room = this };
		}

		public IEnumerable<Message> Snapshot(string messageId, int before, int after)
		{
			List<Message> ordered = Messages.OrderBy(m => m.Timestamp).ToList();
			int position = ordered.IndexOf(ordered.First(m => m.Id == messageId));
			int start = position > before ? position - before : 0;

			return ordered.Skip(start).Take(after + before);
		}
	}
}
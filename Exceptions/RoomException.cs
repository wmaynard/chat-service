using System.Text.Json.Serialization;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Exceptions
{
	public abstract class RoomException : PlatformException
	{
		[JsonInclude]
		public object Room { get; private set; }

		protected RoomException(Room room, string message) : base(message)
		{
			Room = new
			{
				Id = room.Id,
				Name = $"{room.Language}-{room.Discriminator}",
				Language = room.Language,
				OpenSpots = room.Vacancies
			};
		} // TODO: Track PlayerInfo to properties, take from subclasses
	}
}
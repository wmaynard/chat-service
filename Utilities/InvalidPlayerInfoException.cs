using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Utilities
{
	public class InvalidPlayerInfoException : RumbleException
	{
		public InvalidPlayerInfoException() : base("Invalid player information."){}
		public InvalidPlayerInfoException(string reason) : base($"Invalid player information. ({reason})"){}
	}
}
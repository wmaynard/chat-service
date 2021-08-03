using Rumble.Platform.Common.Exceptions;

namespace Rumble.Platform.ChatService.Utilities
{
	public class UserBannedException : RumbleException
	{
		public UserBannedException() : base("You are banned."){}
		public UserBannedException(string remaining) : base($"You are banned. (Time Remaining: {remaining})"){}
	}
}
using System;
using System.Runtime.Serialization;

namespace Rumble.Platform.ChatService.Utilities
{
	public class AuthException : Exception
	{
		public AuthException() : base(){}
		public AuthException(SerializationInfo info, StreamingContext context) : base(info, context){}
		public AuthException(string message) : base(message){}
		public AuthException(string message, Exception inner) : base(message, inner) {}
	}
}
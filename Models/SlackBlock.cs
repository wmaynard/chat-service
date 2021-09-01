using System;
using Newtonsoft.Json;

namespace Rumble.Platform.ChatService.Models
{
	public struct SlackBlockz
	{
		public enum BlockType { HEADER, DIVIDER, MARKDOWN }

		[JsonProperty]
		public string Type { get; set; }
		
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public object Text { get; set; }
		
		public SlackBlockz(BlockType type, string text = null)
		{
			Type = null;
			Text = null;
			switch (type)
			{
				case BlockType.HEADER:
					Type = "header";
					Text = new
					{
						Type = "plain_text",
						Text = text,
						Emoji = true
					};
					break;
				case BlockType.DIVIDER:
					Type = "divider";
					break;
				case BlockType.MARKDOWN:
					Type = "section";
					Text = new
					{
						Type = "mrkdwn",
						Text = text
					};
					break;
				default:
					Type = null;
					Text = null;
					break;
			}
		}
	}
}
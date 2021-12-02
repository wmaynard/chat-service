using System.Collections.Generic;
using System.Linq;
using Rumble.Platform.ChatService.Models;
using Rumble.Platform.Common.Utilities;
using Rumble.Platform.Common.Web;
using Rumble.Platform.CSharp.Common.Interop;

namespace Rumble.Platform.ChatService.Services
{
	public class RoomDespawnService : PlatformTimerService
	{
		public const int DESPAWN_THRESHOLD_SECONDS = 3_600;
		private readonly RoomService _roomService;
		private Dictionary<string, long> _lifeSupport;
		private int _roomsDestroyed;

		public RoomDespawnService(RoomService roomService) : base(intervalMS: 60_000)
		{
			_roomService = roomService;
			_lifeSupport = new Dictionary<string, long>();
			_roomsDestroyed = 0;

			_roomService.OnEmptyRoomsFound += TrackEmptyRooms;
		}

		internal void TrackEmptyRooms(object sender, RoomService.EmptyRoomEventArgs args)
		{
			// Keep the previous timestamps, but forget about rooms that now have users in them.
			// This way, we only clear rooms that have been empty for the duration of the timestamp
			// and seeing no activity.
			_lifeSupport = args.roomIds
				.ToDictionary(
					keySelector: id => id,
					elementSelector: id => _lifeSupport.TryGetValue(id, out long idleSince) ? idleSince : UnixTime
			);
		}

		protected override void OnElapsed()
		{
			string[] roomsToDestroy = _lifeSupport
				.Where(pair => UnixTime - pair.Value > DESPAWN_THRESHOLD_SECONDS)
				.Select(pair => pair.Key)
				.ToArray();

			if (!roomsToDestroy.Any())
				return;
			
			Log.Info(Owner.Default, "Deleting empty rooms", data: new
			{
				RoomIds = roomsToDestroy
			});
			foreach (string id in roomsToDestroy)
				_roomService.Delete(id);
			_roomsDestroyed += roomsToDestroy.Length;
			Graphite.Track("global-rooms-despawned", roomsToDestroy.Length, type: Graphite.Metrics.Type.FLAT);
			_lifeSupport.Clear();
		}

		public override object HealthCheckResponseObject => GenerateHealthCheck(new GenericData()
		{
			["roomsTracked"] = _lifeSupport.Count,
			["roomsDestroyed"] = _roomsDestroyed
		});
	}
}
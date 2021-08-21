# chat-service
 An API for in-game social messaging

# Introduction

The Chat Service does not differentiate between global chats, guild chats, direct messages (DMs), or any other variants.  After all, a DM is just a chat room with a limit of two people.  To minimize the number of requests, Chat returns a response containing all `RoomUpdates` with every interaction.  These `RoomUpdates` contain every unread message in every room a player is in, regardless of whether or not it's a DM, global chat, or any other room they're a member of.

Joining a room and sending a message alike should then be used to update client-side chats.

Rooms do not hold messages indefinitely.  The capacity is configurable in the constant `Room.MESSAGE_CAPACITY`.  When at capacity, any new messages will push old ones out.  For customer service purposes, `Reports` and `Snapshots` both create complete copies of data that are stored independently from this limit.

# Glossary

| Term | Definition |
| ---: | :---       |
| Account ID (aid) / gukey | An **aid** is a MongoDB-generated unique identifier used to differentiate between players. | 
| Ban / Squelch | A **ban** is an administrator-issued restriction on a player's ability to interact with the service.  **Bans** may be timed or permanent.  Unlike **muting** a player, a **ban** stops them at the service-level from sending **messages**.  **Bans** do not affect **broadcasts**. |
| Broadcast | A **broadcast** is a server-generated **message**.  It has special formatting and is programmatically created for qualifying **broadcast events**, such as summoning a very rare hero or completing certain quests. |
| Chat | The `chat-service` project. |
| Discriminator | A semi-unique identifier for a player consisting of a randomly-generated 4-digit number.  This is not directly modifiable by the player, but is generated by `player-service`. |
| Global Room | A special type of **room**, and the most public-facing ones.  Chat V1 consists solely of a single **global room**.  In the future, **global rooms** will **auto-scale**: upon reaching capacity, a new **global room** with the same language will be created.  Similarly, when a room is empty for a long enough time (TBD), it will be destroyed.
| Message | A **message** is a snippet of text or data sent by a player or generated by the server, such as in the case of **broadcasts**.  For communication purposes, however, **message** should generally refer to player-created content unless otherwise specified. |
| Mute | **Muting** a player is merely an update to their preferences.  The game client needs to actually hide messages from **muted** players. |
| PlayerInfo | To reduce bottlenecks, the chat service stores a copy of relevant player data for its own purposes.  This includes the player's avatar, screenname, discriminator, level, and power.  Other player data may be added as chat evolves.|
| Report | Refers to both the action of flagging an offensive **message** and the saved record of those **messages** and involved players.   Reports are viewable from the `publishing-app` and links to them are dumped into the (TBD) Slack channel. | 
| Response Object | Platform-specific term for a standard data delivery vehicle.  Room information, for example, should always be returned in a JSON key / value pair like `"room": { /* ... */ }`.  The key should match the name of the class, and is handled through reflection via the `RumbleModel` class.
| Room | All chat features can be broken down into **Rooms** of different types.  For example, a **direct message** is no different from a **global room** except for the fact that it can only have two members.  Other types of **room** include **guild** and **sticky**. |
| Screenname / Username | The user-generated component for a friendly, readable name.  For example, if you see `JoeMcFugal#2006` in chat, the **screenname** is "JoeMcFugal".
| Snapshot | When a player is **banned** by an administrator, a complete record of all of their **rooms** is created.  This may not necessarily include any offensive content; if the player's **messages** have fallen off from the **room**, the **snapshot** may be entirely innocent.  However, if administrators issued a **ban** from a **report**, a copy of that **report** will be included for historical purposes.
| Sticky | A **sticky** or **sticky message** is a special kind of **message** that lives in its own, non-joinable **room**.  These **messages** are only viewable for a specific time period and can be used to promote events or send otherwise special server updates to players.
| Token | An `Authorization` header in HTTP requests.  Currently, **tokens** are issued by `player-service`.  They should be included in the format `Bearer {token string}`. |
| Unmute | The counterpart to **mute**.  **Unmuting** a player removes them from the **muted** players list. | 

# Consuming The Service
Every chat-related `POST` endpoint requires the following:
* An `Authorization` header with a value set to `Bearer {token}`, where the token is issued from `player-service`.
    * This is used to authenticate the client via `player-service`'s `/player/verify` endpoint.
    * Tokens contain the user's identification in them, including **account ID**, **username**, and **discriminator**. 
* A `lastRead` field, which is the Unix timestamp of the most recently-read message.
    * On the client-side, store a previous `lastRead` timestamp to send with future requests rather than sending the system's local timestamp.  This guarantees you won't miss messages.
    * While `lastRead` _can_ be `0`, this will return _all_ messages in _every_ room the player is in and is strongly discouraged.

Every JSON response contains an object that can be used to update the client, as in the sample below:

    {
        ...
        "roomUpdates": [
            {
                "id": "60e8d73536134314d16b207a",
                "unreadMessages": [
                    {
                        "id": "eda93b9a-3862-4a6f-9a73-a02fd3408a95",
                        "text": "Anger leads to hate.",
                        "timestamp": 1625872345,
                        "type": "chat",
                        "accountId": "5f727b4dc60f5a956eb1c551"
                    },
                    {
                        "id": "2eaee976-e769-4076-bdd5-d55bf6b64d70",
                        "text": "Hate leads to suffering!",
                        "timestamp": 1625872381,
                        "type": "chat",
                        "accountId": "5f727b4dc60f5a956eb1c551"
                    }
                ],
                "members": [
                    {
                        "aid": "5f727b4dc60f5a956eb1c551",
                        "avatar": "demon_axe_thrower",
                        "memberSince": 1626242762,
                        "sn": "DoktorNik",
                        "screenName": "Slartibartfast",
                        "level": 17,
                        "power": 9001,
                        "discriminator": 4539
                    },
                    {
                        "accountId": "60a43b0c70edc8aa7cf3bed6",
                        "avatar": "demon_axe_thrower",
                        "inRoomSince": 1626245489,
                        "screenName": "Arthur Dent"
                        "level": 6,
                        "power": 61,
                        "discriminator": 8039
                    }
                ],
                "previousMembers": []
            },
            {
                "id": "60ee83aa734d06135565dda7",
                "unreadMessages": [],
                "members": [
                    {
                    "accountId": "5f727b4dc60f5a956eb1c551",
                    "avatar": "demon_axe_thrower",
                    "inRoomSince": 0,
                    "screenName": "Slartibartfast"
                    }
                ]
            }
        ]
        ...
    }
In this sample, two separate `RoomUpdates` are returned.  In the first `Room`, we have two unread messages that have not been seen by the client.  The `members` field contains a list of all of a room's members so that messages can be linked to appropriate accounts, avatars, and screennames.  More data may be included in this as Chat evolves.

In the second room, there are no new messages.

# Endpoints

All chat endpoints can be accessed via the `/chat/` base API route.

### Example Flow

This is an example flow, as imagined by Platform, but is subject to change for implementation.

1. When the game client starts up, `/launch` is called.  This retrieves more data than any other request, but includes everything necessary to initialize chat, show new messages, or display important information to the user such as stickies or ban information.
    * When the response is received, a **data handler** stores the `unreadMessages` locally, along with their timestamps.  The largest timestamp (most recent) is stored for future requests.  This handler should take care of every response from chat.
2. The game client starts a **timer** in the background.  When the timer **elapses**, `/messages/unread` is called to look for `roomUpdates`.  It is sent with the `lastRead` timestamp in the body, so that only newer messages are returned, if any.
3. The player enters a message into the chat client.  `/messages/send` is called, again with the `lastRead` parameter.  The **timer** is reset, as this call also will return with all `roomUpdates` for the player.
4. The player enters a screen where chat is not visible.  The **timer** is paused to prevent unnecessary traffic to the service.
5. The player enters a screen where chat is accessible once again.  The **timer** is resumed. 
6. The player quits the game.  `/rooms/global/leave` is called (or, alternatively, `/rooms/leave`, with a `roomId`).  This prevents offline `aid`s from flooding the room on the data side.

## Top Level

Mentioned first because of `/launch`: this endpoint is intended to be the first call to the chat service from the client.

| Method  | Endpoint  | Description | Required Parameters | Optional Parameters |
| ---:    | :---      | :---        | :---                | :---                |
| GET | `/health` | Health check; returns the status of ALL services: `banService`, `reportService`, `roomService`, and `settingsService`.  Required by the AWS Load balancer. | |
| POST | `/launch` | Launches the current player into chat.  This endpoint adds the user to a global room for their language, returns any current **sticky messages**, **bans**, **settings**, and **room updates** for the player. | `language`<br />`lastRead`<br />`playerInfo` | |

## Admin

All `Admin` endpoints other than `/admin/health` require a valid **admin token**.  For publishing app, this can be found in the dynamic config, using the `chatToken` value.  The `AdminController` class is responsible for anything requiring elevated privileges.

| Method  | Endpoint  | Description | Required Parameters | Optional Parameters |
| ---:    | :---      | :---        | :---                | :---                |
| GET | `/admin/health` | Health check; returns the status of the `BanService` and `RoomService`. | | |
| GET | `/admin/ban/list` | Lists all **bans** for all users, including expired **bans**. | | |
| GET | `/admin/rooms/list` | Lists all **rooms** and all data associated with the **rooms**.  Once live, this will probably need to be trimmed to just room IDs and basic metrics. | | |
| POST | `/admin/ban/lift` | Removes a specific **ban**, provided its ID. | `banId` | |
| POST | `/admin/ban/player` | Issues a **ban** against a player.  Can be temporary (timed) or indefinite. | `aid`<br />`reason` | `durationInSeconds`<br />`reportId` |
| POST | `/admin/messages/sticky` | Creates a **sticky message**. | `message` | `language` |
| POST | `/admin/messages/unsticky` | Deletes a **sticky message**. | `messageId` | |
| POST | `/admin/reports/ignore` | Marks a **report** as *benign*.  This does not delete the **report**, but is an indicator that it should be kept for archival purposes. | `reportId` | |
| POST | `/admin/reports/delete` | Deletes a **report**.  For clearly harmless **reports** that serve no useful purpose. | `reportId` | |
| POST | `/admin/messages/delete` | Deletes a **message**.  It would be ideal to never use this. | `messageIds`<br />`roomId` | |

## Debug

All `Debug` endpoints other than `/debug/health` are encapsulated by conditional compilation (`#if DEBUG`).  While the endpoints are not secured, they will not be available when deployed.  Once Chat is fully featured and stable, the `DebugController` should be deleted.

| Method  | Endpoint  | Description | Required Parameters | Optional Parameters |
| ---:    | :---      | :---        | :---                | :---                |
| GET | `/debug/health` | Health check; returns the status of the `RoomService` and the `SettingsService`. | | |
| POST | `/debug/rooms/clear` | Clears all `Members`, `PreviousMembers`, and `Messages` from all **rooms**. | | |
| POST | `/debug/rooms/join` | Joins the user to a **room**.  Does not leave any previous **rooms**. | `playerInfo`<br />`roomId` | | |
| POST | `/debug/settings/globalUnmute` | **Unmutes** all players from all accounts. | | |
| POST | `/debug/rooms/nuke` | Destroys all rooms.  Returns the number of destroyed **rooms**. | | |
| POST | `/debug/settings/nuke` | Destroys all user settings. | | |

## Messages

| Method  | Endpoint  | Description | Required Parameters | Optional Parameters |
| ---:    | :---      | :---        | :---                | :---                |
| GET | `/messages/health` | Health check; returns the status of the `ReportService` and the `RoomService`. | | |
| POST | `/messages/broadcast` | Sends a **broadcast**. | `aid`<br />`lastRead`<br />`message` | |
| POST | `/messages/report` | Reports a **message** for abuse.  | `lastRead`<br />`messageId`<br />`roomId` | |
| POST | `/messages/send` | Sends a **message** to a **room**.  | `lastRead`<br />`message`<br />`roomId`| |
| POST | `/messages/unread` | Retrieves all unread **messages**. | `lastRead` | |
| POST | `/messages/sticky` | Retrieves **sticky messages**. | `lastRead` | `all` |

#### Notes

* The `message` parameter is an object containing a string, `text`.  There are other fields that can be included in a `message` object, but none are useful in the context of the `MessageController`.


## Rooms

| Method  | Endpoint  | Description | Required Parameters | Optional Parameters |
| ---:    | :---      | :---        | :---                | :---                |
| GET | `/rooms/health` | Health check; returns the status of the `RoomService`. | | |
| POST | `/rooms/available` | Returns a list of available **room** for the player, as dictated by the `language` parameter. | `lastRead` | |
| POST | `/rooms/leave` | Leaves a **room**, as specified by its ID. | `lastRead`<br />`roomId` | |
| POST | `/rooms/list` | Lists all the **rooms** and all their data the user is currently in. | `lastRead` | |
| POST | `/rooms/global/join` | Joins the next available **global room**, as dictated by the `language` parameter. | `language`<br />`lastRead`<br />`playerInfo`| `roomId` |
| POST | `/rooms/global/leave` | Leaves all **global rooms** the player is currently in. This *should* only be one room, but if the client closed unexpectedly it may be more than one. | `lastRead` | |
| POST | `/rooms/update` | Updates a player's information across all of their **rooms**. | `lastRead`<br />`playerInfo` | |

#### Notes

* `/rooms/global/join` requires a **language** to be specified.  While a `roomId` can be passed in to join a specific global room, if that `roomId` is tied to a different **language**, the request will fail.  The request will also fail if the room is full.

## Settings

The `SettingsController` is responsible for storing a user's chat-specific settings.  Right now, this is limited to muted players, but could conceivably be used for other features, such as storing custom profanity filters, starred players, etc.

| Method  | Endpoint  | Description | Required Parameters | Optional Parameters |
| ---:    | :---      | :---        | :---                | :---                |
| GET | `/settings/health` | Health check; returns the status of the `SettingsService`. | | |
| GET | `/settings` | Returns all the settings for the current player. | |
| POST | `/settings/mute` | **Mutes** another player. | `playerInfo` | |
| POST | `/settings/unmute` | **Unmutes** a previously **muted** player. | `playerInfo` | | |

## Project Maintenance

* Every set of related endpoints should be contained in its own **controller** class that inherits from `RumbleController` unless there's a good reason to fragment it (such as the `AdminController`, which contains all endpoints that require elevated permissions).
* Every `MongoDB collection` used in the project should have its own **service** class and should inherit from `RumbleMongoService`.  Examples include `BanService` and `RoomService`.
* Every data **model** should inherit from `RumbleModel`.
* Every **model** should contain two sets of constant keys for each property.  Any space savings in MongoDB will be significant with a global launch, whether that's a 5KB savings in network traffic for the client or 500MB on the storage side.
  * A `DB_KEY`: an abbreviated or other shorthand string for storage in MongoDB.
  * A `FRIENDLY_KEY`: a more verbose key, used for parameter parsing (incoming traffic)  and response serialization (outgoing traffic).
  * These can be set for each property separately, with the `DB_KEY` specified in `[BsonElement]` attributes and the `FRIENDLY_KEY` specified in `[JsonProperty]` attributes.
* Any data sent back to a client should be contained in a **response object**.  Unless overridden, any `RumbleModel` has a `ResponseObject` that can be used.  This uses reflection to generate a JSON key / value pair of the class name and object data, and helps keep responses standardized.
    * When sending collections of models back (such as a List of messages), use the `RumbleController`'s `CollectionResponseObject(IEnumerable<T> objects)` method instead.

## Future Updates, Optimizations, and Nice-to-Haves

* Requests to Chat could include `Room` accountIds so that Chat can return only new members / members that have left the room, reducing data load.  Returning partial information could yield substantial data savings on every request.  The best method may be to have the client send known accountIds, then the service could return who has left the room and any new members.
* (Frontend) Thresholds could be set so that when chat is fairly inactive - perhaps defined by a metric like `messages / minute` - the **update timer** takes longer to fire requests off.  Conversely, if chat is *very* active, it could instead be increased.
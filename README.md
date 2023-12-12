# Chat V2

 An API for in-game social messaging - now new and improved!

## Introduction

Chat V1 was a particularly interesting Platform project - because it predated platform-common, it had a lot of wild west-style code and a healthy amount of duct tape binding it together.  It was lightweight, responsive, and never complained about its RPS (requests per second), though, so despite the stress we hit it with, it actually performed quite well.

However, it had some significant downfalls.  It was very difficult to maintain, not being built on the tooling that most of Platform relies on now, and adding new features such as guild chat was not going to be clean or easy.

This guide will walk you through how to use the second iteration of chat.  There is only one guiding principle that carries over from V1...

## Every Request Returns Unread Messages

Any endpoint that a game client or other token representing a player hits the chat-service, Platform will return unread messages for the player.  This helps keep the traffic minimal.  For _every single request_ made to Chat, the consuming client should:

* Include a UTC Unix timestamp in the body or parameters (as appropriate) with the key of `lastRead`.
* Find the maximum timestamp returned in the unread messages and store it for use in the next request to Chat.

It's important to state this right at the beginning as it's a critical point to both a performant client and service.

## Chat Is Now Simpler

V1 was designed to match previous groovy practices, but also tried to provide endpoints that looked like function names, all with POST methods.  We had a `/launch`, `/settings`, `/settings/mute`, `/settings/unmute`, and a dozen other endpoints that had to be juggled.  V2 needed to add support for many more types of rooms than just global chat - such as preparing for guilds and/or DMs - and the number of endpoints was only going to explode.

Now, we have the following:

* Client
  * GET / - returns unreads
  * GET /globals - lists **available** global rooms (those that aren't full)
  * POST /message - Sends a message to a room; client-authoritative*.
  * PUT /preferences - Stores client-side preferences, such as a squelch list.
  * POST /report - Creates a chat report that notifies CS something is amiss.
* Admin
  * PUT /admin/message - Replaces a message.  This is useful when interacting with **message context**, which will be useful later.
  * POST /admin/broadcast - Same as before?
  * PATCH /admin/report - Resolve or delete a report
  * GET /admin/room - View all messages for a specific room
  * GET /admin/player - View messages a player has sent; and messages around theirs, grouped by room.
  * POST /admin/announcement
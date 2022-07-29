namespace Shared

open System
open ErrorHandling

type Player = {
    Id: Guid
    JoinedAt: DateTime
    Name: string
}

[<RequireQualifiedAccess>]
module Player =
    let anonymous = {
        Id = Guid.NewGuid()
        JoinedAt = DateTime.Now
        Name = "Fox"
    }

    let create = function
        | empty when empty |> String.IsNullOrWhiteSpace -> None
        | name ->
            Some {
                Id = Guid.NewGuid()
                JoinedAt = DateTime.Now
                Name = name
            }

type ChatMessage = {
    Author: Guid
    Created: DateTime
    Message: string
}

type ErrorMessage = string

type PlayerRequest<'Data> = PlayerRequest of Guid * 'Data
type PlayerAction<'Data, 'Response> = PlayerRequest<'Data> -> AsyncResult<'Response, ErrorMessage>

[<RequireQualifiedAccess>]
module PlayerAction =
    let request player data = PlayerRequest (player, data)

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

    let webSocket = "/socket"

type IGameApi = {
    newPlayer: string -> Async<Player>
    logout: Guid -> Async<unit>
    loadPlayer: Guid -> Async<Player option>
    getPlayers: unit -> Async<Player list>
    changeCurrentPlayerName: PlayerAction<string, unit>

    getChat: DateTime -> Async<ChatMessage list>
    sendChatMessage: PlayerAction<ChatMessage, unit>
}

// Message from the client, telling the server that a new post is added
[<RequireQualifiedAccess>]
type RemoteClientMsg =
    | PlayerChanged
    | PlayerLoggedOut
    | ChatMessageAdded of DateTime

// Message from the server, telling the client to reload posts
[<RequireQualifiedAccess>]
type RemoteServerMsg =
    | RefreshPlayers
    | RefreshChat of DateTime

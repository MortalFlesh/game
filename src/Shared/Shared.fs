namespace Shared

open System
open ErrorHandling

type Player = {
    Id: Guid
    Name: string
}

[<RequireQualifiedAccess>]
module Player =
    let anonymous = {
        Id = Guid.NewGuid()
        Name = "Fox"
    }

    let create = function
        | empty when empty |> String.IsNullOrWhiteSpace -> None
        | name ->
            Some {
                Id = Guid.NewGuid()
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
    loadPlayer: Guid -> Async<Player>
    getPlayers: unit -> Async<Player list>
    changeCurrentPlayerName: PlayerAction<string, unit>

    getChat: DateTime option -> Async<ChatMessage list>
    sendChatMessage: PlayerAction<ChatMessage, unit>
}

// Message from the client, telling the server that a new post is added
[<RequireQualifiedAccess>]
type RemoteClientMsg =
    | PlayerChanged
    | ChatMessageAdded of DateTime

// Message from the server, telling the client to reload posts
[<RequireQualifiedAccess>]
type RemoteServerMsg =
    | RefreshPlayers
    | RefreshChat of DateTime

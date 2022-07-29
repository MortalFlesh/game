namespace Shared

open System

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

module Route =
    let builder typeName methodName =
        sprintf "/api/%s/%s" typeName methodName

    let webSocket = "/socket"

type IGameApi = {
    newPlayer: string -> Async<Player>
    loadPlayer: Guid -> Async<Player>
    getPlayers: unit -> Async<Player list>
    changeCurrentPlayerName: Guid * string -> Async<unit>
}

// Message from the client, telling the server that a new post is added
[<RequireQualifiedAccess>]
type RemoteClientMsg =
    | PlayerChanged

// Message from the server, telling the client to reload posts
[<RequireQualifiedAccess>]
type RemoteServerMsg =
    | RefreshPlayers

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

type IGameApi = {
    newPlayer: string -> Async<Player>
    loadPlayer: Guid -> Async<Player>
    getPlayers: unit -> Async<Player list>
    changeCurrentPlayerName: Guid * string -> Async<unit>
}

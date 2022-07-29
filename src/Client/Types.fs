module Types

open Shared

type Msg =
    | SetInput of string
    | ChangeName
    | PlayerChanged
    | InitGame of Player
    | ReloadPlayers
    | GotPlayers of Player list
    | ServerMsg of RemoteServerMsg

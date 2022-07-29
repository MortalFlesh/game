module Types

open System
open Shared

type Msg =
    | SetName of string
    | ChangeName
    | PlayerChanged
    | InitGame of Player option
    | ReloadPlayers
    | GotPlayers of Player list

    | Logout
    | PlayerLoggedOut

    | ReloadChat of DateTime
    | GotChat of ChatMessage list
    | SetChatMessage of string
    | SendChatMessage
    | ChatMessageSent of DateTime

    | ServerMsg of RemoteServerMsg

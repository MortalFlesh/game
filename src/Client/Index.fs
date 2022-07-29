module Index

open System
open Elmish
open Elmish.Bridge
open Fable.Remoting.Client

open Shared
open Types
open Storage

[<AutoOpen>]
module Utils =
    let inline (<?=>) o d = o |> Option.defaultValue d
    let inline (<?**>) o (withSome, withNone) =
        match o with
        | Some _ -> withSome
        | None -> withNone

type Model = {
    Players: Player list
    CurrentPlayer: Guid option
    Input: string

    Chat: ChatMessage list
    ChatMessage: string
}

[<RequireQualifiedAccess>]
module Model =
    let currentPlayer = function
        | { Players = [] } -> None
        | { CurrentPlayer = Some id; Players = players } -> players |> Seq.tryFind (fun p -> p.Id = id)
        | _ -> None

    let playerAction data = currentPlayer >> Option.map (fun { Id = id } -> PlayerAction.request id data)

    let storePlayer player =
        SessionStorage.save Key.Player player.Id

    let loadPlayer () =
        match SessionStorage.loadItem Key.Player with
        | Some loadedId ->
            match loadedId.Trim '"' |> Guid.TryParse with
            | true, id -> Some id
            | _ -> None
        | _ -> None

    let clearPlayer () =
        SessionStorage.delete Key.Player

    let playerName model id =
        model.Players
        |> List.tryFind (fun p -> p.Id = id)
        |> Option.map (fun p -> p.Name)
        |> Option.defaultValue "*Already gone*"

let gameApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IGameApi>

let init () : Model * Cmd<Msg> =
    let model = {
        Players = []
        CurrentPlayer = None
        Input = ""

        Chat = []
        ChatMessage = ""
    }

    let currentPlayerIdCmd =
        match Model.loadPlayer () with
        | Some id -> Cmd.OfAsync.perform gameApi.loadPlayer id InitGame
        | _ -> Cmd.none

    model, Cmd.batch [
        Cmd.OfAsync.perform gameApi.getPlayers () GotPlayers
        currentPlayerIdCmd
    ]

let update (msg: Msg) (model: Model) : Model * Cmd<Msg> =
    let changePlayerName model cmd =
        { model with Input = "" }, cmd

    match model, msg with
    | _, SetName value -> { model with Input = value }, Cmd.none

    | { CurrentPlayer = None }, ChangeName ->
        Cmd.OfAsync.perform gameApi.newPlayer model.Input (Some >> InitGame)
        |> changePlayerName model

    | { CurrentPlayer = Some id }, ChangeName ->
        Cmd.OfAsync.perform gameApi.changeCurrentPlayerName (PlayerAction.request id model.Input) (fun _ -> PlayerChanged)
        |> changePlayerName model

    | _, PlayerChanged ->
        Bridge.Send(RemoteClientMsg.PlayerChanged)
        model, Cmd.none

    | _, InitGame None ->
        Model.clearPlayer()
        model, Cmd.none

    | _, InitGame (Some player) ->
        Model.storePlayer player

        { model with CurrentPlayer = Some player.Id },
        Cmd.batch [
            Cmd.ofMsg PlayerChanged
            Cmd.OfAsync.perform gameApi.getChat player.JoinedAt GotChat
            Cmd.ofMsg NotifyPlayerStatus
        ]
    | { CurrentPlayer = Some id }, NotifyPlayerStatus ->
        model, Cmd.OfAsync.perform gameApi.notifyPlayerStatus (PlayerAction.request id ()) (fun _ -> PlayerStatusNotified)

    | _, PlayerStatusNotified ->
        model, Cmd.OfAsync.result (async {
            do! Async.Sleep (10 * 1000)
            return NotifyPlayerStatus
        })

    | { CurrentPlayer = Some id }, Logout -> { model with CurrentPlayer = None }, Cmd.OfAsync.perform gameApi.logout id (fun _ -> PlayerLoggedOut)
    | _, PlayerLoggedOut ->
        Bridge.Send(RemoteClientMsg.PlayerLoggedOut)
        model, Cmd.none

    | _, ReloadPlayers -> model, Cmd.OfAsync.perform gameApi.getPlayers () GotPlayers
    | _, GotPlayers players -> { model with Players = players }, Cmd.none

    | _, ReloadChat from -> model, Cmd.OfAsync.perform gameApi.getChat from GotChat
    | _, GotChat chat -> { model with Chat = model.Chat @ chat |> List.distinct }, Cmd.none
    | _, SetChatMessage message -> { model with ChatMessage = message }, Cmd.none
    | { CurrentPlayer = Some id; ChatMessage = message }, SendChatMessage when message |> String.IsNullOrWhiteSpace |> not ->
        let message = {
            Author = id
            Created = DateTime.Now
            Message = message
        }
        model, Cmd.OfAsync.perform gameApi.sendChatMessage (PlayerAction.request id message) (fun _ -> ChatMessageSent message.Created)
    | _, SendChatMessage _ -> model, Cmd.none
    | _, ChatMessageSent sentAt ->
        Bridge.Send(RemoteClientMsg.ChatMessageAdded sentAt)
        { model with ChatMessage = "" }, Cmd.none

    // WS messages
    | _, ServerMsg RemoteServerMsg.RefreshPlayers -> model, Cmd.ofMsg ReloadPlayers
    | _, ServerMsg (RemoteServerMsg.RefreshChat from) -> model, Cmd.ofMsg (ReloadChat from)

    // Fallback
    | _, _ -> model, Cmd.none

open Feliz
open Feliz.Bulma
open Fulma

let navBrand =
    Bulma.navbarBrand.div [
        Bulma.navbarItem.a [
            navbarItem.isActive
            prop.children [
                Html.h1 [
                    prop.text "A Game"
                ]
            ]
        ]
    ]

let playerBox (model: Model) (dispatch: Msg -> unit) =
    let currentPlayer = model |> Model.currentPlayer

    Bulma.box [
        match currentPlayer with
        | Some player ->
            Bulma.media [
                Bulma.mediaContent [
                    Bulma.content [
                        Html.h1 [ prop.text player.Name ]
                    ]
                ]
                Bulma.mediaRight [
                    Bulma.control.p [
                        Bulma.button.a [
                            color.isDanger
                            prop.onClick (fun _ -> dispatch Logout)
                            prop.text "Logout"
                        ]
                    ]
                ]
            ]
        | _ -> ()

        Bulma.field.div [
            field.isGrouped
            prop.children [
                Bulma.control.p [
                    control.isExpanded
                    prop.children [
                        Bulma.input.text [
                            prop.value model.Input
                            prop.placeholder (currentPlayer <?**> ("Change your name", "Choose your name"))
                            prop.onChange (SetName >> dispatch)
                        ]
                    ]
                ]
                Bulma.control.p [
                    Bulma.button.a [
                        color.isPrimary
                        prop.disabled (model.Input |> String.IsNullOrWhiteSpace)
                        prop.onClick (fun _ -> dispatch ChangeName)
                        prop.text (currentPlayer <?**> ("Change name", "Join"))
                    ]
                ]
            ]
        ]
        Bulma.content [
            match model.Players with
            | [] -> Html.h2 [ prop.text "No players yet" ]
            | players ->
                Html.ol [
                    for player in players do
                        Html.li [ prop.text player.Name ]
                ]
        ]
    ]

let chatBox (model: Model) (dispatch: Msg -> unit) =
    let currentPlayer = model |> Model.currentPlayer

    let chatMessageCard (authorName: Guid -> string) (message: ChatMessage) =
        Bulma.content [
            Bulma.media [
                Html.strong [ prop.text (authorName message.Author) ]
                Bulma.mediaRight [
                    prop.text (message.Created.ToString("HH:mm:ss yyyy-MM-dd"))
                ]
            ]
            Html.pre [ prop.text message.Message ]
        ]

    Bulma.box [
        Bulma.content [
            match model.Chat with
            | [] -> Html.h3 [ prop.text "No messages yet" ]
            | messages ->
                for message in messages do
                    chatMessageCard (Model.playerName model) message
        ]

        match currentPlayer with
        | Some _ ->
            Bulma.field.div [
                field.isGrouped
                prop.children [
                    Bulma.control.p [
                        control.isExpanded
                        prop.children [
                            Bulma.input.text [
                                prop.value model.ChatMessage
                                prop.placeholder "Message ..."
                                prop.onChange (SetChatMessage >> dispatch)
                            ]
                        ]
                    ]
                    Bulma.control.p [
                        Bulma.button.a [
                            color.isPrimary
                            prop.disabled (model.ChatMessage |> String.IsNullOrWhiteSpace)
                            prop.onClick (fun _ -> dispatch SendChatMessage)
                            prop.text "Send message"
                        ]
                    ]
                ]
            ]
        | _ -> ()
    ]

let view (model: Model) (dispatch: Msg -> unit) =
    Bulma.hero [
        hero.isFullHeight
        color.isPrimary
        prop.style [
            style.backgroundSize "cover"
            style.backgroundImageUrl "https://unsplash.it/1200/900?random"
            style.backgroundPosition "no-repeat center center fixed"
        ]
        prop.children [
            Bulma.heroHead [
                Bulma.navbar [
                    Bulma.container [ navBrand ]
                ]
            ]
            Bulma.heroBody [
                Bulma.container [
                    Bulma.column [
                        column.is8
                        column.isOffset2
                        prop.children [
                            Bulma.title [
                                text.hasTextCentered
                                prop.text "game"
                            ]
                            playerBox model dispatch
                            chatBox model dispatch
                        ]
                    ]
                ]
            ]
        ]
    ]
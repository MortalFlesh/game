module Index

open Elmish
open Fable.Remoting.Client
open Shared
open System
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
}

[<RequireQualifiedAccess>]
module Model =
    let currentPlayer = function
        | { Players = [] } -> None
        | { CurrentPlayer = Some id; Players = players } -> players |> Seq.tryFind (fun p -> p.Id = id)
        | _ -> None

    let storePlayer player =
        SessionStorage.save Key.Player player.Id

    let loadPlayer () =
        match SessionStorage.loadItem Key.Player with
        | Some loadedId ->
            match loadedId.Trim '"' |> Guid.TryParse with
            | true, id -> Some id
            | _ -> None
        | _ -> None

type Msg =
    | SetInput of string
    | ChangeName
    | InitGame of Player
    | ReloadPlayers
    | GotPlayers of Player list

let gameApi =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.buildProxy<IGameApi>

let init () : Model * Cmd<Msg> =
    let model = {
        Players = []
        CurrentPlayer = None
        Input = ""
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
    match msg with
    | SetInput value -> { model with Input = value }, Cmd.none
    | ChangeName ->
        { model with Input = "" },
        match model with
        | { CurrentPlayer = Some id } -> Cmd.OfAsync.perform gameApi.changeCurrentPlayerName (id, model.Input) (fun _ -> ReloadPlayers)
        | _ -> Cmd.OfAsync.perform gameApi.newPlayer model.Input InitGame
    | InitGame player ->
        Model.storePlayer player

        { model with CurrentPlayer = Some player.Id }, Cmd.ofMsg ReloadPlayers
    | ReloadPlayers -> model, Cmd.OfAsync.perform gameApi.getPlayers () GotPlayers
    | GotPlayers players -> { model with Players = players }, Cmd.none

open Feliz
open Feliz.Bulma

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

let containerBox (model: Model) (dispatch: Msg -> unit) =
    let currentPlayer = model |> Model.currentPlayer

    Bulma.box [
        match currentPlayer with
        | Some player ->
            Bulma.content [
                Html.h1 [ prop.text player.Name ]
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
                            prop.onChange (fun value -> SetInput value |> dispatch)
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
            Bulma.control.p [
                Bulma.button.a [
                    color.isPrimary
                    prop.onClick (fun _ -> dispatch ReloadPlayers)
                    prop.text "Refresh"
                ]
            ]

            match model.Players with
            | [] -> Html.h2 [ prop.text "No players yet" ]
            | players ->
                Html.ol [
                    for player in players do
                        Html.li [ prop.text player.Name ]
                ]
        ]
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
                            containerBox model dispatch
                        ]
                    ]
                ]
            ]
        ]
    ]
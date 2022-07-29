module Server

open System
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open Giraffe
open Elmish
open Elmish.Bridge

open Shared
open ErrorHandling

// server state is what the server keeps track of
type ServerState = Nothing

// the server message is what the server reacts to
// in this case, it reacts to messages from client
type ServerMsg = ClientMsg of RemoteClientMsg

// The postsHub keeps track of connected clients and has broadcasting logic
let actionsHub = ServerHub<ServerState, ServerMsg, RemoteServerMsg>().RegisterServer(ClientMsg)

type ServerPlayer =
    {
        Player: Player
        Checked: DateTime
    }
    with
        member this.Id = this.Player.Id

[<RequireQualifiedAccess>]
module ServerPlayer =
    let id { Player = { Id = id } } = id
    let player { Player = player } = player

module Storage =
    let players: ResizeArray<ServerPlayer> = ResizeArray()
    let chat: ResizeArray<ChatMessage> = ResizeArray()

    let addPlayer player = players.Add player
    let addMessage message = chat.Add message

    let checkPlayers = async {
        try
            let info players =
                let now = DateTime.Now

                players
                |> Seq.toList
                |> List.map (fun p ->
                    sprintf "[P:%A] Joined: %A | Idle: %As" p.Player.Name p.Player.JoinedAt (now - p.Checked).Seconds
                )
                |> printfn "Currently %A"

            let toRemove =
                players
                |> tee info
                |> Seq.filter (fun player -> player.Checked < DateTime.Now.Subtract(TimeSpan.FromMinutes(1)))
                |> List.ofSeq
                |> tee (printfn "Remove: %A")

            let refresh =
                toRemove
                |> List.fold (fun acc toRemove -> acc || players.Remove toRemove) false

            if refresh then
                printfn "[Player left] -> Refresh"
                actionsHub.BroadcastClient RemoteServerMsg.RefreshPlayers
        with e -> printfn "Check error: %A" e
    }

[<RequireQualifiedAccess>]
module Authorize =
    let authorizeAsyncResult action: PlayerAction<'Data, 'Response> = fun (PlayerRequest (id, data)) -> asyncResult {
        let! player =
            Storage.players
            |> Seq.tryFind (fun player -> player.Id = id)
            |> Result.ofOption "Player not found"

        return! action player data
    }

    let authorizeAsync action = authorizeAsyncResult (fun player -> action player >> AsyncResult.ofAsyncCatch (sprintf "%A"))
    let authorizeResult action = authorizeAsyncResult (fun player -> action player >> AsyncResult.ofResult)
    let authorize action = authorizeAsyncResult (fun player -> action player >> AsyncResult.ofSuccess)

let gameApi = {
    newPlayer = fun name -> async {
        let player =
            name
            |> Player.create
            |> Option.defaultValue Player.anonymous

        Storage.addPlayer { Player = player; Checked = DateTime.Now }

        return player
    }
    logout = fun id -> async {
        Storage.players
        |> Seq.tryFind (fun player -> player.Id = id)
        |> Option.map Storage.players.Remove
        |> ignore
    }
    loadPlayer = fun id -> async {
        return
            Storage.players
            |> Seq.tryFind (ServerPlayer.id >> (=) id)
            |> Option.map ServerPlayer.player
    }

    notifyPlayerStatus = Authorize.authorizeAsync <| fun player () -> async {
        let i = Storage.players.FindIndex (fun p -> p.Id = player.Id)

        Storage.players[i] <- { player with Checked = DateTime.Now }
    }

    getPlayers = fun () -> async { return Storage.players |> Seq.map ServerPlayer.player |> List.ofSeq }

    changeCurrentPlayerName = Authorize.authorizeAsync <| fun player name -> async {
        let i = Storage.players.FindIndex (fun p -> p.Id = player.Id)

        Storage.players[i] <- { player with Player = { player.Player with Name = name }}
    }

    getChat = fun from -> async {
        return
            Storage.chat
            |> Seq.filter (fun message -> message.Created >= from)
            |> List.ofSeq
    }
    sendChatMessage = Authorize.authorize <| fun _ -> Storage.addMessage
}

async {
    while true do
        do! Storage.checkPlayers
        do! Async.Sleep (10 * 1000)
}
|> Async.Start

let update (clientDispatch: Dispatch<RemoteServerMsg>) (ClientMsg clientMsg) currentState =
    match clientMsg with
    | RemoteClientMsg.PlayerLoggedOut ->
        actionsHub.BroadcastClient RemoteServerMsg.RefreshPlayers
        actionsHub.BroadcastClient RemoteServerMsg.RefreshChat
        currentState, Cmd.none

    | RemoteClientMsg.PlayerChanged ->
        actionsHub.BroadcastClient RemoteServerMsg.RefreshPlayers
        currentState, Cmd.none

    | RemoteClientMsg.ChatMessageAdded addedAt ->
        actionsHub.BroadcastClient (RemoteServerMsg.RefreshChat addedAt)
        currentState, Cmd.none

let init (clientDispatch: Dispatch<RemoteServerMsg>) () = Nothing, Cmd.none

let socketServer =
    Bridge.mkServer Route.webSocket init update
    |> Bridge.withConsoleTrace
    |> Bridge.withServerHub actionsHub
    |> Bridge.register ClientMsg
    |> Bridge.run Giraffe.server

let apiRouter =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue gameApi
    |> Remoting.buildHttpHandler

let webApp =
    choose [
        socketServer
        apiRouter
    ]

let app =
    application {
        url "http://*:8085"
        use_router webApp
        //disable_diagnostics
        memory_cache
        use_static "public"
        app_config Giraffe.useWebSockets
        use_gzip
    }

[<EntryPoint>]
let main _ =
    run app
    0
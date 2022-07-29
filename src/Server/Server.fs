module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open Giraffe
open Elmish
open Elmish.Bridge

open Shared
open ErrorHandling

module Storage =
    let players: ResizeArray<Player> = ResizeArray()
    let chat: ResizeArray<ChatMessage> = ResizeArray()

    let addPlayer player = players.Add player
    let addMessage message = chat.Add message

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

        Storage.addPlayer player

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
            |> Seq.tryFind (fun player -> player.Id = id)
    }

    getPlayers = fun () -> async { return Storage.players |> List.ofSeq }

    changeCurrentPlayerName = Authorize.authorizeAsync <| fun player name -> async {
        let i = Storage.players.FindIndex (fun p -> p.Id = player.Id)

        Storage.players[i] <- { Storage.players[i] with Name = name }
    }

    getChat = fun from -> async {
        return
            Storage.chat
            |> Seq.filter (fun message -> message.Created >= from)
            |> List.ofSeq
    }
    sendChatMessage = Authorize.authorize <| fun _ -> Storage.addMessage
}

// server state is what the server keeps track of
type ServerState = Nothing

// the server message is what the server reacts to
// in this case, it reacts to messages from client
type ServerMsg = ClientMsg of RemoteClientMsg

// The postsHub keeps track of connected clients and has broadcasting logic
let actionsHub = ServerHub<ServerState, ServerMsg, RemoteServerMsg>().RegisterServer(ClientMsg)

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
        memory_cache
        use_static "public"
        app_config Giraffe.useWebSockets
        use_gzip
    }

[<EntryPoint>]
let main _ =
    run app
    0
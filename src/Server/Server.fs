module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn
open Giraffe
open Elmish
open Elmish.Bridge

open Shared

module Storage =
    let players = ResizeArray()

    let addPlayer player = players.Add player

let gameApi = {
    newPlayer = fun name -> async {
        let player =
            name
            |> Player.create
            |> Option.defaultValue Player.anonymous

        Storage.addPlayer player

        return player
    }
    loadPlayer = fun id -> async {
        return
            Storage.players
            |> Seq.tryFind (fun player -> player.Id = id)
            |> Option.defaultValue { Player.anonymous with Id = id }
    }
    getPlayers = fun () -> async { return Storage.players |> List.ofSeq }
    changeCurrentPlayerName = fun (id, name) -> async {
        let i = Storage.players.FindIndex (fun p -> p.Id = id)

        Storage.players[i] <- { Storage.players[i] with Name = name }
    }
}

// server state is what the server keeps track of
type ServerState = Nothing

// the server message is what the server reacts to
// in this case, it reacts to messages from client
type ServerMsg = ClientMsg of RemoteClientMsg

// The postsHub keeps track of connected clients and has broadcasting logic
let postsHub = ServerHub<ServerState, ServerMsg, RemoteServerMsg>().RegisterServer(ClientMsg)

let update (clientDispatch: Dispatch<RemoteServerMsg>) (ClientMsg clientMsg) currentState =
    match clientMsg with
    | RemoteClientMsg.PlayerChanged ->
        postsHub.BroadcastClient RemoteServerMsg.RefreshPlayers
        currentState, Cmd.none

let init (clientDispatch: Dispatch<RemoteServerMsg>) () = Nothing, Cmd.none

let socketServer =
    Bridge.mkServer Route.webSocket init update
    |> Bridge.withConsoleTrace
    |> Bridge.withServerHub postsHub
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
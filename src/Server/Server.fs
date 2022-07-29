module Server

open Fable.Remoting.Server
open Fable.Remoting.Giraffe
open Saturn

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

let webApp =
    Remoting.createApi ()
    |> Remoting.withRouteBuilder Route.builder
    |> Remoting.fromValue gameApi
    |> Remoting.buildHttpHandler

let app =
    application {
        url "http://*:8085"
        use_router webApp
        memory_cache
        use_static "public"
        use_gzip
    }

[<EntryPoint>]
let main _ =
    run app
    0
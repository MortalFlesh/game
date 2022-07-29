module App

open Elmish
open Elmish.Bridge
open Elmish.React

#if DEBUG
open Elmish.Debug
open Elmish.HMR
#endif

Program.mkProgram Index.init Index.update Index.view
|> Program.withBridgeConfig (Bridge.endpoint Shared.Route.webSocket |> Bridge.withMapping Types.ServerMsg)
#if DEBUG
|> Program.withConsoleTrace
#endif
|> Program.withReactSynchronous "elmish-app"
#if DEBUG
|> Program.withDebugger
#endif
|> Program.run

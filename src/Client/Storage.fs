module Storage
    open Thoth.Json
    open Browser.Types
    open Browser.WebStorage

    type Key =
        | Player

    [<RequireQualifiedAccess>]
    module Key =
        let value = function
            | Player -> "mf.game.player"

    [<RequireQualifiedAccess>]
    module LocalStorageLib =
        open Thoth.Json

        let loadItem key =
            let item = localStorage.getItem key
            if isNull item then None
            else Some item

        let inline loadWith (decoder: Decoder<'Data>) key: Result<'Data, string> =
            match key |> loadItem with
            | Some item -> Decode.fromString decoder item
            | _ -> "No item found in local storage with key " + key |> Error

        let inline load<'Data> key: Result<'Data, string> =
            key |> loadWith (Decode.Auto.generateDecoder<'Data>())

        let delete key =
            localStorage.removeItem(key)

        let inline saveWith (encode: 'Data -> string) key (data: 'Data) =
            localStorage.setItem(key, encode data)

        let inline save key (data: 'Data) =
            data |> saveWith (fun data -> Encode.Auto.toString(0, data)) key

    [<RequireQualifiedAccess>]
    module SessionStorageLib =
        open Thoth.Json

        let loadItem key =
            let item = sessionStorage.getItem key
            if isNull item then None
            else Some item

        let inline loadWith (decoder: Decoder<'Data>) key: Result<'Data, string> =
            match key |> loadItem with
            | Some item -> Decode.fromString decoder item
            | _ -> "No item found in local storage with key " + key |> Error

        let inline load<'Data> key: Result<'Data, string> =
            key |> loadWith (Decode.Auto.generateDecoder<'Data>())

        let delete key =
            sessionStorage.removeItem(key)

        let inline saveWith (encode: 'Data -> string) key (data: 'Data) =
            sessionStorage.setItem(key, encode data)

        let inline save key (data: 'Data) =
            data |> saveWith (fun data -> Encode.Auto.toString(0, data)) key


    (* [<AutoOpen>]
    module private Storage =
        let loadItem (storage: Storage) key =
            let item = storage.getItem key
            if isNull item then None
            else Some item

        let inline loadWith (storage: Storage) (decoder: Decoder<'Data>) key: Result<'Data, string> =
            match key |> loadItem storage with
            | Some item -> Decode.fromString decoder item
            | _ -> "No item found in local storage with key " + key |> Error

        let inline load<'Data> (storage: Storage) key: Result<'Data, string> =
            key |> loadWith storage (Decode.Auto.generateDecoder<'Data>())

        let delete (storage: Storage) key =
            storage.removeItem(key)

        let inline saveWith (storage: Storage) (encode: 'Data -> string) key (data: 'Data) =
            storage.setItem(key, encode data)

        let inline save (storage: Storage) key (data: 'Data) =
            data |> saveWith storage (fun data -> Encode.Auto.toString(0, data)) key *)

    [<RequireQualifiedAccess>]
    module LocalStorage =
        // let loadWith decoder key = loadWith localStorage decoder (key |> Key.value)
        // let inline loadItem key = loadItem localStorage (key |> Key.value)
        // let inline load<'Data> key = load<'Data> localStorage (key |> Key.value)
        // let delete key = delete localStorage (key |> Key.value)
        // let inline save key (data: 'Data) = save localStorage (key |> Key.value) data
        let loadWith decoder key = LocalStorageLib.loadWith decoder (key |> Key.value)
        let inline loadItem key = LocalStorageLib.loadItem (key |> Key.value)
        let inline load<'Data> key = LocalStorageLib.load<'Data> (key |> Key.value)
        let delete key = LocalStorageLib.delete (key |> Key.value)
        let inline save key (data: 'Data) = LocalStorageLib.save (key |> Key.value) data

    [<RequireQualifiedAccess>]
    module SessionStorage =
        // let loadWith decoder key = loadWith sessionStorage decoder (key |> Key.value)
        // let inline loadItem key = loadItem sessionStorage (key |> Key.value)
        // let inline load<'Data> key = load<'Data> sessionStorage (key |> Key.value)
        // let delete key = delete sessionStorage (key |> Key.value)
        // let inline save key (data: 'Data) = save sessionStorage (key |> Key.value) data
        let loadWith decoder key = SessionStorageLib.loadWith decoder (key |> Key.value)
        let inline loadItem key = SessionStorageLib.loadItem (key |> Key.value)
        let inline load<'Data> key = SessionStorageLib.load<'Data> (key |> Key.value)
        let delete key = SessionStorageLib.delete (key |> Key.value)
        let inline save key (data: 'Data) = SessionStorageLib.save (key |> Key.value) data

﻿open System.IO
open System.Net
open Suave
open Suave.Operators
#if (Remoting)
open Fable.Remoting.Server
open Fable.Remoting.Suave
#endif
open Shared

//#if (Deploy == "azure")
let publicPath = Azure.tryGetEnv "public_path" |> Option.defaultValue "../Client/public" |> Path.GetFullPath
let port = Azure.tryGetEnv "HTTP_PLATFORM_PORT" |> Option.map System.UInt16.Parse |> Option.defaultValue 8085us
let appInsightsKey = Azure.tryGetEnv "APPINSIGHTS_INSTRUMENTATIONKEY" |> Option.defaultValue ""

//#else
let publicPath = Path.GetFullPath "../Client/public"
let port = 8085us
//#endif

let config =
  { defaultConfig with 
      homeFolder = Some publicPath
      bindings = [ HttpBinding.create HTTP (IPAddress.Parse "0.0.0.0") port ] }

let getInitCounter () : Async<Counter> = async { return 42 }

let init : WebPart = 
#if (Remoting)
  let counterProcotol = 
    { getInitCounter = getInitCounter }
  // Create a WebPart for the given implementation of the protocol
  remoting counterProcotol {
    // define how routes are mapped
    use_route_builder Route.builder 
  }
#else
  Filters.path "/api/init" >=>
  fun ctx ->
    async {
      let! counter = getInitCounter()
      return! Successful.OK (string counter) ctx
    }
#endif

let webPart =
  choose [
    init
    Filters.path "/" >=> Files.browseFileHome "index.html"
    Files.browseHome
    RequestErrors.NOT_FOUND "Not found!"
  ] |> Azure.AI.withAppInsights Azure.AI.buildApiOperationName

Azure.AppServices.addTraceListeners()
Azure.AI.configure { AppInsightsKey = appInsightsKey; DeveloperMode = false; TrackDependencies = true }

startWebServer config webPart
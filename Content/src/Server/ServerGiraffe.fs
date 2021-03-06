﻿open System
open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection

open Giraffe

#if (Remoting)
open Fable.Remoting.Server
open Fable.Remoting.Giraffe
#else
open Giraffe.Serialization
open Microsoft.Extensions.DependencyInjection
#endif

open Shared

//#if (Deploy == "azure")
let publicPath =
    match System.Environment.GetEnvironmentVariable "public_path" with
    | null | "" -> "../Client/public"
    | path -> path
    |> Path.GetFullPath
//#else
let publicPath = Path.GetFullPath "../Client/public"
//#endif
let port = 8085us

let getInitCounter () : Task<Counter> = task { return 42 }

let webApp : HttpHandler =
#if (Remoting)
  let counterProcotol = 
    { getInitCounter = getInitCounter >> Async.AwaitTask }
  // creates a HttpHandler for the given implementation
  remoting counterProcotol {
    use_route_builder Route.builder
  }
#else
  route "/api/init" >=>
    fun next ctx ->
      task {
        let! counter = getInitCounter()
        return! Successful.OK counter next ctx
      }
#endif

let configureApp  (app : IApplicationBuilder) =
  app.UseDefaultFiles()
     .UseStaticFiles()
     .UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    services.AddGiraffe() |> ignore
    #if (!Remoting)
    let fableJsonSettings = Newtonsoft.Json.JsonSerializerSettings()
    fableJsonSettings.Converters.Add(Fable.JsonConverter())
    services.AddSingleton<IJsonSerializer>(NewtonsoftJsonSerializer fableJsonSettings) |> ignore
    #endif
    #if (Deploy == "azure")
    services.AddApplicationInsightsTelemetry(System.Environment.GetEnvironmentVariable "APPINSIGHTS_INSTRUMENTATIONKEY") |> ignore
    #endif

WebHost
  .CreateDefaultBuilder()
  .UseWebRoot(publicPath)
  .UseContentRoot(publicPath)
  .Configure(Action<IApplicationBuilder> configureApp)
  .ConfigureServices(configureServices)
  .UseUrls("http://0.0.0.0:" + port.ToString() + "/")
  .Build()
  .Run()
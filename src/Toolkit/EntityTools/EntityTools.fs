namespace EntityTools

open FSharp.Data
open System

type satoriRequest = JsonProvider<"https://www.bingapis.com/api/v5/search?knowledge=1&appId=D41D8CD98F00B204E9800998ECF8427E496F9910&responseformat=json&responsesize=m&q=space%20needle">
type satoriRequestPlaces = JsonProvider<"https://www.bingapis.com/api/v5/search?knowledge=1&appId=D41D8CD98F00B204E9800998ECF8427E496F9910&responseformat=json&responsesize=m&q=paris%20hotel%20and%20casino">

type Entitytools = 
    static member getSatoriInfo entityName:string[] = 
        let appID = "D41D8CD98F00B204E9800998ECF8427E496F9910"
        
        let rlt = [|"\t\t\t\t\t"|]
        try
            if not(String.IsNullOrWhiteSpace(entityName))  then
                let request = "https://www.bingapis.com/api/v5/search?knowledge=1&appId=" + appID + "&responseformat=json&responsesize=m&q=" + entityName
                let satoriRequestHtml = "https://www.bingapis.com/api/v5/search?knowledge=1&appId=" + appID + "&responseformat=html&responsesize=m&q=" + entityName
                let satoriResponse = satoriRequest.Load(request)
                
                if satoriResponse.JsonValue.TryGetProperty("entities").IsSome 
                      then satoriResponse.Entities.Value 
                                |> Array.map (fun x ->
                                let satoriID = x.Id
                                let satoriName = x.Name
                                let satoriDesc = x.Description
                                let satoriType = 
                                    if x.JsonValue.TryGetProperty("_type").IsSome 
                                        then x.Type
                                        //else x.EntityPresentationInfo.EntityTypeHints.[0] 
                                        else if x.EntityPresentationInfo.JsonValue.TryGetProperty("entityTypeHints").IsSome
                                            then x.EntityPresentationInfo.EntityTypeHints.[0] 
                                            else x.EntityPresentationInfo.EntityTypeDisplayHint
                                let satoriImage = x.Image.ThumbnailUrl
                                let satoriSnippet = satoriRequestHtml + "&filters=sid:\"" + satoriID.ToString() + "\""
                                satoriID.ToString() + "\t" + satoriName + "\t" + satoriDesc + "\t" + satoriImage + "\t" + satoriType + "\t" + satoriSnippet
                                )
                      else
                          let satoriResponsePlaces = satoriRequestPlaces.Load(request)
                          satoriResponsePlaces.Places.Value 
                                |> Array.map (fun x ->
                                let satoriID = x.Id
                                let satoriName = x.Name
                                let satoriDesc = x.Description
                                let satoriType = 
                                    if x.JsonValue.TryGetProperty("_type").IsSome 
                                        then x.Type
                                        else x.EntityPresentationInfo.EntityTypeHints.[0] 
                                let satoriImage = 
                                    if x.JsonValue.TryGetProperty("image").IsSome 
                                        then x.Image.ContentUrl
                                        elif x.JsonValue.TryGetProperty("photo").IsSome 
                                            then x.Photo.[0].ContentUrl
                                            else "ImageNotAvailable"
                                let satoriSnippet = satoriRequestHtml + "&filters=sid:\"" + satoriID + "\""            
                                satoriID.ToString() + "\t" + satoriName + "\t" + satoriDesc + "\t" + satoriImage + "\t" + satoriType + "\t" + satoriSnippet
                                )
            else          
                rlt      
        with
        | :? System.Exception as ex -> 
            printfn "!error! cannot get satori Info for %A\n" entityName
            rlt 
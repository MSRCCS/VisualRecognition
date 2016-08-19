namespace EntityTools

open FSharp.Data
open System

type satoriRequest = JsonProvider<"https://www.bingapis.com/api/v5/search?knowledge=1&appId=D41D8CD98F00B204E9800998ECF8427E496F9910&responseformat=json&responsesize=m&q=tom%20cruise">

type Entitytools = 
    static member getSatoriInfo entityName:string = 
        try
            if not(String.IsNullOrWhiteSpace(entityName))  then
                let satoriResponse = satoriRequest.Load("https://www.bingapis.com/api/v5/search?knowledge=1&appId=D41D8CD98F00B204E9800998ECF8427E496F9910&responseformat=json&responsesize=m&q=" + entityName)
                //if String.length(entityName) > 0 && satoriResponse.Entities.Value.Length > 0 then
                let satoriID = satoriResponse.Entities.Value.[0].Id
                let satoriName = satoriResponse.Entities.Value.[0].Name
                let satoriDesc = satoriResponse.Entities.Value.[0].Description
                let satoriImage = satoriResponse.Entities.Value.[0].Image.ThumbnailUrl
                satoriID.ToString() + "\t" + satoriName + "\t" + satoriDesc + "\t" + satoriImage
            else
                "\t\t\t"        
        with
        | :? System.Exception as ex -> 
            Guid.Empty.ToString() + "\t\t\t"
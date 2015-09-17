# AppleCare GSX C# Implementation without WSDL

I had a great deal of trouble implementing the new (aug 15 2015) WSDL of Apple's GSX API, so I ended up writing my own wrapper for it.

WebserviceHandler.cs handles the actual communication to the AppleCare GSX endpoint, or any webservice for that matter.
Just pass it the service endpoint URL as well as the SSL certificate file (and passord if used), then call up the Call() function with the XML data to send and you're golden.
The response will come back as an XDocument.

GSXNextGen.cs is the C# class wrapper for the various functions to call the GSX API for.

## Example

´´´csharp
    const string serviceUrl = "https://gsxapi.apple.com/gsx-ws/services/emea/asp";

    var userId = "user@domain.com"; // This is a verified GSX user.
    var serviceAccountNo = "XXX"; // Insert your ShipTo number here.

    var res = GSXNextGen.Execute<GSXNextGen.Authenticate.AuthenticateResponse>(
        new GSXNextGen.Authenticate.globAuthenticate {
            ColonCutOffset = 4,
            AuthenticateRequest = new GSXNextGen.Authenticate.AuthenticateRequest {
                languageCode = "EN",
                serviceAccountNo = serviceAccountNo,
                userId = userId,
                userTimeZone = "CET"
            }
        },
        GSXNextGen.Envelope.Global,
        serviceUrl,
        "Authenticate");
´´´

This will create a authenticate object which is passed into the Execute() function, which will convert it to XML and pass it along to the WebserviceHandler for request and response.

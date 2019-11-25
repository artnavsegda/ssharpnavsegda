using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.WebScripting; // Add a reference to SimplSharpCWSHelperInterface.dll
using Newtonsoft.Json;                  // Add a reference to Newtonsoft.Json.Compact.dll
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;

namespace HttpClientExample
{
    public class RelayMonitor : IDisposable
    {
        HttpClient client;          // sends GET, POST, and DELETE requests to the server's REST API to get relay info and create/delete subscriptions
        HttpCwsServer listener;     // listens for notifications from the server when the subscribed relay changes state
        UrlParser listenUrl;        // obscure URL on which to listen for notifications after making a subscription
        UrlParser serverAPIUrl;     // Root URL of the server API. Combined with relative paths to construct destination URL for HTTP requests
        UrlParser subscriptionUrl;  // obscure URL of the monitor's subscription resource. Sending a DELETE to this URL constitutes an unsubscription
        bool subscribed = false;    // current subscription state. Monitor only handles one subscription for the purpose of the example
        int rlyID;                  // ID of the relay subscribed to. Ranges between 1 and the maximum number of relays on the server

        public RelayMonitor(string myHostname)
        {
            client = new HttpClient();
            listener = new HttpCwsServer("/listen");
            listener.ReceivedRequestEvent += new HttpCwsRequestEventHandler(listener_ReceivedRequestEvent);
            listener.HttpRequestHandler = new DefaultHandler(this); // this handler will process all requests made to the listener
            
            listenUrl = new UrlParser("http://" + myHostname + "/cws/listen/" + Guid.NewGuid().ToString());
            
            // register listener to begin receiving POST notifications
            if (listener.Register())
            {
                CrestronConsole.PrintLine("Registered notification listener");
            }
            else
            {
                CrestronConsole.PrintLine("Failed to register notification listener");
            }
        }

        // Subscribe to a random relay port on the server control system. The RelayMonitor will print notifications
        // to the console when this relay changes state
        public void Subscribe(string serverHostname)
        {
            try
            {
                if (subscribed)
                {
                    CrestronConsole.PrintLine("The monitor can only subscribe to one relay at a time. Unsubscribe first");
                    return;
                }

                // point serverAPIUrl at the root of the remote CWS server
                serverAPIUrl = new UrlParser("http://" + serverHostname + "/cws/api/");

                // GET the relay collection and derive the number of relays 
                // available on the control system from the response
                HttpClientRequest req = new HttpClientRequest();
                UrlParser collectionUrl = new UrlParser(serverAPIUrl, "relays"); // complete URL = baseUrl + relativeUrl

                req.Url = collectionUrl;
                req.RequestType = RequestType.Get;
                HttpHeader acceptHeader = new HttpHeader("Accept", "application/vnd.collection+json");
                req.Header.AddHeader(acceptHeader);
                req.FinalizeHeader();
                HttpClientResponse res = client.Dispatch(req);

                if (res.Code == 200)
                {
                    CrestronConsole.PrintLine("Received GET response for the relay collection");
               
                    string json = res.ContentString;
                    JObject collection = JsonConvert.DeserializeObject<JObject>(json);
                    JArray list = (JArray)collection["collection"]["items"];
                    int relayCount = list.Count;
                    Random rnd = new Random();
                    rlyID = rnd.Next(1, relayCount + 1);

                    CrestronConsole.PrintLine("Server control system has " + relayCount + " relays. Subscribing to relay #" + rlyID + "...");

                    // Subscribe the the web-hook for the Relay #rlyID
                    req = new HttpClientRequest();
                    UrlParser webhookUrl = new UrlParser(serverAPIUrl, "relays/" + rlyID + "/web-hooks");
                    req.Url = webhookUrl;
                    req.RequestType = RequestType.Post;
                    // add the Content-Type and Notification-Type headers as required by the RESTful WebHooks standard
                    HttpHeaders headers = new HttpHeaders();
                    headers.SetHeaderValue("Content-Type", "text/uri-list"); // webhook expects POST body to contain a single URL
                    headers.SetHeaderValue("Notification-Type", "UPDATED"); // monitor wants to know when relay state is changed
                    req.Header = headers;
                    req.FinalizeHeader();
                    req.ContentString = listenUrl.ToString();
                    res = client.Dispatch(req);

                    if (res.Code == 201) // successful POST, subscription resource has been created
                    {
                        subscriptionUrl = new UrlParser(res.Header["Location"].Value); // save the obscure URL to the new subscription resource
                        subscribed = true;
                        CrestronConsole.PrintLine("Subscribed to Relay #" + rlyID);
                        CrestronConsole.PrintLine("Subscription resource URL: " + subscriptionUrl);
                    }
                    else
                    {
                        CrestronConsole.PrintLine("Failed to subscribe to " + rlyID);
                    }
                    // Must call Dispose on the HttpClientResponse object to end this HTTP session
                    res.Dispose();
                }
                else
                {
                    CrestronConsole.PrintLine("Failed to get relay collection");
                    return;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in Subscribe(): " + e.Message);
            }
            finally
            {

            }
        }

        // Unsubscribe to the relay on the server control system
        public void Unsubscribe()
        {
            try
            {
                if (!subscribed)
                {
                    CrestronConsole.PrintLine("Already unsubscribed to relay on " + serverAPIUrl);
                    return;
                }
                // send a DELETE request to the subscription URL
                HttpClientRequest req = new HttpClientRequest();

                req.Url = subscriptionUrl;
                req.RequestType = RequestType.Delete;

                HttpClientResponse res = client.Dispatch(req);

                if (res.Code == 204) // Expecting a "Not Found" response to show that the DELETE succeeded
                {
                    CrestronConsole.PrintLine("Deleted subscription to relay " + rlyID);
                    subscribed = false;
                }
                else
                {
                    CrestronConsole.PrintLine("Server was unable to delete subscription to relay " + rlyID);
                }
                res.Dispose();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in Unsubscribe: " + e.Message);
            }
        }

        // Event handler called every time the listener receives an HTTP request
        private void listener_ReceivedRequestEvent(object sender, HttpCwsRequestEventArgs args)
        {
            CrestronConsole.PrintLine("");
            CrestronConsole.PrintLine("Received " + args.Context.Request.HttpMethod + " request from " 
                                        + args.Context.Request.UserHostName);
        }

        // Print a description of the monitor's status
        public override string ToString()
        {
            if (subscribed)
            {
                return "Monitor subscribed to Relay #" + rlyID + " using API at " + serverAPIUrl;
            }
            else
            {
                return "Unsubscribed monitor";
            }
        }

        #region Dispose Pattern
        bool disposed = false;
        public void Dispose()
        {            
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
            {
                client.Dispose();
                listener.Dispose();
                listener.Unregister();
            }           
            disposed = true;
        }

        ~RelayMonitor()
        {
            Dispose(false);
        }
        #endregion

        // Handler for all incoming HTTP requests. Only notifications POSTed to the monitor's listenUrl
        // get a non-error response
        class DefaultHandler : IHttpCwsHandler
        {
            RelayMonitor parent;
            public DefaultHandler(RelayMonitor parent)
            {
                this.parent = parent;
            }

            public void ProcessRequest(HttpCwsContext context) 
            {
                try
                {                 
                    // Only respond to POST requests made to the listen URL's path
                    if (parent.listenUrl.Path == context.Request.Url.AbsolutePath)
                    {
                        if (context.Request.HttpMethod == "POST")
                        {
                            // GET the individual relay to observe its updated state
                            HttpClientRequest req = new HttpClientRequest();
                            string linkString = context.Request.Headers["Link"];

                            // The notification POST request contains a link to the updated resource in the Link header.
                            // in compliance with the RESTful WebHooks standard at 
                            // https://webhooks.pbworks.com/w/page/13385128/RESTful%20WebHooks
                            // This URL will be nested between the '<' and '>' characters,
                            // as described in https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Link
                            int startIndex = linkString.IndexOf('<') + 1;
                            int endIndex = linkString.IndexOf('>');
                            string linkUrlString = linkString.Substring(startIndex, endIndex - startIndex);

                            UrlParser relayUrl = new UrlParser(linkUrlString); 
                            req.Url = relayUrl;
                            req.RequestType = RequestType.Get;
                            HttpHeader acceptHeader = new HttpHeader("Accept", "application/vnd.collection+json");
                            req.Header.AddHeader(acceptHeader);
                            req.FinalizeHeader();
                            HttpClientResponse res = parent.client.Dispatch(req);

                            int rlyID;
                            string newState = "";
                            if (res.Code == 200)
                            {
                                JObject item = JsonConvert.DeserializeObject<JObject>(res.ContentString);

                                rlyID = (int)item["collection"]["items"][0]["data"][0]["value"];
                                newState = (string)item["collection"]["items"][0]["data"][1]["value"];
                            }
                            else
                            {
                                CrestronConsole.PrintLine("Failed to get individual relay");
                                return;
                            }
                            // log the notification message to console
                            CrestronConsole.PrintLine("NOTIFICATION: Relay #" + rlyID + "'s state changed to " + newState);

                            // respond to notification with 200 OK
                            context.Response.StatusCode = 200;
                            context.Response.StatusDescription = "OK";
                            context.Response.End();
                        }
                        else
                        {
                            context.Response.StatusCode = 405;
                            context.Response.StatusDescription = "Method Not Allowed";
                            context.Response.End();
                        }
                    }
                    else // ignore all other URLs besides the listener URL
                    {
                        CrestronConsole.PrintLine("Sending back a 404");
                        context.Response.StatusCode = 404;
                        context.Response.StatusDescription = "Not Found";
                        context.Response.End();
                    }
                }
                catch (Exception e)
                {
                    try
                    {
                        // Respond with error message
                        context.Response.StatusCode = 500;
                        context.Response.StatusDescription = "Internal Server Error";
                        context.Response.End();
                        CrestronConsole.PrintLine("Error in ProcessRequest: " + e.Message);
                    }
                    catch (Exception ex)
                    {
                        CrestronConsole.PrintLine("ProcessRequest unable to send error response: " + ex.Message);
                    }
                }
                finally
                {
                    ErrorLog.Notice("Served response to " + context.Request.UserHostName);
                }
            }
        }
    }
}
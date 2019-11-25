using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.WebScripting;  // Add a reference to SimplSharpCWSHelperInterface.dll
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net;
using Crestron.SimplSharpPro;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;                   // Add a reference to Newtonsoft.Json.Compact.dll
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;

namespace HttpCwsServerExample
{
    public class RestfulRelayServer : IDisposable
    {
        class Subscription
        {
            HttpClient notifier; // get this client from the RestfulRelayServer parent object
            public string SubscriberCallbackUrl { get; set; }
            public Guid SubID { get; set; }
            public string ResourceUrl { get; set; }

            // CREATED, ACCESSED, UPDATED, or DELETED are the four possible event types according to
            // the RESTful WebHooks standard at https://webhooks.pbworks.com/w/page/13385128/RESTful%20WebHooks 
            // This example, however, will only send UPDATED notifications.
            public string NotificationType { get; set; } 

            public Subscription(string resourceUrl, string callbackUrl, string notificationType, HttpClient notifier)
            {
                ResourceUrl = resourceUrl;
                UrlParser parser = new UrlParser();
                parser.Parse(callbackUrl); // ensure that the URL passed is non-null and that it is well-formed
                SubscriberCallbackUrl = callbackUrl;
                this.notifier = notifier;
                NotificationType = notificationType;
                SubID = Guid.NewGuid(); // generate a unique ID for this subscription resource
            }

            // Notify() sends a POST request to the Subscription's callback URL.
            // DispatchAsync could be used to keep this method from blocking if the server has many subscriptions
            public void Notify() 
            {
                try
                {
                    HttpClientRequest req = new HttpClientRequest();
                    req.RequestType = RequestType.Post;

                    // First, resolve the hostname using the ResolveHost method
                    var parsedUrl = new UrlParser(SubscriberCallbackUrl);
                    string resolvedHostname = ResolveHost(parsedUrl.Hostname);

                    // Swap in the resolved hostname when setting the target URL of the notification
                    string resolvedUrl = parsedUrl.ToString().Replace(parsedUrl.Hostname, resolvedHostname);                    
                    req.Url = new UrlParser(resolvedUrl);
                    HttpHeaders headers = new HttpHeaders();
                    headers.SetHeaderValue("Notification-Type", NotificationType);
                    headers.SetHeaderValue("Link", "<" + ResourceUrl + ">, rel=\"Resource\"");
                    req.Header = headers;
                    req.FinalizeHeader();
                    HttpClientResponse res = notifier.Dispatch(req);

                    if (res.Code != 200)
                    {
                        CrestronConsole.PrintLine("Notification to " + resolvedUrl + " not acknowledged");
                    }

                    // Must call Dispose on the HttpClientResponse object to end the http session
                    res.Dispose();
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine(e.ToString());
                }
            }

            // Resolve the hostname to an IP address
            private string ResolveHost(string hostname)
            {
                var resolvedHost = new StringBuilder();
                if (!InitialParametersClass.ResolveHostName(hostname, ref resolvedHost))
                {
                    throw new Exception("Failed to resolve host");
                }
                return resolvedHost.ToString();
            }
        }

        readonly int _numberOfRelays;
        public int NumberOfRelays
        {
            get { return _numberOfRelays; }
        }

        readonly CrestronCollection<Relay> _relays;
        public CrestronCollection<Relay> Relays // read-only property to access relays
        {
            get { return _relays; }
        }

        // http://<hostname>
        readonly string _root;
        public string Root
        {
            get { return _root; }
        }
        readonly string _cjTemplate; // Collection+JSON template. Used to represent both the Relay collection and individual Relays
        public string CjTemplate
        {
            get { return _cjTemplate; }
        }
        readonly HttpCwsServer server;
        readonly HttpClient notifier;
        readonly List<Dictionary<string, Subscription>> sublists; // map GuID string to the Subscription object
        const int COLLECTION_SUBLIST = 0;

        public RestfulRelayServer(CrestronCollection<Relay> relay_collection, string hostname)
        {
            try
            {
                _numberOfRelays = relay_collection.Count;
                _relays = relay_collection;
                _root = "http://" + hostname;
                notifier = new HttpClient();

                // To ensure that the template.json file appears in the control system's application directory, 
                // set "Build Action" to "Content" and "Copy to Output Directory" to "Copy always" in the file's Properties menu
                _cjTemplate = File.ReadToEnd(Directory.GetApplicationDirectory() + "\\template.json", Encoding.UTF8);

                // set up subscription lists for the Relay collection and for each Relay
                sublists = new List<Dictionary<string, Subscription>>(NumberOfRelays + 1); // Both the collection and each relay gets its own subscription dictionary
                for (int i = 0; i < NumberOfRelays + 1; i++)
                {
                    sublists.Add(new Dictionary<string, Subscription>()); // client will use a GuID to locate the subscription resource if it wants to DELETE it.
                }

                // start up the server                               
                server = new HttpCwsServer("/api");
                /*
                URL Routing table:
                    /relays
                    /relays/web-hooks
                    /relays/web-hooks/{subid}
                    /relays/{id}
                    /relays/{id}/web-hooks
                    /relays/{id}/web-hooks/{subid}
                */

                // subscribe to events
                server.ReceivedRequestEvent += new HttpCwsRequestEventHandler(server_ReceivedRequestEvent);
                server.HttpRequestHandler = new DefaultHandler(this); // This is the default handler. It will process unrouted requests.
                foreach (Relay r in relay_collection)
                {
                    r.StateChange += new RelayEventHandler(r_StateChange);
                }

                // Add each route to server's routing table: In this example, each route is 
                // associated with its own RouteHandler class, which will process the HTTP request and write its response.
                // Other designs might use a single route handler that interprets the full requested path and performs the
                // route handling in this handler's ProcessRequest method

                // The order in which you add the routes matters. The HttpCwsServer will search in this order for a route matching
                // the requested URL, stopping at the first match it finds and calling the corresponding handler's 
                // ProcessRequest method, or that of the HttpRequestHandler (called "DefaultHandler" in this example) 
                // if no route-specific handler exists.
                HttpCwsRoute route = new HttpCwsRoute("relays") { Name = "relay_collection" }; // do not include the leading '/' in the HttpCwsRoute constructor
                route.RouteHandler = new RelaysHandler(this);
                server.Routes.Add(route);

                route = new HttpCwsRoute("relays/web-hooks") { Name = "relay_collection_subscriptions" };
                route.RouteHandler = new RelaysWebhooksHandler(this);
                server.Routes.Add(route);

                route = new HttpCwsRoute("relays/web-hooks/{subid}") { Name = "relay_collection_subscription" };
                route.RouteHandler = new RelaysWebhooksSubidHandler(this);
                server.Routes.Add(route);

                route = new HttpCwsRoute("relays/{id}") { Name = "individual_relay" };
                route.RouteHandler = new RelaysIdHandler(this);
                server.Routes.Add(route);

                route = new HttpCwsRoute("relays/{id}/web-hooks") { Name = "individual_relay_subscriptions" };
                route.RouteHandler = new RelaysIdWebhooksHandler(this);
                server.Routes.Add(route);

                route = new HttpCwsRoute("relays/{id}/web-hooks/{subid}") { Name = "individual_relay_subscription" };
                route.RouteHandler = new RelaysIdWebhooksSubidHandler(this);
                server.Routes.Add(route);

                // Start receiving HTTP requests
                server.Register();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in the RestfulRelayServer constructor: " + e.Message);
            }
            finally
            {

            }
        }

        // Not Thread-safe: does not handle issues where client subscribes/unsubscribes during the 
        // state change event handler. The foreach loops would fail in this case
        void r_StateChange(Relay relay, RelayEventArgs args)
        {
            try
            {
                // dispatch notifications to collection subscribers and to this particular relay's subscribers
                foreach (KeyValuePair<string, Subscription> subscription in sublists[COLLECTION_SUBLIST])
                {
                    subscription.Value.Notify();
                }
                foreach (KeyValuePair<string, Subscription> subscription in sublists[(int)relay.ID])
                {
                    subscription.Value.Notify();
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in r_StateChange(): " + e.Message);
            }
            finally
            {
                string stateStr = (relay.State) ? "closed" : "opened";
                CrestronConsole.PrintLine("Relay " + relay.ID + " " + stateStr);
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
                server.Unregister();
                server.Dispose();
                notifier.Dispose();
            }           
            disposed = true;
        }

        ~RestfulRelayServer()
        {
            Dispose(false);
        }
        #endregion

        // Event handlers
        // Implement the route handlers by reading the HTTP method/headers/entity-body and 
        // performing the corresponding action

        void server_ReceivedRequestEvent(object sender, HttpCwsRequestEventArgs args)
        {
            try
            {
                // Do something light here. Do not process the requests in the event handler.
                CrestronConsole.PrintLine("Incoming " + args.Context.Request.HttpMethod + " Request from "
                                            + args.Context.Request.UserHostName + " to " + args.Context.Request.Url);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in server_ReceivedRequestEvent: {0}", e);
            }
        }

        // This base class implements methods that all the derived request handlers share
        abstract class MyHttpHandler : IHttpCwsHandler
        {
            protected string allowedMethods; // value of the "Allow" header when the server returns a "Method Not Allowed" response
            protected string supportedMediaTypes; // for classes that handle POST/PUT requests, they can inform clients of the media types supported in the bodies of such requests
            protected RestfulRelayServer server; // reference to the server, the handler's "parent"
            public abstract void ProcessRequest(HttpCwsContext context);

            public MyHttpHandler(RestfulRelayServer parent)
            {
                server = parent;
            }

            // Return a string representing the URL of the requested resource with no trailing
            // The returned URL won't end in a '/'
            public string GetResourceUrl(HttpCwsContext context)
            {
                // Constructing the URL from the HTTP request object avoids hard-coding the protocol (http/https)
                // into the URL, and it allows one to change the route name without breaking the code
                string resourceUrl = context.Request.Url.Scheme + "://"
                            + context.Request.Url.Authority
                            + context.Request.Url.AbsolutePath;
                // remove trailing '/'s if present
                resourceUrl = resourceUrl.TrimEnd('/');

                //while (resourceUrl.EndsWith("/"))
                //{
                //    resourceUrl = resourceUrl.Substring(0, resourceUrl.Length - 1);
                //}
                return resourceUrl;
            }

            // Write a Collection+JSON error message to the client. Put a non-empty string in msg to provide a specific
            // error description.
            public void RespondWithJsonError(HttpCwsContext context, int code, string title, string resourceUrl, string msg)
            {
                try
                {
                    context.Response.StatusCode = code;
                    context.Response.StatusDescription = title;
                    context.Response.ContentType = "application/vnd.collection+json";
                    if (code == 405)
                    {
                        context.Response.AppendHeader("Allow", allowedMethods);
                    }
                    // Since the client is expecting a Collection+JSON response, send the "error" object
                    // detailing the error
                    JObject err = new JObject 
                    {
                        { 
                            "collection", new JObject
                            {
                                { "version", "1.0"},
                                { "href", resourceUrl },
                                { 
                                    "error", new JObject
                                    {
                                        { "title", title },
                                        { "code", code }
                                    }
                                }
                            }
                        }
                    };
                    // Optionally include a specific error message to the client. Automated clients
                    // would be able to print this message in an error log, for example
                    if (msg != null && msg != "") 
                    {
                        err["collection"]["error"]["message"] = msg;
                    }

                    string json = JsonConvert.SerializeObject(err);
                    context.Response.Write(json, true);
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Exception in RespondWithJsonError: " + e.Message);
                }
                finally
                {
                    CrestronConsole.PrintLine("Served a Collection+JSON error resposne");
                }
            }
        };

        // handle unrouted requests by returning a 404 response
        class DefaultHandler : MyHttpHandler
        {
            public DefaultHandler(RestfulRelayServer parent) : base(parent)
            {
                allowedMethods = ""; // Server doesn't care what the HTTP method is for an unrouted request
            }
            public override void ProcessRequest(HttpCwsContext context)
            {
                string resourceUrl = "";
                try
                {
                    resourceUrl = GetResourceUrl(context);
                    RespondWithJsonError(context, 404, "Not Found", resourceUrl, "Requested resource does not exist");
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Error in DefaultHandler: " + e.Message);
                    RespondWithJsonError(context, 500, "Internal Server Error", resourceUrl, null);
                }
            }
        }

        // handle requests to /cws/api/relays
        class RelaysHandler : MyHttpHandler
        {
            public RelaysHandler(RestfulRelayServer parent) : base(parent)
            {
                allowedMethods = "GET, HEAD";
            }

            // FormatCollection: convert the Relay collection to a string 
            // representing the 'items' property of the Collection+JSON response
            public string FormatCollection(CrestronCollection<Relay> collec, string collecUrl)
            {
                JArray items = new JArray();
                foreach (Relay r in collec)
                {
                    JObject item = new JObject();
                    item["href"] = collecUrl + "/" + r.ID;
                    
                    JObject ID = new JObject();
                    ID["name"] = "ID";
                    ID["value"] = r.ID;

                    JObject State = new JObject();
                    State["name"] = "State";
                    State["value"] = (r.State) ? "Closed" : "Open";

                    JArray data = new JArray();
                    data.Add(ID);
                    data.Add(State);

                    item["data"] = data;

                    items.Add(item);
                }

                return JsonConvert.SerializeObject(items);
            }

            public override void ProcessRequest(HttpCwsContext context)
            {
                string resourceUrl = "";
                try
                {
                    resourceUrl = GetResourceUrl(context);
                    if (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "HEAD") // HTTP methods are case-sensitive
                    {
                        context.Response.StatusCode = 200;
                        context.Response.StatusDescription = "OK";
                        context.Response.AppendHeader("Content-Type", "application/vnd.collection+json");
                        context.Response.AppendHeader("Link", resourceUrl + "/web-hooks" + ", rel=\"Subscriptions\""); // requirement of RESTful WebHooks standard

                        if (context.Request.HttpMethod == "GET") // add the body for the GET request
                        {
                            // return the Collection+JSON representation of the Relay list

                            // Populate template.json with the Collection+JSON response.
                            // This response will not include the "template" property, since 
                            // clients may not PUT or POST to the relay collection itself
                            string template = String.Copy(server.CjTemplate);
                            template = template.Replace("{@resource}", resourceUrl);
                            template = template.Replace("{@list}", FormatCollection(server.Relays, resourceUrl));
                            template = template.Replace("{@includetemplate}", "");
                            template = template.Replace("{@template}", "");
                            context.Response.Write(template, false); // write the populated template as a response
                        }

                        context.Response.End();
                    }
                    else
                    {
                        RespondWithJsonError(context, 405, "Method Not Allowed", resourceUrl, allowedMethods);
                    }
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Could not process request to " +
                        context.Request.Url.AbsolutePath + ": " + e.Message);
                    RespondWithJsonError(context, 500, "Internal Server Error", resourceUrl, null);
                }
                finally
                {
                    CrestronConsole.PrintLine("Served response for " + context.Request.Url.AbsolutePath);
                }
            }
        }

        // handle requests to /cws/api/relays/web-hooks
        class RelaysWebhooksHandler : MyHttpHandler
        {
            public RelaysWebhooksHandler(RestfulRelayServer parent) : base(parent)
            {
                allowedMethods = "POST";
                supportedMediaTypes = "text/uri-list";
            }

            public override void ProcessRequest(HttpCwsContext context)
            {
                // Example request/response:
                // =>
                // POST http://control-system/cws/api/relays/web-hooks
                // Content-Type : text/uri-list
                // Notification-Type : UPDATED
                //
                // http://www.webscripts.com/myaccount/mylistener
                //
                // <=
                // 201 Created
                // Location : http://control-system/cws/api/relays/web-hooks/46da87b3-7997-4ca1-8203-29ae68f01a6f
                // Link : <http://control-system/cws/api/relays/web-hooks>, rel="Subscriptions"
                string resourceUrl = "";
                try
                {
                    resourceUrl = GetResourceUrl(context);
                    if (context.Request.HttpMethod == "POST")
                    {
                        if (context.Request.Headers.Get("Content-Type") == "text/uri-list")
                        {
                            string callbackUrl;
                            using (StreamReader s = new StreamReader(context.Request.InputStream)) 
                            {
                                callbackUrl = s.ReadLine(); // the first line of the request's entity body must be the callback URL
                            }
                            if (context.Request.Headers.Get("Notification-Type") == "UPDATED")
                            {
                                // Create an internal subscription resource. Subscription constructor will validate the URL
                                // remove "/web-hooks" so the subscription is linked to the collection resource itself, not the webhook
                                Subscription newSub = new Subscription(resourceUrl.Replace("/web-hooks", ""), callbackUrl, "UPDATED", server.notifier);
                                server.sublists[COLLECTION_SUBLIST].Add(newSub.SubID.ToString(), newSub);

                                // send response
                                context.Response.StatusCode = 201;
                                context.Response.StatusDescription = "Created";
                                context.Response.AppendHeader("Location", resourceUrl + "/" + newSub.SubID);
                                context.Response.AppendHeader("Link", "<" + resourceUrl + ">" + ", rel=\"Subscriptions\"");
                                context.Response.End();
                            }
                            // Other notification types include "ACCESSED," "CREATED," and "DELETED," but this
                            // server's WebHooks resources will not allow them
                            else
                            {
                                RespondWithJsonError(context, 400, "Bad Request", resourceUrl, "supported Notification-Types: UPDATED");
                            }
                        }
                        else
                        {
                            RespondWithJsonError(context, 415, "Unsupported Media Type", resourceUrl, "supported Content-Types: " + supportedMediaTypes);
                        }
                    }
                    else
                    {
                        RespondWithJsonError(context, 405, "Method Not Allowed", resourceUrl, "allowed methods: " + allowedMethods);
                    }
                }
                catch (System.UriFormatException e)
                {
                    CrestronConsole.PrintLine("Malformed client request to " + context.Request.Url.AbsolutePath + ": " + e.Message);
                    RespondWithJsonError(context, 400, "Bad Request", resourceUrl, "Malformed URL provided in request body");

                }
                catch (System.ArgumentNullException e)
                {
                    CrestronConsole.PrintLine("Malformed client request to " + context.Request.Url.AbsolutePath + ": " + e.Message);
                    RespondWithJsonError(context, 400, "Bad Request", resourceUrl, "No URL provided in request body");
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Could not process request to " + context.Request.Url.AbsolutePath + ": " + e.Message);
                    RespondWithJsonError(context, 500, "Internal Server Error", resourceUrl, null);
                }
                finally
                {
                    CrestronConsole.PrintLine("Served response for " + context.Request.Url.AbsolutePath);
                }
            }
        }

        // handle requests to /cws/api/relays/{subid}
        class RelaysWebhooksSubidHandler : MyHttpHandler
        {
            public RelaysWebhooksSubidHandler(RestfulRelayServer parent) : base(parent)
            {
                allowedMethods = "DELETE";
            }
            public override void ProcessRequest(HttpCwsContext context)
            {
                string resourceUrl = "";
                try
                {
                    resourceUrl = GetResourceUrl(context);
                    string subID = context.Request.RouteData.Values["subid"].ToString();
                    if (server.sublists[COLLECTION_SUBLIST].ContainsKey(subID))
                    {
                        if (context.Request.HttpMethod == "DELETE")
                        {
                            // remove the internal subscription resource
                            server.sublists[COLLECTION_SUBLIST].Remove(subID);
                            CrestronConsole.PrintLine("subscription " + subID + " has been deleted by " + context.Request.UserHostAddress);
                            // send the response
                            context.Response.StatusCode = 204;
                            context.Response.StatusDescription = "No Content";
                            context.Response.End();
                        }
                        else
                        {
                            RespondWithJsonError(context, 405, "Method Not Allowed", resourceUrl, "allowed methods: " + allowedMethods);
                        }
                    }
                    else
                    {
                        RespondWithJsonError(context, 404, "Not Found", resourceUrl, null);
                    }
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Could not process request to " + context.Request.Url.AbsolutePath + ": " + e.Message);
                    RespondWithJsonError(context, 500, "Internal Server Error", resourceUrl, null);
                }
                finally
                {
                    CrestronConsole.PrintLine("Served response for " + context.Request.Url.AbsolutePath);
                }
            }
        }

        // handle requests to /cws/api/relays/{id}
        class RelaysIdHandler : MyHttpHandler
        {
            // JSON schema to validate the body of a PUT request. 
            // The schema will ensure that the PUT request's body 
            // is a valid Collection+JSON template object
            JsonSchema putRequestBodySchema;

            // JSON string representing the template object in the Collection+JSON response to a GET request.
            // Clients will use this template to construct a PUT request to the Relay resource to update its state
            string templateString;
            public RelaysIdHandler(RestfulRelayServer parent) : base(parent)
            {
                allowedMethods = "GET, HEAD, PUT";
                supportedMediaTypes = "application/vnd.collection+json"; // required Content-Type for the body of a PUT request
                // ProcessRequest will use a JSON Schema to validate the PUT request body, which must be a 
                // Collection+JSON document with a filled-out template object
                // Example PUT request body:
                //
                // { "template" :
                //  {
                //   "data" : [
                //    {"prompt" : "Change the Relay State (Open or Close)", "name" : "State", "value" : "Open"}
                //   ]
                //  }
                // }
                string schemaJson = @"{
                  'description': 'A Collection+JSON document representing a filled-out template',
                  'type': 'object',
                  'properties': {
                    'template': {
                      'type':'object', 
                      'properties': {
                        'data': {
                          'type':'array', 
                          'items': {
                            'type':'object', 
                            'properties': {
                              'prompt':{'type':'string'},
                              'name':{'type':'string'},
                              'value':{'type':'string'}
                            }
                          }
                        }
                      }
                    }
                  }
                }";

                putRequestBodySchema = JsonSchema.Parse(schemaJson);

                // make the template JObject
                JObject templateObj = new JObject
                              {
                                  { "data", new JArray
                                      {
                                          new JObject
                                          {
                                              {"prompt", "Change the Relay State (Open or Close)"},
                                              {"name", "State"},
                                              {"value", ""}
                                          }
                                      }
                                  }
                              };
                templateString = JsonConvert.SerializeObject(templateObj);

            }

            // FormatItem: convert the individual Relay to a string 
            // representing the 'items' property of the Collection+JSON response
            public string FormatItem(Relay rly, string rlyUrl)
            {
                JArray items = new JArray();
                JObject item = new JObject();
                item["href"] = rlyUrl;

                JObject ID = new JObject();
                ID["name"] = "ID";
                ID["value"] = rly.ID;

                JObject State = new JObject();
                State["name"] = "State";
                State["value"] = (rly.State) ? "Closed" : "Open";

                JArray data = new JArray();
                data.Add(ID);
                data.Add(State);

                item["data"] = data;

                items.Add(item);
                return JsonConvert.SerializeObject(items);
            }

            public override void ProcessRequest(HttpCwsContext context)
            {
                uint rlyID;
                string resourceUrl = "";
                try
                {
                    resourceUrl = GetResourceUrl(context);
                    rlyID = uint.Parse((string) context.Request.RouteData.Values["id"]);
                }
                catch (Exception e) 
                {
                    CrestronConsole.PrintLine("Error while parsing {id} of individual relay from URL: " + e.Message);
                    RespondWithJsonError(context, 404, "Not Found", resourceUrl, "The requested Relay ID does not parse as an integer");
                    return;
                }
                try 
                {
                    if (rlyID >= 1 && rlyID <= server.NumberOfRelays)
                    {
                        context.Response.StatusCode = 200;
                        context.Response.StatusDescription = "OK"; // server will respond with 200 OK for a successful GET, HEAD, or PUT
                        if (context.Request.HttpMethod == "GET" || context.Request.HttpMethod == "HEAD")
                        {
                            // add method-specific response headers
                            context.Response.AppendHeader("Content-Type", "application/vnd.collection+json");
                            context.Response.AppendHeader("Link", resourceUrl + "/web-hooks" + ", rel=\"Subscriptions\""); // requirement of RESTful WebHooks standard

                            if (context.Request.HttpMethod == "GET") // add the body for the GET request
                            {
                                // return the Collection+JSON representation of the individual Relay

                                // Populate template.json with the Collection+JSON response.
                                // This response will include the "template" property, since 
                                // clients may PUT to the relay to update its state
                                string template = String.Copy(server.CjTemplate);
                                template = template.Replace("{@resource}", resourceUrl);
                                template = template.Replace("{@list}", FormatItem(server.Relays[rlyID], resourceUrl));
                                template = template.Replace("{@includetemplate}", ", \"template\": ");
                                template = template.Replace("{@template}", templateString);
                                context.Response.Write(template, false); // write the populated template as a response
                            }

                            context.Response.End();
                        }
                        else if (context.Request.HttpMethod == "PUT")
                        {
                            if (context.Request.Headers.Get("Content-Type") == "application/vnd.collection+json")
                            {
                                string json;
                                using (StreamReader s = new StreamReader(context.Request.InputStream))
                                {
                                    json = s.ReadToEnd();
                                }
                                JObject body = JsonConvert.DeserializeObject<JObject>(json);
                                if (body.IsValid(putRequestBodySchema)) 
                                {
                                    string state = ((string) body["template"]["data"][0]["name"]).ToLower();
                                    string newValue = ((string) body["template"]["data"][0]["value"]).ToLower();
                                    if (state == "state" && (newValue == "close" || newValue == "open")) 
                                    {
                                        // update internal Relay resource (true is Close, false is Open)
                                        server.Relays[rlyID].State = (newValue == "close") ? true : false; // the collection itself is 0-based, but the ID in the URL is 1-based

                                        // send the 200 OK response. No additional headers or entity-body is included in the
                                        // PUT response, but other applications may return a representation of the updated
                                        // resource
                                        context.Response.End();
                                    }
                                    else 
                                    {
                                        RespondWithJsonError(context, 400, "Bad Request", resourceUrl, "Bad name or value property in Collection+JSON template");
                                    }
                                }
                                else
                                {
                                    RespondWithJsonError(context, 400, "Bad Request", resourceUrl, "malformed Collection+JSON template provided");
                                }
                            }
                            else
                            {
                                RespondWithJsonError(context, 415, "Unsupported Media Type", resourceUrl, "supported media types: " + supportedMediaTypes);
                            }
                        }
                        else
                        {
                            RespondWithJsonError(context, 405, "Method Not Allowed", resourceUrl, "allowed methods: " + allowedMethods);
                        }
                    }
                    else
                    {
                        RespondWithJsonError(context, 404, "Not Found", resourceUrl, "The requested relay ID does not map to an available relay");
                    }
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Could not process request to " + context.Request.Url.AbsolutePath + ": " + e.Message);
                    RespondWithJsonError(context, 500, "Internal Server Error", resourceUrl, null);
                }
                finally
                {
                    CrestronConsole.PrintLine("Served response for " + context.Request.Url.AbsolutePath);
                }
            }
        }

        // handle requests to /cws/api/relays/{id}/web-hooks
        class RelaysIdWebhooksHandler : MyHttpHandler
        {
            public RelaysIdWebhooksHandler(RestfulRelayServer parent) : base(parent)
            {
                allowedMethods = "POST";
                supportedMediaTypes = "text/uri-list";
            }

            public override void ProcessRequest(HttpCwsContext context)
            {
                // Example request/response:
                // =>
                // POST http://control-system/cws/api/relays/web-hooks
                // Content-Type : text/uri-list
                // Notification-Type : UPDATED
                //
                // http://www.webscripts.com/myaccount/mylistener
                //
                // <=
                // 201 Created
                // Location : http://control-system/cws/api/relays/web-hooks/46da87b3-7997-4ca1-8203-29ae68f01a6f
                // Link : <http://control-system/cws/api/relays/web-hooks>, rel="Subscriptions"
                string resourceUrl = "";
                int rlyID;
                try
                {
                    resourceUrl = GetResourceUrl(context);
                    rlyID = int.Parse((string)context.Request.RouteData.Values["id"]);
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Error while parsing {id} of individual relay from URL: " + e.Message);
                    RespondWithJsonError(context, 404, "Not Found", resourceUrl, "The requested Relay ID does not parse as an integer");
                    return;
                }
                try
                {
                    if (context.Request.HttpMethod == "POST")
                    {
                        if (context.Request.Headers.Get("Content-Type") == "text/uri-list")
                        {
                            string callbackUrl;
                            using (StreamReader s = new StreamReader(context.Request.InputStream)) 
                            {
                                callbackUrl = s.ReadLine(); // the first line of the request's entity body must be the callback URL
                            }
                            if (context.Request.Headers.Get("Notification-Type") == "UPDATED")
                            {
                                // Create an internal subscription resource. Subscription constructor will validate the URL
                                // remove "/web-hooks" so the subscription is linked to the individual relay resource itself, not the webhook
                                Subscription newSub = new Subscription(resourceUrl.Replace("/web-hooks", ""), callbackUrl, "UPDATED", server.notifier);
                                server.sublists[rlyID].Add(newSub.SubID.ToString(), newSub);
                                CrestronConsole.PrintLine("Created new subscription resource with ID: " + newSub.SubID);
                                // send response
                                context.Response.StatusCode = 201;
                                context.Response.StatusDescription = "Created";
                                context.Response.AppendHeader("Location", resourceUrl + "/" + newSub.SubID);
                                context.Response.AppendHeader("Link", "<" + resourceUrl + ">" + ", rel=\"Subscriptions\"");
                                context.Response.End();
                            }
                            // Other notification types include "ACCESSED," "CREATED," and "DELETED," but this
                            // server's WebHooks resources will not allow them
                            else
                            {
                                RespondWithJsonError(context, 400, "Bad Request", resourceUrl, "supported Notification-Types: UPDATED");
                            }
                        }
                        else
                        {
                            RespondWithJsonError(context, 415, "Unsupported Media Type", resourceUrl, "supported Content-Types: " + supportedMediaTypes);
                        }
                    }
                    else
                    {
                        RespondWithJsonError(context, 405, "Method Not Allowed", resourceUrl, "allowed methods: " + allowedMethods);
                    }
                }
                catch (System.UriFormatException e)
                {
                    CrestronConsole.PrintLine("Malformed client request to " + context.Request.Url.AbsolutePath + ": " + e.Message);
                    RespondWithJsonError(context, 400, "Bad Request", resourceUrl, "Malformed URL provided in request body");

                }
                catch (System.ArgumentNullException e)
                {
                    CrestronConsole.PrintLine("Malformed client request to " + context.Request.Url.AbsolutePath + ": " + e.Message);
                    RespondWithJsonError(context, 400, "Bad Request", resourceUrl, "No URL provided in request body");
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Could not process request to " + context.Request.Url.AbsolutePath + ": " + e.Message);
                    RespondWithJsonError(context, 500, "Internal Server Error", resourceUrl, null);
                }
                finally
                {
                    CrestronConsole.PrintLine("Served response for " + context.Request.Url.AbsolutePath);
                }
            }
        }

        // handle requests to /cws/api/relays/{id}/web-hooks/{subid}
        class RelaysIdWebhooksSubidHandler : MyHttpHandler
        {
            public RelaysIdWebhooksSubidHandler(RestfulRelayServer parent) : base(parent)
            {
                allowedMethods = "DELETE";
            }
            public override void ProcessRequest(HttpCwsContext context)
            {
                string resourceUrl = "";
                int rlyID;
                try
                {
                    resourceUrl = GetResourceUrl(context);
                    rlyID = int.Parse((string)context.Request.RouteData.Values["id"]);
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Error while parsing {id} of individual relay from URL: " + e.Message);
                    RespondWithJsonError(context, 404, "Not Found", resourceUrl, "The requested Relay ID does not parse as an integer");
                    return;
                }
                try
                {
                    string subID = context.Request.RouteData.Values["subid"].ToString();
                    if (server.sublists[rlyID].ContainsKey(subID))
                    {
                        if (context.Request.HttpMethod == "DELETE")
                        {
                            // remove the internal subscription resource
                            server.sublists[rlyID].Remove(subID);
                            CrestronConsole.PrintLine("subscription " + subID + " has been deleted by " + context.Request.UserHostAddress);
                            // send the response
                            context.Response.StatusCode = 204;
                            context.Response.StatusDescription = "No Content";
                            context.Response.End();
                        }
                        else
                        {
                            RespondWithJsonError(context, 405, "Method Not Allowed", resourceUrl, "allowed methods: " + allowedMethods);
                        }
                    }
                    else
                    {
                        RespondWithJsonError(context, 404, "Not Found", resourceUrl, null);
                    }
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("Could not process request to " + context.Request.Url.AbsolutePath + ": " + e.Message);
                    RespondWithJsonError(context, 500, "Internal Server Error", resourceUrl, null);
                }
                finally
                {
                    CrestronConsole.PrintLine("Served response for " + context.Request.Url.AbsolutePath);
                }
            }
        }
    }
}
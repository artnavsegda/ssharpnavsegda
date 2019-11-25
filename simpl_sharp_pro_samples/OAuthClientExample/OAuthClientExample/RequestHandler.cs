using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronDataStore;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using Crestron.SimplSharp.WebScripting;
using Newtonsoft.Json;                   // Add a reference to Newtonsoft.Json.Compact.dll
using Newtonsoft.Json.Linq;

namespace OAuthClientExample
{
    /// <summary>
    /// The RequestHandler represents the bulk of the OAuth client's functionality. It serves
    /// a website at https://<control-system-hostname>/cws/ that allows the user to register, authorize,
    /// and test the client. Any HTTP requests made by the user while interacting with the website are
    /// handled in this class.
    /// </summary>
    public class RequestHandler : IHttpCwsHandler
    {


        // Templates folder and paths to each HTML template. These templates
        // contain variables in the form "{@name}", where "name" is the variable's 
        // identifier. Replacements for any such variables can be passed to the 
        // CreateHtmlBody method, allowing the server to programmatically serve the 
        // HTML webpages based on its internal state
        readonly static string templateFolder = Directory.GetApplicationDirectory() + "\\templates\\";
        readonly static string startHtml = templateFolder + "start.html";
        readonly static string registerHtml = templateFolder + "register.html";
        readonly static string unregisteredHtml = templateFolder + "unregistered.html";
        readonly static string registeredHtml = templateFolder + "registered.html";
        readonly static string errorHtml = templateFolder + "error.html";
        readonly static string testHtml = templateFolder + "test.html";
        readonly static string tokenDisposedHtml = templateFolder + "tokendisposed.html";
        readonly static string regsuccessHtml = templateFolder + "regsuccess.html";
        readonly static string authsuccessHtml = templateFolder + "authsuccess.html";

        // Represents empty token
        const string Empty = "NONE";

        // Internal flags for the OAuth client's state
        bool Registered
        {
            get
            {
                bool result;
                GetOrCreateRecord("Registered", out result);
                return result;
            }
            set 
            {
                var retVal = CrestronDataStoreStatic.SetLocalBoolValue("Registered", value);
                if (value == false) 
                {
                    // Clear all entries from the database except "Registered"
                    CrestronDataStoreStatic.clearLocal("ClientID");
                    CrestronDataStoreStatic.clearLocal("ClientSecret");
                    CrestronDataStoreStatic.clearLocal("Domain");
                    CrestronDataStoreStatic.clearLocal("RefreshToken");
                    CrestronDataStoreStatic.clearLocal("HasRefreshToken");
                }
            }
        }

        bool HasRefreshToken
        {
            get
            {
                bool result;
                GetOrCreateRecord("HasRefreshToken", out result);
                return result;
            }
            set
            {
                CrestronDataStoreStatic.SetLocalBoolValue("HasRefreshToken", value);
            }
        }

        bool hasAccessToken;


        
        string Domain
        {
            get
            {
                string result;
                GetOrCreateRecord("Domain", out result);
                return result;
            }
            set
            {
                var retVal = CrestronDataStoreStatic.SetLocalStringValue("Domain", value);
                if (retVal != CrestronDataStore.CDS_ERROR.CDS_SUCCESS)
                {
                    CrestronConsole.PrintLine("Error setting Domain: {0}", retVal);
                }
            }
        }

        string ClientID
        {
            get
            {
                string result;
                GetOrCreateRecord("ClientID", out result);
                return result;
            }
            set
            {
                CrestronDataStoreStatic.SetLocalStringValue("ClientID", value);
            }
        }
        

        EncryptionLayer el = new EncryptionLayer(); // used to encrypt/decrypt the Client Secret and the Refresh Token

        string ClientSecret 
        {
            get
            {
                string result;
                GetOrCreateRecord("ClientSecret", out result);
                if (result == Empty)
                {
                    return result; // No need to decrypt the "NONE" placeholder
                }
                return el.RSADecrypt(Convert.FromBase64String(result)); // else, decrypt the contents of the datastore and return that
            }
            set
            {
                if (value == Empty)
                {
                    // no need to encrypt the Empty constant
                    CrestronDataStoreStatic.SetLocalStringValue("ClientSecret", value);
                }
                else
                {
                    // encrypt using the key
                    byte[] encrypted = el.RSAEncrypt(value);
                    CrestronDataStoreStatic.SetLocalStringValue("ClientSecret", Convert.ToBase64String(encrypted));
                }
            }
        }

        string RefreshToken
        {
            get
            {
                string result;
                GetOrCreateRecord("RefreshToken", out result);
                if (result == Empty)
                {
                    return result; // No need to decrypt the "NONE" placeholder
                }
                return el.RSADecrypt(Convert.FromBase64String(result)); // else, decrypt the contents of the datastore and return that
            }
            set
            {
                if (value == Empty)
                {
                    // no need to encrypt the Empty constant
                    CrestronDataStoreStatic.SetLocalStringValue("RefreshToken", value);
                }
                else
                {
                    // encrypt using the RSA public key
                    byte[] encrypted = el.RSAEncrypt(value);
                    CrestronDataStoreStatic.SetLocalStringValue("RefreshToken", Convert.ToBase64String(encrypted));
                }
            }
        }

        // URLs of OAuth endpoints
        string CallbackUrl
        {
            get
            {
                // Determine the callback URL using the control system's hostname so it looks like
                // https://<hostname>/cws/callback
                return "https://" + CrestronEthernetHelper.GetEthernetParameter(
                        CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_HOSTNAME, 0).ToLower()
                        + "/cws/callback";
            }
        }

        string AuthorizationEndpoint
        {
            get
            {
                return "https://" + Domain + "/authorize";
            }
        }

        string TokenEndpoint
        {
            get
            {
                return "https://" + Domain + "/oauth/token";
            }
        }

        string ResourceEndpoint
        {
            get
            {
                return "https://" + Domain + "/userinfo";
            }
        }

        // For demonstration purposes, the Access Token will be not stored
        // persistently in the DataStore, while the Refresh Token will be.
        string accessToken = Empty;

        // secure, one-time use strings
        string stateString;
        string authorizationCode;


        // match every HTTP response status code the CWS server uses to its corresponding description
        static readonly Dictionary<int, string> statusDescriptions = new Dictionary<int, string>
        { 
            { 200, "OK" },
            { 302, "Found" },
            { 400, "Bad Request" },
            { 401, "Unauthorized" },
            { 403, "Forbidden" },
            { 404, "Not Found" },
            { 405, "Method Not Allowed" }, 
            { 415, "Unsupported Media Type" },
            { 500, "Internal Server Error" }
        };

        public RequestHandler()
        {
            try
            {
                CrestronConsole.PrintLine("DataStore init: {0}", CrestronDataStoreStatic.InitCrestronDataStore());
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in RequestHandler(): {0}", e);
            }
        }

        /// <summary>
        /// Get the specified record from the Datastore, or create it if no record exists. Return a bool indicating
        /// whether the record already existed when this method was called
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        bool GetOrCreateRecord(string tag, out string value)
        {
            try
            {
                var retVal = CrestronDataStoreStatic.GetLocalStringValue(tag, out value);
                if (retVal != CrestronDataStore.CDS_ERROR.CDS_SUCCESS)
                {
                    CrestronDataStoreStatic.SetLocalStringValue(tag, Empty);
                    value = Empty;
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("GetOrCreateRecord: {0}", e);
                value = "";
                return false;
            }
        }

        /// <summary>
        /// Get the specified record from the Datastore, or create it if no record exists. Return a bool indicating
        /// whether the record already existed when this method was called
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        bool GetOrCreateRecord(string tag, out bool value)
        {
            try
            {
                var retVal = CrestronDataStoreStatic.GetLocalBoolValue(tag, out value);
                if (retVal != CrestronDataStore.CDS_ERROR.CDS_SUCCESS)
                {
                    CrestronDataStoreStatic.SetLocalBoolValue(tag, false);
                    value = false;
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("GetOrCreateRecord: {0}", e);
                value = false;
                return false;
            }
        }

        /// <summary>
        /// Send an HTTP error response by specifying the status code and message. 
        /// In the case of a 405 response, the caller should also include the allowed
        /// HTTP methods for the requested resource
        /// </summary>
        /// <param name="context"></param>
        /// <param name="code"></param>
        /// <param name="errorMsg"></param>
        /// <param name="allowedMethods"></param>
        void SendError(HttpCwsContext context, int code, string errorMsg, params string[] allowedMethods)
        {
            context.Response.StatusCode = code;
            context.Response.StatusDescription = statusDescriptions[code];
            if (code == 405)
            {
                string allowHeader = "";
                // Add each allowed method in a comma-separated list
                for (int i = 0; i < allowedMethods.Length; i++)
                {
                    allowHeader += allowedMethods[i].ToUpper();
                    if (i != allowedMethods.Length - 1)
                    {
                        allowHeader += ", ";
                    }
                }
                // Add the finished header to the HTTP response and to the response body
                context.Response.AppendHeader("Allow", allowHeader);
            }

            // Send the error response
            context.Response.ContentType = "text/html";
            context.Response.Write(CreateHtmlBody(errorHtml,
                                    new NameValueCollection() { {"error" , errorMsg } }),
                                    true);
        }

        public void ProcessRequest(HttpCwsContext context)
        {
            try
            {
                Uri requestUri = context.Request.Url;
                string method = context.Request.HttpMethod;
                string path = context.Request.Path.ToLower();
                switch (path)
                {
                    case "/cws/":                 
                        if (method == "GET" || method == "HEAD")
                        {
                            context.Response.StatusCode = 200;
                            context.Response.StatusDescription = "OK";
                            context.Response.ContentType = "text/html";
                            // include the body for the GET request, but not HEAD
                            if (method == "GET")
                            {
                                context.Response.Write(CreateHtmlBody(startHtml,
                                    // Use the current state to decide whether to enable the buttons or not
                                                       new NameValueCollection() 
                                                       {
                                                           { "enable", 
                                                               Registered ? "" : "disabled" },
                                                           { "enable-forget-access", 
                                                               (Registered && hasAccessToken) ? "" : "disabled" },
                                                           { "enable-forget-refresh", 
                                                               (Registered && HasRefreshToken) ? "" : "disabled" }
                                                       }),
                                                       false);
                            }
                            context.Response.End();
                        }
                        else
                        {
                            SendError(context, 405, context.Request.HttpMethod + " not allowed", "GET", "HEAD");
                        }
                        break;
                    case "/cws/register":
                        if (method == "GET" || method == "HEAD")
                        {
                            context.Response.StatusCode = 200;
                            context.Response.StatusDescription = "OK";
                            context.Response.ContentType = "text/html";
                            // include the body for the GET request, but not HEAD
                            if (method == "GET")
                            {
                                string body = Registered ? File.ReadToEnd(registeredHtml, Encoding.UTF8) : 
                                                            File.ReadToEnd(unregisteredHtml, Encoding.UTF8);
                                
                                body = CreateHtmlBody(registerHtml,
                                new NameValueCollection()
                                {
                                    { "register", body }
                                });
                                if (!Registered)
                                {
                                    // Make two additional variable evaluations on the "unregistered" HTML page
                                    string scheme = context.Request.Url.Scheme;
                                    body = body.Replace("{@callbackUrl}", CallbackUrl);
                                    body = body.Replace("{@httpsAlert}", (scheme == "https") ? "" :
                                                "<p>WARNING: It looks like you got here via http, not https."
                                                + " Check that SSL is enabled"
                                                + " on the control system. Don't submit anything until it is!</p>");
                                }
                                context.Response.Write(body, false);
                            }
                            context.Response.End();
                        }
                        else if (method == "POST") 
                        {
                            // Get the entity-body, which should contain the domain, client_id, and client_secret
                            // parameters
                            if (context.Request.ContentType != "application/x-www-form-urlencoded")
                            {
                                SendError(context, 415, "Content-Type must be application/x-www-form-urlencoded");
                                return;
                            }
                            // POST requests from HTML forms put their parameters in the request body 
                            // using the application/x-www-form-urlencoded content type. An example can be found
                            // at https://developer.mozilla.org/en-US/docs/Web/HTTP/Methods/POST
                            string requestBody;
                            // Parse the entity-body of the request and check for correctness
                            using (StreamReader sr = new StreamReader(context.Request.InputStream))
                            {
                                requestBody = sr.ReadToEnd();
                            }
                            NameValueCollection keyValuePairs = ParseQueryParams(requestBody);
                            
                            string[] keys = keyValuePairs.AllKeys; 

                            // Handler for the "Unregister and Return to Home Page" button
                            if (keys.Contains("unregister"))
                            {
                                Registered = false;
                                context.Response.Redirect("/cws/", true);
                                return;
                            }

                            // Check that all necessary registration fields (client_id, client_secret, and domain)
                            // have been provided
                            if (String.IsNullOrEmpty(keyValuePairs["domain"]) ||
                                String.IsNullOrEmpty(keyValuePairs["client_id"]) ||
                                String.IsNullOrEmpty(keyValuePairs["client_secret"]))
                            {
                                // Error, bad request
                                SendError(context, 400, "Registration submission is missing a necessary field");
                                return; 
                            }
                            // Save the values provided to the datastore
                            Domain = keyValuePairs["domain"];
                            ClientID = keyValuePairs["client_id"];
                            ClientSecret = keyValuePairs["client_secret"];
 
                            // Send OK response
                            context.Response.StatusCode = 200;
                            context.Response.StatusDescription = statusDescriptions[200];
                            context.Response.Write(CreateHtmlBody(regsuccessHtml,
                                            new NameValueCollection() 
                                            {
                                                { "domain", Domain },
                                                { "client_id", ClientID }
                                            }), true);

                            // Now the client is registered with the Authorization Server
                            Registered = true;
                        }
                        else
                        {
                            SendError(context, 405, context.Request.HttpMethod + " not allowed", "GET", "HEAD", "POST");
                        }
                        break;
                    case "/cws/authorize":
                        if (!Registered)
                        {
                            SendError(context, 404, "The client must be registered with the Auth0 Authorization server" +
                                                    " before it can get authorized");
                            return;
                        }
                        if (method == "GET" || method == "HEAD")
                        {
                            // First, discard the old set of tokens
                            accessToken = Empty;
                            hasAccessToken = false;
                            RefreshToken = Empty;
                            HasRefreshToken = false;

                            // Generate a random state parameter, which the callback URL will check for
                            // consistency
                            stateString = Guid.NewGuid().ToString("N");
                            NameValueCollection queries = 
                                new NameValueCollection()
                                {
                                    // necessary scopes to get full user info and to get a Refresh Token
                                    { "scope", "offline_access%20openid%20email%20profile" }, 
                                    // Authorization code flow
                                    { "response_type", "code"},         
                                    { "client_id", ClientID },
                                    { "redirect_uri", CallbackUrl },
                                    { "state", stateString },
                                    // Always show the login and consent pages upon redirecting
                                    { "prompt", "login%20consent" }
                                };

                            context.Response.StatusCode = 302;
                            context.Response.StatusDescription = statusDescriptions[302];
                            // Contruct the redirect URL and send the user to Auth0's authorization endpoint
                            context.Response.AppendHeader("Location", BuildAuthorizationUrl(queries));                            
                            context.Response.End();
                        }
                        else
                        {
                            SendError(context, 405, context.Request.HttpMethod + " not allowed", "GET", "HEAD");
                        }
                        break;
                    case "/cws/callback":
                        if (method == "HEAD" || method == "GET")
                        {
                            context.Response.StatusCode = 200;
                            context.Response.StatusDescription = statusDescriptions[200];
                            if (method == "GET")
                            {
                                // Check if there's a query string
                                if (context.Request.Url.Query == null ||
                                    context.Request.Url.Query == "")
                                {
                                    SendError(context, 400, "No Query parameters provided");
                                    return;
                                }
                                NameValueCollection queryParams = null;
                                // Parse query string and check state
                                try
                                {
                                    queryParams = ParseQueryParams(context.Request.Url.Query);
                                }
                                catch (Exception e)
                                {
                                    SendError(context, 400, "Query string parsing error: " + e.Message);
                                    return;
                                }
                                if (queryParams["state"] != stateString)
                                {
                                    CrestronConsole.PrintLine("State parameter mismatch: expected " +
                                                stateString + " but got " + queryParams["state"]);
                                    SendError(context, 400, "State parameter did not match.");
                                    return;
                                }
                                authorizationCode = queryParams["code"];
                                
                                if (String.IsNullOrEmpty(authorizationCode))
                                {
                                    // Authorization was denied by the user
                                    SendError(context, 400, 
                                        "Authorization was denied. The OAuth Client has no Access or Refresh token now");
                                    return;
                                }

                                // Using an HttpsClient, exchange the received authorization code for an Access Token
                                using (HttpsClient client = new HttpsClient())
                                {
                                    HttpsClientRequest req = new HttpsClientRequest();
                                    req.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                                    req.Url = new UrlParser(TokenEndpoint);
                                    CrestronConsole.PrintLine("GETting {0}", req.Url);
                                    HttpsHeaders headers = new HttpsHeaders();
                                    // Auth0's token endpoint expects all the necessary information to be 
                                    // placed in the entity-body of the request
                                    headers.SetHeaderValue("Content-Type", "application/x-www-form-urlencoded");

                                    req.ContentString = BuildQueryString(
                                            new NameValueCollection 
                                            {
                                                { "grant_type", "authorization_code" },
                                                { "client_id", ClientID },
                                                { "client_secret", ClientSecret },
                                                { "code", authorizationCode },
                                                { "redirect_uri", CallbackUrl }
                                            });

                                    // Always set the Content-Length of your POST request to indicate the length of the body,
                                    // or else the Content-Length will be set to 0 by default! 
                                    headers.SetHeaderValue("Content-Length", req.ContentString.Length.ToString());
                                    req.Header = headers;

                                    // Send the POST request to the token endpoint and wait for a response...
                                    HttpsClientResponse tokenResponse = client.Dispatch(req);
                                    if (tokenResponse.Code >= 200 && tokenResponse.Code < 300)
                                    {
                                        // Parse JSON response and securely store the tokens
                                        JObject resBody = JsonConvert.DeserializeObject<JObject>(tokenResponse.ContentString);
                                        accessToken = (string)resBody["access_token"];
                                        hasAccessToken = true;
                                        RefreshToken = (string)resBody["refresh_token"];
                                        HasRefreshToken = true;
                                        CrestronConsole.PrintLine("Received \"{0}\" Access/Refresh Tokens. They expire in {1} hours",
                                                                        (string)resBody["token_type"],
                                                                        (int)resBody["expires_in"]/60.0/60.0);
                                        // Respond
                                        context.Response.Write(CreateHtmlBody(authsuccessHtml, null), false);
                                    }
                                    else
                                    {
                                        SendError(context, tokenResponse.Code,
                                                "Could not get access/refresh tokens. Token endpoint returned code " +
                                                "<strong>" + tokenResponse.Code + "</strong>" +
                                                "<br /><br />Error Message:<br /><br /><strong>" 
                                                + tokenResponse.ContentString + "</strong>");
                                        return;
                                    }
                                }
                            }
                            context.Response.End();
                        }
                        else
                        {
                            SendError(context, 405, context.Request.HttpMethod + " not allowed", "GET", "HEAD");
                        }
                        break;
                    case "/cws/test":
                        if (method == "GET" || method == "HEAD")
                        {
                            if (method == "GET")
                            {
                                if (!Registered)
                                {
                                    SendError(context, 404, "The client must be registered before it can try to access" +
                                                            " a protected resource");
                                    return;
                                }

                                // Send a GET request to the protected resource
                                using (HttpsClient client = new HttpsClient())
                                {
                                    HttpsClientRequest req = new HttpsClientRequest();
                                    req.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Get;
                                    req.Url = new UrlParser(ResourceEndpoint);
                                    CrestronConsole.PrintLine("GETting {0}", req.Url);
                                    // Put the Access Token in the Authorization header, if it's present 
                                    if (hasAccessToken)
                                    {
                                        req.Header.SetHeaderValue("Authorization", "Bearer " + accessToken);
                                    }
                                    var res = client.Dispatch(req);
                                    if (res.Code >= 200 && res.Code < 300)
                                    {
                                        JObject resBody =
                                            JsonConvert.DeserializeObject<JObject>(res.ContentString);
                                        string name, email, picUrl;
                                        bool verified;
                                        // Try to get the username from any of these three JSON properties,
                                        // if they are defined. If none of them are defined, put "undefined" as a 
                                        // placeholder name
                                        name = (string) resBody["given_name"] ??
                                               (string) resBody["nickname"] ??
                                               (string) resBody["email"] ?? 
                                                "undefined";
                                        email = (string) resBody["email"] ?? "undefined";
                                        picUrl = (string) resBody["picture"] ?? "undefined";
                                        verified = (bool?) resBody["email_verified"] ?? false;

                                        context.Response.Write(CreateHtmlBody(testHtml, 
                                            new NameValueCollection() 
                                            { 
                                                { "name", name },
                                                { "email", email },
                                                { "verified", "<strong>" + (verified ? "verified" : "not verified") 
                                                                    + "</strong>"},
                                                { "pic_url", picUrl },
                                            }), false);
                                    } 
                                    else {
                                        // If RefreshAccessToken succeeds, accessToken will contain the new token
                                        // If not, display the body of the error response returned from the 
                                        // authorization server
                                        string errMsg;
                                        int errCode;
                                        if (RefreshAccessToken(out errMsg, out errCode))
                                        {
                                            CrestronConsole.PrintLine("Successfully refreshed the Access Token." +
                                                " Redirecting user to /cws/Test...");
                                            context.Response.Redirect("/cws/Test", true);
                                            return;
                                        }
                                        else
                                        {
                                            SendError(context, errCode,
                                                "Failed to refresh Access Token." + 
                                                " Resource endpoint returned code <strong>" + errCode + "</strong>" +
                                                "<br /><br />Error Message:<br /><br /><strong>" + errMsg + "</strong>");
                                            return; 
                                        }
                                    }
                                    context.Response.End();
                                }
                            }
                        }
                        else
                        {
                            SendError(context, 405, context.Request.HttpMethod + " not allowed", "GET", "HEAD");
                        }
                        break;
                    case "/cws/forgetaccess":
                        if (method == "GET" || method == "HEAD")
                        {
                            context.Response.StatusCode = 200;
                            context.Response.StatusDescription = statusDescriptions[200];
                            if (method == "GET")
                            {
                                if (hasAccessToken)
                                {
                                    accessToken = Empty;
                                    hasAccessToken = false;
                                    context.Response.Write(CreateHtmlBody(tokenDisposedHtml,
                                        new NameValueCollection
                                        {
                                            { "token_kind", "Access" }
                                        }), false);
                                }
                                else
                                {
                                    SendError(context, 404, "There is currently no Access Token to be disposed of");
                                } 
                            }
                            context.Response.End();
                        }
                        else
                        {
                            SendError(context, 405, context.Request.HttpMethod + " not allowed", "GET", "HEAD");
                        }
                        break;
                    case "/cws/forgetrefresh":
                        if (method == "GET" || method == "HEAD")
                        {
                            context.Response.StatusCode = 200;
                            context.Response.StatusDescription = statusDescriptions[200];
                            if (method == "GET")
                            {
                                if (HasRefreshToken)
                                {
                                    RefreshToken = Empty;
                                    HasRefreshToken = false;
                                    context.Response.Write(CreateHtmlBody(tokenDisposedHtml,
                                        new NameValueCollection
                                        {
                                            { "token_kind", "Refresh" }
                                        }), false);
                                }
                                else
                                {
                                    SendError(context, 404, "There is currently no Refresh Token to be disposed of");
                                }
                            }
                            context.Response.End();
                        }
                        else
                        {
                            SendError(context, 405, context.Request.HttpMethod + " not allowed", "GET", "HEAD");
                        }
                        break;
                    default:
                        SendError(context, 404, path + " not found");
                        break;

                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error in ProcessRequest(): {0}", e);
                try
                {
                    SendError(context, 500, "An error occurred in the CWS server: " + e.Message);
                    CrestronConsole.PrintLine("Sent " + context.Response.StatusCode + 
                        " response to " + context.Request.UserHostName);

                }
                catch (Exception ex)
                {
                    CrestronConsole.PrintLine("Could not send Error response: {0}", ex);
                }
            }
            finally
            {
                CrestronConsole.PrintLine("Sent " + context.Response.StatusCode +
                    " response to " + context.Request.UserHostName);
            }
        }

        /// <summary>
        /// Attempt to refresh the access token, return a boolean indicating success (true)/failure (false), and
        /// include the response code and body from the Authorization Server in the event of failure
        /// </summary>
        /// <param name="errMsg"></param>
        /// <param name="statusCode"></param>
        /// <returns></returns>
        bool RefreshAccessToken(out string errMsg, out int statusCode)
        {
            using (var client = new HttpsClient())
            {
                CrestronConsole.PrintLine("Attempting to refresh Access Token...");
                HttpsClientRequest req = new HttpsClientRequest();
                req.RequestType = Crestron.SimplSharp.Net.Https.RequestType.Post;
                req.Url = new UrlParser(TokenEndpoint);
                HttpsHeaders headers = new HttpsHeaders();
                // Auth0's token endpoint expects all the necessary information to be 
                // placed in the entity-body of the request
                headers.SetHeaderValue("Content-Type", "application/x-www-form-urlencoded");
                req.ContentString = BuildQueryString(
                        new NameValueCollection 
                                            {
                                                { "grant_type", "refresh_token" },
                                                { "client_id", ClientID },
                                                { "client_secret", ClientSecret },
                                                { "refresh_token", RefreshToken }
                                            });

                // Always set the Content-Length of your POST request to indicate the length of the body,
                // or else the Content-Length will be set to 0 by default! 
                headers.SetHeaderValue("Content-Length", req.ContentString.Length.ToString());
                req.Header = headers;

                // Send the POST request to the token endpoint and wait for a response...
                HttpsClientResponse tokenResponse = client.Dispatch(req);
                if (tokenResponse.Code >= 200 && tokenResponse.Code < 300)
                {
                    // Parse JSON response and securely store the token
                    JObject resBody = JsonConvert.DeserializeObject<JObject>(tokenResponse.ContentString);
                    accessToken = (string)resBody["access_token"];
                    CrestronConsole.PrintLine("Received a new \"{0}\" Access Token. It expires in {1} hours",
                                                    (string)resBody["token_type"],
                                                    (int)resBody["expires_in"]/60.0/60.0);
                    // Client is now authorized to access the protected resource again
                    hasAccessToken = true;
                    errMsg = "";
                    statusCode = tokenResponse.Code;
                    return true;
                }
                else 
                {
                    CrestronConsole.PrintLine("Refresh Failed");
                    errMsg = tokenResponse.ContentString; 
                    statusCode = tokenResponse.Code;
                    return false;
                }
            }
        }


        /// <summary>
        /// Given a query string, parse the parameter key-value pairs and return a 
        /// NameValueCollection representing them. This method will trim off the '?' delimiter if present
        /// </summary>
        /// <param name="queryString"></param>
        /// <returns></returns>
        NameValueCollection ParseQueryParams(string queryString)
        {
            queryString = queryString.TrimStart(' ', '?');
            // Separate each key/value pair using the '&' delimiter
            string[] tuples = queryString.Split('&');
            NameValueCollection keyValuePairs = new NameValueCollection();
            foreach (string tuple in tuples)
            {
                // Parse the key from the value based on the '=' sign
                keyValuePairs.Add(tuple.Substring(0, tuple.IndexOf('=')),
                                  tuple.Substring(tuple.IndexOf('=') + 1));

            }
            return keyValuePairs;
        }

        /// <summary>
        /// Given the provided key-value pairs in queries, build a query string like "foo=bar&next=etc"
        /// </summary>
        /// <param name="queries"></param>
        /// <returns></returns>
        string BuildQueryString(NameValueCollection queries)
        {
            StringBuilder queryString = new StringBuilder();
            for (int i = 0; i < queries.Count; i++)
            {
                queryString.Append(queries.GetKey(i) + '=' + queries[i]);
                if (i != queries.Count - 1)
                    queryString.Append('&'); // Append '&' for each parameter except the last
            }
            return queryString.ToString();
        }

        /// <summary>
        /// Build the Authorization URL by attaching the query string built from the queries input
        /// to the Authorization endpoint
        /// </summary>
        /// <param name="queries"></param>
        /// <returns></returns>
        string BuildAuthorizationUrl(NameValueCollection queries) 
        {
            // Append the query string to the end of the endpoint (right after the path ends) using the '?' delimiter
            // and return the resulting URL string
            return AuthorizationEndpoint + "?" + BuildQueryString(queries);
        }

        /// <summary>
        /// Populate the HTML template by replacing the specified variables with the 
        /// values provided in the locals collection, then return the HTML as a string.
        /// templateFilename is an absolute path to the HTML file.
        /// </summary>
        /// <param name="filename">The path to the template file</param>
        /// <param name="locals">A collection of local variables to be replaced in the template.</param>
        string CreateHtmlBody(string templateFilename, NameValueCollection locals)
        {
            try 
            {
                // First, open and read the template file
                // UTF-8 is the standard encoding for HTML. The parameter to the constructor indicates there
                // won't be a byte-order mark at the beginning of the file
                UTF8Encoding enc = new UTF8Encoding(false);
                string body = File.ReadToEnd(templateFilename, enc); 

                // perform string replacement to construct the HTTP response body from the template
                if (locals != null)
                {
                    foreach (var key in locals.AllKeys) 
                    {
                        // locals[key] is the replaced string, such as an error description
                        // or a conditionally-sent message
                        body = body.Replace("{@" + key + "}", locals[key]);
                    }
                } 
                // return the HTML body
                return body;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Throwing exception in CreateHtmlBody()");
                throw e; // throw the exception up to ProcessRequest
            }
        }
    }
}
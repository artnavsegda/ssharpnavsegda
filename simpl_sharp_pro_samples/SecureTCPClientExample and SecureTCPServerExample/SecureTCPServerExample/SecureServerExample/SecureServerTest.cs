using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;               // For SecureTCPServer
using Crestron.SimplSharp.Cryptography.X509Certificates; // For X.509 certificates
using Crestron.SimplSharp.CrestronIO;                    // For FileStream, BinaryReader, and Directory

namespace SecureServerExample
{
    public class SecureServer
    {
        SecureTCPServer server;

        public SecureServer() 
        {
            CrestronConsole.AddNewConsoleCommand(Listen, "listen", "usage: listen [<cert_file> <key_file>] <port>", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(Disconnect, "disconnect", "usage: disconnect [<client_index>]", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(ShowStatus, "showstatus", "usage: showstatus [<client_index>]", ConsoleAccessLevelEnum.AccessOperator);
            server = null;
        }

        // Console command implementations

        public void Listen(string args_str)
        {
            if (server != null)
            {
                CrestronConsole.PrintLine("Server is already online. Disconnect it first");
                return;
            }

            int bufsize = 100; // sample size for the server sockets' incoming data buffers
            int max_connections = 3; // sample size for the maximum number of simultaneous server sockets

            // Parse command-line arguments
            // You can optionally associate the client object with a certifiate and private key, which both must be in DER format, from the file system
            // For this particular example, the filenames must not contains spaces and must be located in the application directory
            string[] args = args_str.Split(' ');
            if (args.Length != 1 && args.Length != 3)
            {
                CrestronConsole.PrintLine("usage: listen [<cert_file> <key_file>] <port>");
                return;
            }
            bool provideCert = false;
            string cert_fn = null; // certificate filename
            string key_fn = null; // private key filename
            int start = 0; // starting index of the hostname and port arguments in args
            if (args.Length == 3) // user provides filenames for the cert/key before the hostname and port arguments.
            {
                provideCert = true;
                cert_fn = args[0];
                key_fn = args[1];
                start += 2;
            }
            int port = 0;
            try
            {
                port = int.Parse(args[start]);
            }
            catch
            {
                PrintAndLog("Error: port number passed in is not numeric");
                return;
            }

            if (port > 65535 || port < 0)
            {
                CrestronConsole.PrintLine("Port number is out of range");
                return;
            }

            ErrorLog.Notice("Instantiating server object...");
            try
            {
                server = new SecureTCPServer(port, bufsize, EthernetAdapterType.EthernetUnknownAdapter, max_connections);
                server.SocketStatusChange += new SecureTCPServerSocketStatusChangeEventHandler(ServerSocketStatusChanged);
            }
            catch (Exception e)
            {
                PrintAndLog("Error encountered while instantiating the server object: " + e.Message);
                return;
            }

            if (provideCert)
            {
                X509Certificate cert;
                byte[] key;

                // Populate cert and key
                loadCertAndKey(cert_fn, key_fn, out cert, out key);

                // Set the server's certificate and private key

                /*
                 * The X509Certificate passed to SetServerCertificate should have the following attributes in these extension
                 * fields:
                 * 
                 * [...]
                 * X509v3 Basic Constraints: critical
                 *     CA:FALSE
                 * X509v3 Key Usage: critical
                 *     Digital Signature, Key Encipherment, Key Agreement
                 * X509v3 Extended Key Usage: 
                 *     TLS Web Client Authentication, TLS Web Server Authentication
                 * [...]
                 */
                // Only call SetServerCertificate and SetServerPrivateKey if loadCertAndKey succeeded in populating cert and key.
                // Otherwise, the server will be associated with a default key and certificate determined by the control system's SSL settings
                if (cert != null && key != null)
                {
                    PrintAndLog("Associating user-specified certificate and key with server...");
                    server.SetServerCertificate(cert);

                    // The private key set here must correspond to the public key embedded in the server's certificate
                    server.SetServerPrivateKey(key);
                }
                else
                {
                    PrintAndLog("Associating default certificate and key with server...");
                }
            }
            SocketErrorCodes err;

            ErrorLog.Notice("Begin listening for clients...");

            // ServerConnectedCallback will get invoked once a client either  
            // connects successfully or if the connection encounters an error
            err = server.WaitForConnectionAsync(ServerConnectedCallback);
            PrintAndLog("WaitForConnectionAsync returned: " + err);
        }

        public void Disconnect(string args) {
            try
            {
                if (server == null)
                {
                    CrestronConsole.PrintLine("Server is already disconnected");
                    return;
                }
                if (args == "")
                { // When no arguments are provided, disconnect from all clients and destroy the server
                    server.DisconnectAll();
                    CrestronConsole.PrintLine("Server has disconnected from all clients and is no longer listening on port " + server.PortNumber);
                    server = null;
                }
                else
                { // Disconnect from the client specified by the user
                    uint clientIndex = uint.Parse(args);
                    if (server.ClientConnected(clientIndex))
                    {
                        server.Disconnect(clientIndex);
                        CrestronConsole.PrintLine("Server has disconnected from " + clientIndex);
                    }
                    else
                    {
                        CrestronConsole.PrintLine("Client #" + clientIndex + " does not exist currently");
                    }
                }
            }
            catch (Exception e)
            {
                PrintAndLog("Error in Disconnect: " + e.Message);
            }
        }

        // Callback methods

        public void HandleLinkLoss()
        {
            server.HandleLinkLoss();
            CrestronConsole.PrintLine("HandleLinkLoss: Server state is now " + server.State);
        }

        public void HandleLinkUp()
        {
            server.HandleLinkUp();
            CrestronConsole.PrintLine("HandleLinkUp: Server state is now " + server.State);
        }

        // Show the status of the client index if specified, and always show the current server state
        public void ShowStatus(string arg)
        {
            
            if (server != null)
            {
                if (arg != "") // Optionally, the user may specify a client index to show that client's socket status
                {
                    uint clientIndex = 0;
                    try
                    {
                        clientIndex = uint.Parse(arg);
                    }
                    catch
                    {
                        CrestronConsole.PrintLine("Could not parse clientIndex");
                        return;
                    }
                    CrestronConsole.PrintLine("Socket status of client " + clientIndex + ": " + server.GetServerSocketStatusForSpecificClient(clientIndex));
                }
                CrestronConsole.PrintLine("Server state is: " + server.State); // If the SecureTCPServer object exists, always show its state
            }
            else
            {
                CrestronConsole.PrintLine("No server exists");
            }
        }

        public void ServerSocketStatusChanged(SecureTCPServer server, uint clientIndex, SocketStatus status)
        {
            // A single SecureTCPServer may be handling many different sockets (up to server.MaxNumberOfClientSupported) at once. 
            // This event handler is called whenever the status of any one of these sockets changes. clientIndex
            // uniquely identifies which socket has received a new status.

            if (status == SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                CrestronConsole.PrintLine("ServerSocketStatusChanged: Client " + clientIndex + " connected.");
            }
            else
            {
                CrestronConsole.PrintLine("ServerSocketStatusChange for client " + clientIndex + ": " + status + ".");
            }
        }
        
        public void ServerConnectedCallback(SecureTCPServer server, uint clientIndex)
        {
            if (clientIndex != 0)
            {
                CrestronConsole.PrintLine("Server listening on port " + server.PortNumber + " has connected with a client (client #"+ clientIndex + ")");
                server.ReceiveDataAsync(clientIndex, ServerDataReceivedCallback);
                if (server.MaxNumberOfClientSupported == server.NumberOfClientsConnected)
                {
                    CrestronConsole.PrintLine("Client limit reached.");
                    // This call to Stop() causes the server.State flag, SERVER_NOT_LISTENING, to be set
                    server.Stop();
                    CrestronConsole.PrintLine("After calling server.Stop(), the server state is: " + server.State);
                }
                // If the client limit is reached, WaitForConnectionAsync will return SOCKET_MAX_CONNECTIONS_REACHED
                // and the ServerConnectedCallback will not be registered. Otherwise, the call to WaitForConnectionAsync
                // causes the server to keep listening for more clients.
                SocketErrorCodes err = server.WaitForConnectionAsync(ServerConnectedCallback);
                PrintAndLog("WaitForConnectionAsync returned: " + err); 
            }
            // A clientIndex of 0 could mean that the server is no longer listening, or that the TLS handshake failed when a client tried to connect.
            // In the case of a TLS handshake failure, wait for another connection so that other clients can still connect
            else
            {
                CrestronConsole.Print("Error in ServerConnectedCallback: ");
                if ((server.State & ServerState.SERVER_NOT_LISTENING) > 0)
                {
                    CrestronConsole.PrintLine("Server is no longer listening.");
                }
                else
                {
                    CrestronConsole.PrintLine("Unable to make connection with client.");
                    // This connection failed, but keep waiting for another
                    server.WaitForConnectionAsync(ServerConnectedCallback);
                }
            }
        }

        public void ServerDataReceivedCallback(SecureTCPServer server, uint clientIndex, int bytesReceived)
        {
            if (bytesReceived <= 0) {
                CrestronConsole.PrintLine("ServerDataReceivedCallback error: server's connection with client " + clientIndex + " has been closed.");
                server.Disconnect(clientIndex);
                // A connection has closed, so another client may connect if the server stopped listening 
                // due to the maximum number of clients connecting
                if ((server.State & ServerState.SERVER_NOT_LISTENING) > 0)
                    server.WaitForConnectionAsync(ServerConnectedCallback);     
            }
            else {
                CrestronConsole.PrintLine("\n------ incoming message -----------");
                byte[] recvd_bytes = new byte[bytesReceived];

                // Copy the received bytes into a local buffer so that they can be echoed back.
                // Do not pass the reference to the incoming data buffer itself to the SendDataAsync method
                Array.Copy(server.GetIncomingDataBufferForSpecificClient(clientIndex), recvd_bytes, bytesReceived);

                // The server in this example expects ASCII text from the client, but any other encoding is possible
                string recvd_msg = ASCIIEncoding.ASCII.GetString(recvd_bytes, 0, bytesReceived);
                CrestronConsole.PrintLine("Client " + clientIndex + " says: " + recvd_msg + "\r\nEchoing back to client " + clientIndex + "...");
                
                // echo the received message back to the client who sent it
                server.SendDataAsync(clientIndex, recvd_bytes, recvd_bytes.Length, ServerDataSentCallback);

                // Begin waiting for another message from that same client
                server.ReceiveDataAsync(clientIndex, ServerDataReceivedCallback);

                CrestronConsole.PrintLine("---------- end of message ----------");
            }
        }

        public void ServerDataSentCallback(SecureTCPServer server, uint clientIndex, int bytesSent)
        {
            if (bytesSent <= 0)
            {
                CrestronConsole.PrintLine("Error sending message. Connection has been closed");
            }
            else
            {
                CrestronConsole.PrintLine("Echoed message to client " + clientIndex + " (" + bytesSent + " byte(s))");
            }
        }

        /// <summary>
        /// Helper method to load a certificate and key from the Application directory 
        /// (as determined by Directory.GetApplicationDirectory())
        /// </summary>
        /// <param name="cert_fn"> File name of the certificate to be loaded to the controller </param>
        /// <param name="key_fn"> File name of the matching private key for the controller </param>
        /// <param name="cert"> The X509Certificate, in DER format, read from cert_fn </param>
        /// <param name="key"> The byte[] for the private key, in DER format, taken from key_fn </param>
        /// <returns></returns>
        static void loadCertAndKey(string cert_fn, string key_fn, out X509Certificate cert, out byte[] key)
        {
            PrintAndLog("Loading certificate and key from " + cert_fn + " and " + key_fn + " in directory " + Directory.GetApplicationDirectory() + "...");
            byte[] bufCert; // intermediate buffer used to construct the X509Certificate
            try
            {
                using (FileStream fs = File.OpenRead(Directory.GetApplicationDirectory() + "\\" + cert_fn))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    bufCert = br.ReadBytes((int)fs.Length);
                    br.Close();
                }

                CrestronConsole.Print("Generating X509Certificate object... ");
                cert = new X509Certificate2(bufCert);
                CrestronConsole.PrintLine("done");

                // Extract the byte array from the key file
                using (FileStream fs = File.OpenRead(Directory.GetApplicationDirectory() + "\\"+ key_fn))
                using (BinaryReader br = new BinaryReader(fs))
                {
                    key = br.ReadBytes((int)fs.Length);
                    br.Close();
                }
            }
            catch (Exception e)
            {
                cert = null;
                key = null;
                PrintAndLog("Error loading certificate and key: " + e.Message);
            }
        }

        static void PrintAndLog(string message)
        {
            CrestronConsole.PrintLine(message);
            ErrorLog.Notice(message);
        }
    }
}

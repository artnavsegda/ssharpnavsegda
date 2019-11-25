using System;
using System.Text;                                       // for ASCIIEncoding      
using Crestron.SimplSharp;                               // for CrestronConsole and ConsoleAccessLevelEnum
using Crestron.SimplSharp.CrestronSockets;               // for SecureTCPClient and SecureTCPServer
using Crestron.SimplSharp.Cryptography.X509Certificates; // for X509Certificate
using Crestron.SimplSharp.CrestronIO;                    // For FileStream, BinaryReader, and Directory

namespace SecureClientExample
{
    public class SecureClient : IDisposable
    {
        SecureTCPClient client;

        public SecureClient()
        {
            CrestronConsole.AddNewConsoleCommand(this.Connect, "connect", "usage: connect [<cert_file> <key_file>] <hostname> <port>", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(this.SendMessage, "send", "send <msg>", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(this.Disconnect, "disconnect", "disconnect from server if currently connected", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(this.ShowStatus, "showstatus", "show the client socket's current status", ConsoleAccessLevelEnum.AccessOperator);
            client = null;
        }

        public void Connect(string args_str)
        {
            if (client != null && client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                CrestronConsole.ConsoleCommandResponse("Client is already connected. Disconnect first");
                return;
            }
            
            // Parse command-line arguments
            // You can optionally associate the client object with a certifiate and private key, which both must be in DER format, from the file system
            // For this particular example, the filenames must not contains spaces and must be located in the application directory
            string[] args = args_str.Split(' ');
            if (args.Length != 2 && args.Length != 4)
            {
                CrestronConsole.ConsoleCommandResponse("usage: connect [<cert_file> <key_file>] <hostname> <port>");
                return;
            }

            bool provideCert = false;
            string cert_fn = null; // certificate filename
            string key_fn = null; // private key filename
            int start = 0; // starting index of the hostname and port arguments in args
            if (args.Length == 4) // user provides filenames for the cert/key before the hostname and port arguments. 
            {
                provideCert = true;
                cert_fn = args[0];
                key_fn = args[1];
                start += 2;
            }

            string server_hostname = args[start];
            int port = 0;

            try
            {
                port = int.Parse(args[start+1]);
            }
            catch
            {
                CrestronConsole.ConsoleCommandResponse("Error: port number passed in is not numeric");
                return;
            }

            if (port > 65535 || port < 0)
            {
                CrestronConsole.ConsoleCommandResponse("Port number is out of range\r\n");
                return;
            }
  
            int bufsize = 100; // This is simply a hard-coded buffer size for this example

            if (client == null)
            {
                PrintAndLog("Instantiating a new client object...");
                try
                {
                    client = new SecureTCPClient(server_hostname, port, bufsize);
                    client.SocketStatusChange += new SecureTCPClientSocketStatusChangeEventHandler(clientSocketStatusChange);
                }
                catch (Exception e)
                {
                    PrintAndLog("Error encountered while instantiating the client object: " + e.Message);
                    return;
                }
            }
            // client object already exists; just update the destination hostname/port. 
            // This allows to user to simply run connect again if the connection fails, possibly trying a new host/port
            else 
            {
                client.AddressClientConnectedTo = server_hostname;
                client.PortNumber = port;
            }

            if (provideCert)
            {
                X509Certificate cert;
                byte[] key;

                // Populate cert and key
                loadCertAndKey(cert_fn, key_fn, out cert, out key);

                // Set the client's certificate and private key

                /*
                 * The X509Certificate passed to SetClientCertificate should have the following attributes in these extension
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
                // Only call SetClientCertificate and SetClientPrivateKey if loadCertAndKey succeeded in populating cert and key.
                // Otherwise, the client will be associated with a default key and certificate determined by the control system's SSL settings
                if (cert != null && key != null)
                {
                    PrintAndLog("Associating user-specified certificate and key with client...");
                    client.SetClientCertificate(cert);

                    // The user-provided private key set here must correspond to the public key embedded in the client's certificate
                    client.SetClientPrivateKey(key);
                }
                else
                {
                    PrintAndLog("Associating default certificate and key with client...");
                }
            }

            SocketErrorCodes err;

            ErrorLog.Notice("Trying to connect with server...");
            try
            {
                // clientConnectCallback gets invoked once client either  
                // connects successfully or encounters an error
                err = client.ConnectToServerAsync(clientConnectCallback);
                PrintAndLog("ConnectToServerAsync returned: " + err);
            }
            catch (Exception e)
            {
                PrintAndLog("Error connecting to server: " + e.Message);
            }
        }

        // Asynchronously send an ASCII string of text to the server. clientSendCallback will fire 
        // when the message is sent, or if an error occurs
        public void SendMessage(string message)
        {
            if (client == null || client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                CrestronConsole.PrintLine("You must connect the client to a server before sending data! See the connect user command.");
                return;
            }
            byte[] message_buf;
            try
            {
                // You can use whatever encoding you require. This example uses the ASCII encoding
                message_buf = ASCIIEncoding.ASCII.GetBytes(message);
                SocketErrorCodes err = client.SendDataAsync(message_buf, message.Length, clientSendCallback);
                PrintAndLog("SendDataAsync returned: " + err);
            }
            catch (Exception e)
            {
                PrintAndLog("Error sending message: " + e.Message);
            }
        }

        public void ShowStatus(string args)
        {
            if (client != null)
            {
                CrestronConsole.ConsoleCommandResponse("The client socket's current status is: "
                    + client.ClientStatus + ". It is configured to connect to "
                    + client.AddressClientConnectedTo + " port " + client.PortNumber);
            }
            else
            {
                CrestronConsole.ConsoleCommandResponse("No client object currently exists. Create one with connect");
            }
        }

        // This will end the client's TCP connection with the server by sending a FIN message, or
        // notify you that the client socket is already disconnected
        public void Disconnect(string args)
        {
            try
            {
                if (client != null)
                {
                    SocketErrorCodes err = client.DisconnectFromServer();
                    PrintAndLog("Disconnect with error code: " + err);
                    client.Dispose();
                    client = null;
                }
                else
                {
                    CrestronConsole.ConsoleCommandResponse("Client is already disconnected.");
                }
            }
            catch (Exception e)
            {
                PrintAndLog("Error in Disconnect: " + e.Message);
            }
        }

        public void HandleLinkLoss()
        {
            client.HandleLinkLoss();
            CrestronConsole.PrintLine("HandleLinkLoss: Client status is now " + client.ClientStatus);
        }

        public void HandleLinkUp()
        {
            client.HandleLinkUp();
            CrestronConsole.PrintLine("HandleLinkUp: Client status is now " + client.ClientStatus);
        }

        public void clientReceiveCallback(SecureTCPClient client, int bytes_recvd)
        {
            if (bytes_recvd <= 0) // 0 or negative byte count indicates the connection has been closed
            {
                PrintAndLog("clientReceiveCallback: Could not receive message- connection closed");
            }
            else
            {
                try
                {
                    CrestronConsole.PrintLine("Received " + bytes_recvd + " bytes from " + client.AddressClientConnectedTo + " port " + client.PortNumber);
                    string received = ASCIIEncoding.ASCII.GetString(client.IncomingDataBuffer, 0, client.IncomingDataBuffer.Length);
                    CrestronConsole.PrintLine("Server says: " + received);
                }
                catch (Exception e)
                {
                    PrintAndLog("Exception in clientReceiveCallback: " + e.Message);
                }
                // Wait for another message
                client.ReceiveDataAsync(clientReceiveCallback);
            }
        }

        public void clientSendCallback(SecureTCPClient client, int bytesSent)
        {
            if (bytesSent <= 0) // 0 or negative byte count indicates the connection has been closed
            {
                PrintAndLog("clientSendCallback: Could not send message- connection closed");
            }
            else
            {
                CrestronConsole.PrintLine("clientSendCallback: Sent " + bytesSent + " bytes to " + client.AddressClientConnectedTo + " port " + client.PortNumber);
            }
        }

        public void clientConnectCallback(SecureTCPClient client)
        {
            CrestronConsole.PrintLine("clientConnectCallback: ClientStatus is: " + client.ClientStatus);
            if (client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                CrestronConsole.PrintLine("clientConnectCallback: Connected to " + client.AddressClientConnectedTo + " on port " + client.PortNumber);
                // Once connected, begin waiting for packets from the server.
                // This call to ReceiveDataAsync is necessary to receive the FIN packet from the server in the event
                // that the TLS handshake fails and the connection cannot be made. If you do not call ReceiveDataAsync here,
                // the client will remain "connected" from its perspective, though no connection has been made.
                client.ReceiveDataAsync(clientReceiveCallback);
            }
            else
            {
                PrintAndLog("clientConnectCallback: No connection could be made with the server.");
            }
        }

        public void clientSocketStatusChange(SecureTCPClient client, SocketStatus clientSocketStatus)
        {
            PrintAndLog("Client Socket Status Change - Socket State is now " + clientSocketStatus);
        }

        /// <summary>
        /// Helper method to load a certificate and key from the Application directory 
        /// (as determined by Directory.GetApplicationDirectory()) for the purpose of this example program
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
                using (FileStream fs = File.OpenRead(Directory.GetApplicationDirectory() + "\\" + key_fn))
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

        public void Dispose()
        {
            client.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.ComponentModel;
using System.Threading;

namespace iad_project
{
    /// <summary>
    /// This class control to detect machines when application is connected in local network
    /// refer to http://www.nullskull.com/a/1551/clientserver-autodiscovery-in-c-and-udp-sockets.aspx
    /// </summary>
    class AutoDetect
    {
        //=====================================[ CONSTANTS/ READONLY ]=====================================//
               
        const int AUTO_DETECT_PORT = 18500;    // Fixed Port for auto detecting
        const int SERVER_SEARCHING_TIMEOUT = 10000;    // Timeout to search server
        const int SERVER_MAX_LITSENNING_TIMEOUT = 20000;    // Timeout to litsen for new connection
        readonly byte[] BROADCAST_INDENTIFIER = { 0x7, 0x7, 0x7, 0x7 }; // Identifier for broadcast
        readonly byte[] ACK_BROADCAST_INDENTIFIER = { 0x1, 0x1, 0x1, 0x1 }; // Identifier as ack for broadcast

        //=====================================[ MEMBERS ]=====================================//

        /// <summary>
        /// refer to https://msdn.microsoft.com/en-us/library/system.componentmodel.backgroundworker(v=vs.110).aspx
        /// </summary>
        BackgroundWorker worker;
        String myIPaddress;
        PackageApp pa;

        //=====================================[ METHODS ]=====================================//

        /// <summary>
        /// Allocate resources
        /// </summary>
        /// <param name="worker"></param>
        public AutoDetect(ref BackgroundWorker worker, PackageApp pa)
        {
            this.worker = worker;
            //this.myMode = Mode.None;
            this.myIPaddress = Dns.GetHostAddresses(Dns.GetHostName())[0].ToString();
            this.pa = pa;
        }

        /// <summary>
        /// After broadcast to local network, if there are no reply, it turns to temporary server.
        /// If there are reply from temporary server, turn to temporary client.
        /// Temporary client returns the list (clientList) which has no element.
        /// Temporary server returns the list (clientList) which has client lists including server itself.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Start(object sender, DoWorkEventArgs e)
        {
            IPEndPoint localEP = new IPEndPoint(IPAddress.Any, AutoDetect.AUTO_DETECT_PORT);
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            UdpClient udpClient = new UdpClient(localEP);
            udpClient.Client.ReceiveTimeout = AutoDetect.SERVER_MAX_LITSENNING_TIMEOUT;

            worker.ReportProgress(1, "Start auto-detecting.");

            try
            {
                // Broadcast to find server.(serveral times or for a while)
                if (this.BroadcastDetect())  // Client
                {
                    //this.myMode = Mode.Client;
                    worker.ReportProgress(2, "(Temp-Client) Turn to temporary client.");
                }
                else    // Server
                {
                    //this.myMode = Mode.Server;
                    pa.ClientList.Add(new IPEndPoint(Dns.GetHostAddresses(Dns.GetHostName())[0], EvalServer.EVAL_PORT));
                    worker.ReportProgress(2, "(Temp-Server) Turn to temporary server.");

                    while (true)
                    {
                        byte[] receivedBytes = new byte[1024];

                        // Only get broadcast message from the other ip address
                        do
                        {
                            receivedBytes = udpClient.Receive(ref remoteEP);
                        } while (remoteEP.Address.ToString() == myIPaddress);

                        if (receivedBytes.SequenceEqual(BROADCAST_INDENTIFIER))
                        {            
                            udpClient.Send(this.ACK_BROADCAST_INDENTIFIER, this.ACK_BROADCAST_INDENTIFIER.Length, remoteEP);
                            remoteEP.Port = EvalServer.EVAL_PORT;
                            pa.ClientList.Add(remoteEP);
                            worker.ReportProgress(3, "(Temp-Server) Detect remote computer and Send ack.");
                        }
                    }// End while   
                }// End if-else 

                worker.ReportProgress(4, "(Temp-Server/Client) Quit Auto-Detecting.");
            }
            catch(Exception ex)
            {
                worker.ReportProgress(5, "[SYSTEM] " + ex.Message);
            }
           
            udpClient.Close();
        }

        /// <summary>
        /// Handover client list(list) after finishing auto detecting
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void AutoDetectingCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            pa.IsAutoDetect_Done = true;
        }

        /// <summary>
        /// Broadcast to local area, and check if there are replies
        /// </summary>
        /// <returns></returns>
        private bool BroadcastDetect()
        {
            bool ret = false;
            UdpClient udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;

            // Set the timeout for receiving from the remote host
            udpClient.Client.ReceiveTimeout = AutoDetect.SERVER_SEARCHING_TIMEOUT;

            // IPEndPoint: Represents a network endpoint as an IP address and a port number.
            IPEndPoint ipBroadcast = new IPEndPoint(IPAddress.Broadcast, AutoDetect.AUTO_DETECT_PORT);

            try
            {
                udpClient.Send(this.BROADCAST_INDENTIFIER, this.BROADCAST_INDENTIFIER.Length, ipBroadcast);
                worker.ReportProgress(2, "Sent broadcast.");
                byte[] receivedBytes = udpClient.Receive(ref ipBroadcast);
                //string returnData = Encoding.ASCII.GetString(receivedBytes, 0, receivedBytes.Length);
                worker.ReportProgress(2, "Receive something from remote machines");
                if (receivedBytes.SequenceEqual(this.ACK_BROADCAST_INDENTIFIER))
                {
                    ret = true;
                    worker.ReportProgress(2, "Got ack.");
                }
                    
            }
            catch (SocketException se)
            {
                worker.ReportProgress(5, "[SYSTEM] " + se.Message);
            }

            udpClient.Close();

            return ret;
        }


    }
}

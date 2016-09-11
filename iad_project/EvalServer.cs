using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.ComponentModel;
using System.Threading;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Windows.Forms;

namespace iad_project
{
    class EvalServer
    {
        //=====================================[ CONSTANTS/ READONLY ]=====================================//    
         
        public const int EVAL_PORT = 250;    // Fixed Port for evaluating server (tcp)
        readonly string PREP_HEADER_ID = "PREP_HEADER_ID"; // Identifier for preparing connection with server
        readonly string START_EVAL_ID = "START_EVAL_ID"; // Identifier to start evaluation
        readonly string DONE_EVAL_ID = "DONE_EVAL_ID";  // Identifier to finish evaluation
        readonly string SELECT_SERVER_ID = "SELECT_SERVER_ID";  // Selected server
        readonly int MAX_MACHINES = 10;
        readonly int PACKET_SIZE = 1024;
        readonly int MAX_PACKETS_EVAL = 1000;

        //=====================================[ MEMBERS ]=====================================//

        PackageApp pa;
        Socket mySock;
        Socket serverSock;
        List<Socket> connectedSockList;
        List<string> connectListForClient;
        int numRequiredConnForClient;
        int numWaitingConn;
        TimeSpan totalTimeEval;
        Socket serverCandidateSock;

        //=====================================[ GETTER/ SETTER ]=====================================//
        public List<Socket> ConnectedSockList { get { return connectedSockList; } }

        //=====================================[ METHODS ]=====================================//

        /// <summary>
        /// Setting for class
        /// </summary>
        /// <param name="pa"></param>
        public EvalServer(PackageApp pa)        
        {
            //this.worker = worker;
            this.pa = pa;
            connectedSockList = new List<Socket>();
            connectListForClient = new List<string>();
            numRequiredConnForClient = 0;
            numWaitingConn = 0;
            serverCandidateSock = null;
        }

        /// <summary>
        /// Free resource
        /// </summary>
        ~EvalServer()
        {
            FreeResource();
        }

        /// <summary>
        /// Start the main work by mode (Server or Client)
        /// </summary>
        public void Start()        
        {
            if (pa.MyMode == PackageApp.Mode.Client)
                ClientService();
            else if (pa.MyMode == PackageApp.Mode.Server)
                ServerService();

            SelectServer();

            Thread.Sleep(500);
            frmMain._frmMain.UpdatingTextBox("(Temp-Client/Server) Finish to evaluate server process.");            
        }   
        
        /// <summary>
        /// Work as client. 
        /// Get the connection(TCP) with temp server and get the order from temp server
        /// Connect with the other clients and evaluate for performance to be final server.
        /// Report the result to temp server.
        /// </summary>
        void ClientService()
        {
            Thread.Sleep(500);
            frmMain._frmMain.UpdatingTextBox("(Temp-Client) Start temporary client for evaluating server.");            

            // Make socket for client and bind
            IPHostEntry myIpHE = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress myIpAddr = myIpHE.AddressList[0];
            IPEndPoint myEp = new IPEndPoint(myIpAddr, EVAL_PORT);
            mySock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                // Accept the temp server's connection
                mySock.Bind(myEp);
                mySock.Listen(MAX_MACHINES);

                Thread.Sleep(500);
                frmMain._frmMain.UpdatingTextBox("(Temp-Client) Listen for tcp connection.");

                serverSock = this.mySock.Accept();
                connectedSockList.Add(serverSock);

                frmMain._frmMain.UpdatingTextBox("(Temp-Client) Accept the request with temp server.");                

                // Waiting for server's commend to get the list which this machine should connect to evaluate
                byte[] rcvMsg = new byte[PACKET_SIZE];
                int numBytes = serverSock.Receive(rcvMsg);

                if(numBytes > 0)
                    DecodePreEvalProtocol(rcvMsg);

                // Accept the request
                for(int i = 0; i < numWaitingConn; ++i)
                {
                    mySock.Listen(MAX_MACHINES);
                    Socket sock = mySock.Accept();
                    connectedSockList.Add(sock);

                    frmMain._frmMain.UpdatingTextBox("(Temp-Client) Accept the connection with a client. ");
                }

                // connect to the other clients
                for (int i = 0; i < connectListForClient.Count; ++i)
                {                 
                    Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    IPEndPoint tmpIPep = new IPEndPoint(IPAddress.Parse(connectListForClient[i]), EVAL_PORT);

                    sock.Connect(tmpIPep);
                    connectedSockList.Add(sock);

                    frmMain._frmMain.UpdatingTextBox("(Temp-Client) Connect with a client. " + connectListForClient[i]);                   
                }             


                frmMain._frmMain.UpdatingTextBox("(Temp-Client) Total number of connected clients: " + connectedSockList.Count);

                // Waiting for the order to start evaluation from temp server
                while (true)
                {
                    byte[] msg = new byte[PACKET_SIZE];
                    numBytes = serverSock.Receive(msg);

                    if (numBytes > 0)               
                        if (Regex.IsMatch(Encoding.ASCII.GetString(msg), "^" + START_EVAL_ID + "+"))
                            break;
                }

                frmMain._frmMain.UpdatingTextBox("[SYSTEM-EVAL] Start evaluation.");

                StartEvaluation();

                frmMain._frmMain.UpdatingTextBox("[SYSTEM-EVAL] Done evaluation.");


                ReportEvalResult();
                frmMain._frmMain.UpdatingTextBox("================= EVALUATION (RTT) =================");
                frmMain._frmMain.UpdatingTextBox("My RTT: " + GetRTT().ToString() + " bpms");
                frmMain._frmMain.UpdatingTextBox("=================== END OF LIST ====================");
            }
            catch (Exception e)
            {
                frmMain._frmMain.UpdatingTextBox("[SYSTEM-EVAL] " + e.Message);
            }
        }

        /// <summary>
        /// Do the server works.
        /// Send the list to which they have to connect to the other machines.
        /// Start the evaluation for selecting server.
        /// </summary>
        void ServerService()
        {
            Thread.Sleep(500);
            frmMain._frmMain.UpdatingTextBox("(Temp-Server) Start temporary server for evaluating server.");

            try
            {
                // Connect with all clients in TCP/IP
                for (int i = 0; i < pa.ClientList.Count; ++i)
                {
                    if (pa.ClientList[i].Address.ToString() != Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString())
                    {
                        Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        sock.Connect(pa.ClientList[i]);
                        connectedSockList.Add(sock);

                        frmMain._frmMain.UpdatingTextBox("(Temp-Server) Established with " + pa.ClientList[i].Address.ToString());                    
                    }
                }
            
                // Send to all clients to give the list which clients have to connect with the other clients
                for (int i = 0; i < connectedSockList.Count; ++i)
                {
                    if (i < connectedSockList.Count)
                    {
                        byte[] msg = EncodePreEvalProtocol(i);
                        connectedSockList[i].Send(msg);

                        frmMain._frmMain.UpdatingTextBox("(Temp-Server) Send the list to " + ((IPEndPoint)connectedSockList[i].RemoteEndPoint).Address.ToString());                        
                    }
                }

                // Send the order to all clients for starting evaluation
                Thread.Sleep(3000);            
                for (int i = 0; i < connectedSockList.Count; ++i)
                {
                    byte[] msg = Encoding.ASCII.GetBytes(START_EVAL_ID);
                    connectedSockList[i].Send(msg);
                    frmMain._frmMain.UpdatingTextBox("[SYSTEM-EVAL] Send message to start evaluation to a client.");
                }

                frmMain._frmMain.UpdatingTextBox("[SYSTEM-EVAL] Start evaluation.");                

                StartEvaluation();

                frmMain._frmMain.UpdatingTextBox("[SYSTEM-EVAL] Done evaluation.");

                // collect the evaluation results and display
                Thread.Sleep(5000);
                frmMain._frmMain.UpdatingTextBox("================= EVALUATION (RTT) =================");
                frmMain._frmMain.UpdatingTextBox("My RTT: " + GetRTT().ToString() + " bpms");
                ShowClientEval();
                frmMain._frmMain.UpdatingTextBox("=================== END OF LIST ====================");
            }
            catch(Exception ex)
            {
                frmMain._frmMain.UpdatingTextBox("[SYSTEM-EVAL]" + ex.Message);                
            }                       
        }

        /// <summary>
        /// Start threads for evaluation
        /// </summary>
        void StartEvaluation()
        {
            Thread.Sleep(500);       

            Thread[] ta = new Thread[connectedSockList.Count];
            try
            {
                for (int i = 0; i < connectedSockList.Count; ++i)
                {
                    ta[i] = new Thread(new ParameterizedThreadStart(ThreadEvaluation));
                    ta[i].Start(i);                     
                }

                for (int i = 0; i < connectedSockList.Count; ++i)
                    ta[i].Join();
            }
            catch(Exception ex)
            {
                frmMain._frmMain.UpdatingTextBox("[SYSTEM-EVAL] " + ex.Message);
            }
        }

        /// <summary>
        /// Get the evaluation result from the remote clients
        /// </summary>
        void ShowClientEval()
        {
            double bestPerformance = GetRTT();
            serverCandidateSock = null;

            for(int i = 0; i < connectedSockList.Count; ++i )
            {
                while(true)
                {
                    byte[] buf = new byte[PACKET_SIZE];
                    int n = connectedSockList[i].Receive(buf);

                    if(n > 0)
                    {
                        string msg = Encoding.ASCII.GetString(buf);
                        string[] rttInfo = null;

                        if (Regex.IsMatch(msg, "^" + DONE_EVAL_ID + @"|"))
                        {
                            rttInfo = msg.Split('|');

                            double clientRTT = 0.0f;
                            if (rttInfo != null && rttInfo.Length > 2)
                                double.TryParse(rttInfo[1], out clientRTT);

                            // select best machine
                            if (clientRTT > bestPerformance)
                            {
                                bestPerformance = clientRTT;
                                serverCandidateSock = connectedSockList[i];
                            }                                

                            frmMain._frmMain.UpdatingTextBox(IPAddress.Parse(((IPEndPoint)connectedSockList[i].RemoteEndPoint).Address.ToString()) + 
                                ": " + clientRTT.ToString() + " bpms");
                            break;
                        }                            
                    }// End of if(n>0)
                }// End of while
            }// End of for       
        }// End of void ShowClientEval()

        /// <summary>
        /// Send the packets each other and measure the time
        /// </summary>
        /// <param name="sock"></param>
        void ThreadEvaluation(object index)
        {
            int num = (int)index;

            try
            {
                Stopwatch sw = new Stopwatch();

                for (int i = 0; i < MAX_PACKETS_EVAL; ++i)
                {
                    byte[] msg = new byte[PACKET_SIZE];
                    connectedSockList[num].Send(msg);
                    connectedSockList[num].Receive(msg);
                  
                    if (i == 0)
                        sw.Start();
                }

                sw.Stop();
                totalTimeEval = totalTimeEval.Add(sw.Elapsed);
            }
            catch(Exception ex)
            {
                throw ex;
            }
            
        }

        /// <summary>
        /// Calculate round-trip-time
        /// </summary>
        /// <returns></returns>
        double GetRTT()
        {
            return (double)(PACKET_SIZE * MAX_PACKETS_EVAL) / totalTimeEval.Milliseconds / 
                connectedSockList.Count;
        }

        /// <summary>
        /// Send the client itself's evaluation to server
        /// </summary>
        void ReportEvalResult()
        {
            if (pa.MyMode == PackageApp.Mode.Server)
                return;

            string msg = DONE_EVAL_ID + @"|" + GetRTT() + @"|";
            byte[] packet = Encoding.ASCII.GetBytes(msg);

            if(serverSock != null)
                serverSock.Send(packet);
        }

        /// <summary>
        /// Encoding to prepare evaluation. Send the information for preperation. 
        /// PACKET FORMAT: "PREP_HEADER_ID|(NUMBER OF WAITING FOR CONNECTION)|(NUMBUER OF MACHINES SHOULD CONNECT )|111.111.111.111|111.111.111.222"
        /// EX: "LIST_ID|3|2|111.111.111.111|111.111.111.222"
        /// EX: "LIST_ID|2|0"
        /// </summary>
        /// <param name="numWait">number of waiting connection</param>
        /// <returns></returns>
        byte[] EncodePreEvalProtocol(int numWait)
        {
            string sendStr = PREP_HEADER_ID + "|" + numWait; // NUMBER OF WAITING FOR CONNECTION
            sendStr += "|" + (connectedSockList.Count - 1 - numWait); // NUMBUER OF MACHINES SHOULD CONNECT

            int j = numWait + 1;
            for (; j < connectedSockList.Count; ++j)
                sendStr += "|" + ((IPEndPoint)connectedSockList[j].RemoteEndPoint).Address.ToString();        

            sendStr += "|";
            byte[] msg = Encoding.ASCII.GetBytes(sendStr);

            return msg;
        }

        /// <summary>
        /// Server: Send the selected server address to all clients
        /// Client: Get the the selected server address from server
        /// Do the final work
        /// </summary>
        void SelectServer()
        {
            string selectedServerIP = "";

            if (pa.MyMode == PackageApp.Mode.Client)
            {
                while(true)
                {
                    byte[] packet = new byte[PACKET_SIZE];
                    int numBytes = serverSock.Receive(packet);

                    if(numBytes > 0)
                    {
                        string msg = Encoding.ASCII.GetString(packet);
                        string[] info;
                        if (Regex.IsMatch(msg, "^" + SELECT_SERVER_ID + @"|"))
                        {
                            info = msg.Split('|');
                            selectedServerIP = info[1];
                            ManageSocketAfterSelectServer(selectedServerIP);
                            break;
                        }
                            
                    }
                }
            }
            else if (pa.MyMode == PackageApp.Mode.Server)
            {
                if (serverCandidateSock == null) // temp server -> final server
                    selectedServerIP = Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
                else
                    selectedServerIP = ((IPEndPoint)serverCandidateSock.RemoteEndPoint).Address.ToString();

                byte[] packet = Encoding.ASCII.GetBytes(SELECT_SERVER_ID + @"|" + selectedServerIP + @"|");

                for (int i = 0; i < connectedSockList.Count; ++i)
                    connectedSockList[i].Send(packet);

                ManageSocketAfterSelectServer(selectedServerIP);                
            }

            frmMain._frmMain.UpdatingTextBox("[SYSTEM-EVAL] Final server is " + selectedServerIP);
        }

        /// <summary>
        /// Close/ keep sockets connect itself after selecting server
        /// </summary>
        void ManageSocketAfterSelectServer(string serverIP)
        {
            // Server is me
            if (serverIP == Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString())
            {
                pa.MyMode = PackageApp.Mode.Server;
                return;
            }
            else  // this machine is not server
            {
                pa.MyMode = PackageApp.Mode.Client;

                for (int i = 0; i < connectedSockList.Count; ++i)
                {
                    if (serverIP != ((IPEndPoint)connectedSockList[i].RemoteEndPoint).Address.ToString())
                    {
                        connectedSockList[i].Disconnect(false);
                        connectedSockList[i].Close();
                        connectedSockList.Remove(connectedSockList[i--]);
                    }
                        
                }
            }                
        }

        /// <summary>
        /// Decoding to prepare evaluation. Send the information for preperation. 
        /// </summary>
        /// <param name="msg"></param>
        void DecodePreEvalProtocol(byte[] msg)
        {
            string[] preInfo = null;
            string s = Encoding.ASCII.GetString(msg);

            frmMain._frmMain.UpdatingTextBox(@"(Temp-Client) Get the message " + s);

            if (Regex.IsMatch(s, "^" + PREP_HEADER_ID + @"|"))
                preInfo = s.Split('|');

            if (preInfo != null && preInfo.Length > 2)
            {
                int.TryParse(preInfo[1], out numWaitingConn);
                int.TryParse(preInfo[2], out numRequiredConnForClient);

                if(numRequiredConnForClient > 0)
                {
                    for(int i = 3; i < preInfo.Length - 1; ++i)
                        connectListForClient.Add(preInfo[i]);                    
                }
            }
        }

        /// <summary>
        /// Free resource. close all sockets.
        /// </summary>
        public void FreeResource()
        {
            int numListeners = connectedSockList.Count;

            for (int i = 0; i < numListeners; ++i)
            {
                if (connectedSockList[i] != null)
                {
                    connectedSockList[i].Disconnect(false);
                    connectedSockList[i].Dispose();
                }
            }

            connectedSockList.Clear();
        }

    }
}

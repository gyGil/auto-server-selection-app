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

namespace iad_project
{
    /// <summary>
    /// Service for chatting
    /// </summary>
    class Chat
    {
        //=====================================[ CONSTANTS ]=====================================//
        readonly int PACKET_SIZE = 1024;

        //=====================================[ MEMBERS ]=====================================//
        List<Socket> connectedSockList; // connected socket list
        PackageApp pa;
        bool _isExit;   // indicate program is exit
        Thread[] listenersThreads;  // threads to liten from remote machines

        //=====================================[ METHODS ]=====================================//

        /// <summary>
        /// Allocate resource
        /// </summary>
        /// <param name="pa"></param>
        /// <param name="connectedSockList"></param>
        public Chat(PackageApp pa, List<Socket> connectedSockList)
        {
            this.connectedSockList = connectedSockList;
            this.pa = pa;
            _isExit = false;
        }

        /// <summary>
        /// Deallocate resource
        /// </summary>
        ~Chat()
        {
            FreeResource();
        }

        /// <summary>
        /// Start main thread
        /// </summary>
        public void StartChatProgram()
        {
            if (pa.MyMode == PackageApp.Mode.Server)
                Server();
            else if (pa.MyMode == PackageApp.Mode.Client)
                Client();
        }

        /// <summary>
        /// Service as a server
        /// </summary>
        void Server()
        {
            frmMain._frmMain.Update_Display("================== Server =====================");
            frmMain._frmMain.Update_Display("");

            // make threads for listen from clients
            try
            {
                StartListeners();
            }
            catch (Exception ex)
            {
                frmMain._frmMain.UpdatingTextBox("[SYSTEM-CHAT] " + ex.Message);
            }
        }

        /// <summary>
        /// Service as a client
        /// </summary>
        void Client()
        {
            frmMain._frmMain.UpdatingTextBox("================== Client =====================");
            frmMain._frmMain.UpdatingTextBox("");

            // make threads for listen from clients
            try
            {
                StartListeners();
            }
            catch (Exception ex)
            {
                frmMain._frmMain.UpdatingTextBox("[SYSTEM-CHAT] " + ex.Message);
            }
        }

        /// <summary>
        /// Send message
        /// </summary>
        /// <param name="msg"></param>
        public void SendMessage(string msg)
        {
            Sender(msg);
        }

        /// <summary>
        /// Send the message to remote machines
        /// </summary>
        /// <param name="msg"></param>
        void Sender(string msg)
        {
            byte[] packet = new byte[PACKET_SIZE];
            packet = Encoding.ASCII.GetBytes(msg);

            for(int i = 0; i < connectedSockList.Count; ++i)
            {
                connectedSockList[i].Send(packet);
            }            
        }

        /// <summary>
        /// Start threads for clients.
        /// It get the messages from clients and display.
        /// </summary>
        void StartListeners()
        {

            listenersThreads = new Thread[connectedSockList.Count];
            try
            {
                for (int i = 0; i < connectedSockList.Count; ++i)
                {
                    listenersThreads[i] = new Thread(new ParameterizedThreadStart(Listener));
                    listenersThreads[i].Start(i);                  
                }

                for (int i = 0; i < connectedSockList.Count; ++i)
                    listenersThreads[i].Join();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Get the messages from a client and display.
        /// </summary>
        /// <param name="index"></param>
        void Listener(object index)
        {
            int sockNum = (int)index;
            Socket sock = connectedSockList[sockNum];
            string ip = ((IPEndPoint)connectedSockList[sockNum].RemoteEndPoint).Address.ToString();
            int numBytes = 0;

            if (sock == null) return;

            while(!_isExit)
            {              
                byte[] buf = new byte[PACKET_SIZE];

                try
                {
                    numBytes = sock.Receive(buf);

                    if (numBytes > 0)
                    {
                        string msg = Encoding.ASCII.GetString(buf);
                        frmMain._frmMain.UpdatingTextBox(ip + "] " + msg);

                        if (pa.MyMode == PackageApp.Mode.Server)
                            Sender(msg);
                    }
                }
                catch(Exception ex)
                {
                    throw ex;
                }                
            }// End of while
        }

        /// <summary>
        /// Free resource
        /// </summary>
        public void FreeResource()
        {
            _isExit = true;
         
            int numListeners = connectedSockList.Count;

            try
            {
                for (int i = 0; i < numListeners; ++i)
                    if (listenersThreads[i] != null)
                        listenersThreads[i].Join();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            
        }
    }
}

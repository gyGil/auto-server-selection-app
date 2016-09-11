using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Net;
using System.Threading;
using System.Net.Sockets;

namespace iad_project
{
    /// <summary>
    /// The class control the procedure of AutoDetect, ServerSelection, chatting alogrithms
    /// </summary>
    class PackageApp
    {
        //=====================================[ CONSTANTS ]=====================================//
        public enum Mode { None, Server, Client };

        //=====================================[ MEMBERS ]=====================================//
        Mode myMode;       
        List<IPEndPoint> clientList;    // Client machines list (including server itself)
        bool isAutoDetect_Done; // Indicate auto-detect process is done or not
        Chat chat;
        Thread tChat;
        EvalServer evalServer;

        //=====================================[ GETTER/SETTER ]=====================================//
        public bool IsAutoDetect_Done
        {
            get
            {
                return isAutoDetect_Done;
            }

            set
            {
                isAutoDetect_Done = value;
            }
        }

        public List<IPEndPoint> ClientList
        {
            get
            {
                return clientList;
            }

            set
            {
                clientList = value;
            }
        }

        public Mode MyMode
        {
            get
            {
                return myMode;
            }

            set
            {
                myMode = value;
            }
        }


        //=====================================[ METHODS ]=====================================//

        /// <summary>
        /// Allocate resources
        /// </summary>
        public PackageApp()
        {
            clientList = new List<IPEndPoint>();
            this.isAutoDetect_Done = false;
            chat = null;
            tChat = null;
            this.MyMode = Mode.None;

            // start BackgroundWorkder for Auto-Detect
            BackgroundWorker autoDetectWorker = new BackgroundWorker();
            autoDetectWorker.WorkerReportsProgress = true;
            autoDetectWorker.WorkerSupportsCancellation = true;

            AutoDetect autoDetect = new AutoDetect(ref autoDetectWorker, this);
            autoDetectWorker.DoWork += new DoWorkEventHandler(autoDetect.Start);
            autoDetectWorker.ProgressChanged += new ProgressChangedEventHandler(Worker_ProgressChanged);
            autoDetectWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(autoDetect.AutoDetectingCompleted);
            autoDetectWorker.RunWorkerAsync();
        }

        /// <summary>
        /// Start the auto-detecting -> evaluate the machines -> start chatting service
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Start()        
        {
            // Waiting to finish auto-detecting
            while (!isAutoDetect_Done)
                Thread.Sleep(1000);

            // finishing auto-detecting by the status
            if(clientList.Count == 0)   // Temp-client 
            {
                frmMain._frmMain.UpdatingTextBox("(Temp-Client) Finished Auto-Detecting process. ");
                myMode = Mode.Client;
            }              
            else if(clientList.Count == 1)  // Temp-server but no clients (finish program)
            {
                frmMain._frmMain.UpdatingTextBox("(Temp-Server) Finished Auto-Detecting process. No client(s). ");
                frmMain._frmMain.UpdatingTextBox("(Temp-Server) Terminate program. ");
                return;
            }
            else    // Temp-server with client(s)
            {
                myMode = Mode.Server;
                frmMain._frmMain.UpdatingTextBox("(Temp-Server) Finished Auto-Detecting process. Detected the client(s). ");
                foreach (IPEndPoint iep in clientList)
                    frmMain._frmMain.UpdatingTextBox(iep.Address.ToString());
            }

            StartChatProgram(StartEvalServerWorker());
        }

        /// <summary>
        /// Start to evaluate server
        /// </summary>
        List<Socket> StartEvalServerWorker()
        {
            frmMain._frmMain.UpdatingTextBox("(Temp-Server/Client) Start evaluating server.");            

            evalServer = new EvalServer(this);
            Thread t = new Thread(new ThreadStart(evalServer.Start));
            t.Start();
            t.Join();

            return evalServer.ConnectedSockList;
        }

        /// <summary>
        /// Start chat program
        /// </summary>
        /// <param name="connectedSockList"></param>
        void StartChatProgram(List<Socket> connectedSockList)
        {
            frmMain._frmMain.UpdatingTextBox("(Temp-Server/Client) Start chatting service.");
            chat = new Chat(this, connectedSockList);
            tChat = new Thread(new ThreadStart(chat.StartChatProgram));
            tChat.Start();
        }

        /// <summary>
        /// Send the message
        /// </summary>
        /// <param name="msg"></param>
        public void SendMessage(string msg)
        {
            if (chat != null)
            {
                chat.SendMessage(msg);
                if (MyMode == PackageApp.Mode.Server)
                    frmMain._frmMain.UpdatingTextBox("SERVER] " + msg);
            }              
        }

        /// <summary>
        /// It display the system messages from backgoroundworker
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            frmMain._frmMain.UpdatingTextBox(e.UserState.ToString());
        }

        /// <summary>
        /// Free resource
        /// </summary>
        public void ClosingAPP()
        {
            try
            {
                if (evalServer != null)
                {
                    evalServer = null;
                    evalServer.FreeResource();
                }
                    
                if (chat != null)
                {
                    chat.FreeResource();
                }
                    
                if (tChat != null)
                    tChat.Join();
            }
            catch(Exception ex)
            {
                throw ex;
            }
           
        }
    }
}

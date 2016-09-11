
/// \file frmMain.cs
///
/// \mainpage CNTR2115 - Industrial Application Development
///
/// \section intro Program Introduction
/// - This program service for chatting. 
/// - It detect the clients automatically, and are connected each other.
/// - After that, they evaluate to be server by checking round-time-trip.
/// - If server is selected, it would service for chatting.
///
/// \section notes Special Release Notes
/// <b>Nothing</b>
///
/// \section requirements Style and Convention Requirements
///
/// \todo 
///
/// \bug <b>Nothing</b>
///
/// \section version Current version of this Class
/// <ul>
/// <li>\authors  Geun Young Gil(6944920)</li>
///	<li>          Marcus Rankin (3379187)</li>	
///	<li>          Lingchen Meng (6818678)</li>	
///	<li>          Xuan Zhang (5283460)</li>	
/// <li>\version  1.00.00</li>
/// <li>\date     APR.10.2016</li>
/// <li>\pre      Nothing
/// <li>\warning  Nothing
/// <li>\copyright    Marcus Rankin, Geun Young Gil, Lingchen Meng, Xuan Zhang 
/// <ul>
/// 
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace iad_project
{
    public partial class frmMain : Form
    {
        //=====================================[ MEMBERS ]=====================================//    
        public static frmMain _frmMain;
        Thread t;   // main thread to detect and evaluate (PackageApp class)
        PackageApp pa;

        //=====================================[ METHODS ]=====================================//
        public frmMain()
        {
            InitializeComponent();
            _frmMain = this;
            t = null;
            pa = null;
        }

        /// <summary>
        /// Start the detecting machines and evaluating
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDetect_Click(object sender, EventArgs e)
        {
            pa = new PackageApp();
            t = new Thread(new ThreadStart(pa.Start));
            t.Start();
        }

        /// <summary>
        /// Append text to display window
        /// </summary>
        /// <param name="msg"></param>
        public void Update_Display(string msg)
        {
            rtbDisplay.AppendText(Environment.NewLine + msg);
            rtbDisplay.ScrollToCaret();
        }

        /// <summary>
        /// Closing the main thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                if (pa != null)
                    pa.ClosingAPP();
                if (t != null)
                    t.Join();
            }
            catch(Exception ex)
            {
                Update_Display(ex.Message);
            }
            
        }

        /// <summary>
        /// Send the message when enter button is clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnEnter_Click(object sender, EventArgs e)
        {
            if(pa != null)
            {
                pa.SendMessage(rtbInput.Text);
                rtbInput.Text = "";
            }           
        }

        delegate void TextBoxDelegate(string message);

        /// <summary>
        /// Add the string from cross threads.
        /// </summary>
        /// <param name="msg"></param>
        public void UpdatingTextBox(string msg)
        {
            if (this.rtbDisplay.InvokeRequired)
                this.rtbDisplay.Invoke(new TextBoxDelegate(UpdatingTextBox), new object[] { msg });
            else
            {
                rtbDisplay.AppendText(Environment.NewLine + msg);
                rtbDisplay.ScrollToCaret();
            }
        }
    }
}

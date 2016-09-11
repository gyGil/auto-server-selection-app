/// \file Program.cs
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
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace iad_project
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GetIdleTimeAPI;
using System.Net.NetworkInformation;
using System.Diagnostics;
using NetworkMon;

namespace ShutdownScheduler
{
    public partial class frmMain : Form
    {
        public IniFile ini;

        //Put this stuff into an ini file:
        int MinimumCPUPercentage = 15;
        int MinimumDownloadSpeedKBPerSec = 10;
        int MinimumIdleTimeSeconds = 1200;      //System must be idle for at least this amount of time
        int ProgramTimeoutSeconds = 7200;       //give up after 2 hours of checking for idle
        string ProgramToExecute;
        string ProgramParameters;

        long StartTime;
        long LastFullyIdleTime;
        long ElapsedIdleTime;

        long LastInputTime; 
        bool IdleInterrupted = false;
        bool Interruption = false;

        string LastInterruptionReason = "";

        NetworkMon.NetworkMonitor myNetworkMonitor;
        double DownloadSpeedKBPerSec;

        PerformanceCounter cpuCounter;
        float AverageCPUUsage;

        public frmMain()
        {
            InitializeComponent();

            string inipath = Application.ExecutablePath;
            inipath = inipath.ToLower().Replace(".exe", ".ini");
            ini = new IniFile(inipath);

            try
            {
                MinimumCPUPercentage = int.Parse(ini.IniReadValue("SETTINGS", "MinimumCPUPercentage"));
                MinimumDownloadSpeedKBPerSec = int.Parse(ini.IniReadValue("SETTINGS", "MinimumDownloadSpeedKBPerSec"));
                MinimumIdleTimeSeconds = int.Parse(ini.IniReadValue("SETTINGS", "MinimumIdleTimeSeconds"));
                ProgramTimeoutSeconds = int.Parse(ini.IniReadValue("SETTINGS", "ProgramTimeoutSeconds"));
                ProgramToExecute = ini.IniReadValue("SETTINGS", "ProgramToExecute");
                ProgramParameters = ini.IniReadValue("SETTINGS", "ProgramParameters");
            }
            catch
            {
                MessageBox.Show("ini file is invalid.  Creating a default ini file...");
                //If there is an error reading the ini file, create a new one:
                ini.IniWriteValue("SETTINGS", "MinimumCPUPercentage", MinimumCPUPercentage.ToString());
                ini.IniWriteValue("SETTINGS", "MinimumDownloadSpeedKBPerSec", MinimumDownloadSpeedKBPerSec.ToString());
                ini.IniWriteValue("SETTINGS", "MinimumIdleTimeSeconds", MinimumIdleTimeSeconds.ToString());
                ini.IniWriteValue("SETTINGS", "ProgramTimeoutSeconds", ProgramTimeoutSeconds.ToString());
                ini.IniWriteValue("SETTINGS", "ProgramToExecute", "shutdown");
                ini.IniWriteValue("SETTINGS", "ProgramParameters", "/h");
            }

            myNetworkMonitor = new NetworkMonitor();
            myNetworkMonitor.StartMonitoring();
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            AverageCPUUsage = cpuCounter.NextValue();
            StartTime = Win32.GetTickCount();
            LastInputTime = Win32.GetLastInputTime();
            LastFullyIdleTime = Win32.GetTickCount();
            timer1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            //Generate a 10 second time constant average CPU usage:
            AverageCPUUsage += (cpuCounter.NextValue() - AverageCPUUsage) / 10;

            ShowIdleTimers();
            ShowNetworkUsageStats();
            ShowCPUUsageStats();
            CheckShutdown();
        }

        private void CheckShutdown()
        {
            long PresentTime = Win32.GetTickCount();

            Interruption = false;

            if (AverageCPUUsage >= MinimumCPUPercentage) 
            {
                SetInterruptionFlag("CPU percentage >= " + MinimumCPUPercentage + "%");
            }
            if (DownloadSpeedKBPerSec >= MinimumDownloadSpeedKBPerSec)
            {
                SetInterruptionFlag("Download speed >= " + MinimumDownloadSpeedKBPerSec + " KB/Sec");
            }

            if (IdleInterrupted)
            {
                SetInterruptionFlag("Keyboard or mouse input detected.");
            }

            if (Interruption)
            {
                LastFullyIdleTime = Win32.GetTickCount();
                ElapsedIdleTime = 0;
            }
            else
            {
                ElapsedIdleTime = (PresentTime - LastFullyIdleTime) / 1000;
                if (ElapsedIdleTime >= MinimumIdleTimeSeconds)
                {
                    //Shutdown the system.
                    //MessageBox.Show("System has been Idle for the desired time.  System Shutdown now.");

                    //Hibernate system:
                    LogToFile("Shutting down...");
                    Process.Start(ProgramToExecute, ProgramParameters);

                    //Successful shutdown:
                    Environment.Exit(0);
                }
            }

            IdleInterrupted = false;

            label4.Text = "System Shutdown Timeout: " + (MinimumIdleTimeSeconds - ElapsedIdleTime).ToString("0") + " Seconds";
            
            if ((PresentTime - StartTime)/1000 >= ProgramTimeoutSeconds)
            {
                LogToFile("Program timeout.  System was not idle.");
                //No shutdown:
                Environment.Exit(0);
            }
        }

        private void SetInterruptionFlag(string reason)
        {
            LastInterruptionReason = reason;
            Interruption = true;
            label5.Text = "Last Interruption: " + LastInterruptionReason;
            LogToFile(reason);
        }

        private void LogToFile(string logdata)
        {
            //Log the reason to a file:
            string logpath;

            logpath = Application.ExecutablePath;
            logpath = logpath.ToLower().Replace(".exe", ".log");
            System.IO.StreamWriter logwrite = new System.IO.StreamWriter(logpath, true);        //append the item to the log

            logwrite.WriteLine(DateTime.Now.ToString() + "\t" + logdata);
            logwrite.Close();
        }

        private void ShowCPUUsageStats()
        {
            label3.Text = "CPU usage (10 second average) = " + AverageCPUUsage.ToString("0.00") + " %";
        }

        private void ShowNetworkUsageStats()
        {
            DownloadSpeedKBPerSec = myNetworkMonitor.Adapters[0].DownloadSpeedKbps;

            label2.Text = "Download speed: " + DownloadSpeedKBPerSec.ToString("0.00") + " KB/Sec";
        }
        
        private void ShowIdleTimers() {

            long TotalTime;
            long IdleTime;

            TotalTime =  Win32.GetTickCount();

            if (LastInputTime != Win32.GetLastInputTime())
            {
                LastInputTime = Win32.GetLastInputTime();
                IdleInterrupted = true;
            }
            IdleTime = TotalTime - LastInputTime;


            //label1.Text = "Total time (ms) : " + TotalTime.ToString() + "; "
            //+ "\nLast input time (ms) : " + LastInputTime.ToString() +
            //"\nIdle time (ms) : " + IdleTime;

            label1.Text = "Idle time (Seconds) : " + (IdleTime/1000.0).ToString("0.000");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //Exit program on abort:
            Environment.Exit(0);
        }
    }
}

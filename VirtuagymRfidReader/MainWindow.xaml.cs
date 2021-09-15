using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using VirtuagymRfidReader.Properties;

namespace VirtuagymRfidReader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private RfidReader m_rfidReader;
        private WindowState m_storedWindowState = WindowState.Normal;
        private System.Windows.Forms.NotifyIcon m_notifyIcon;

        public MainWindow()
        {
            InitializeComponent();

            m_notifyIcon = new System.Windows.Forms.NotifyIcon();
            m_notifyIcon.BalloonTipText = "Click the tray icon to show.";
            m_notifyIcon.BalloonTipTitle = "Virtuagym RFID Reader";
            m_notifyIcon.Text = "Virtuagym RFID Reader";
            m_notifyIcon.Icon = new System.Drawing.Icon("Main.ico");
            m_notifyIcon.Click += new EventHandler(m_notifyIcon_Click);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            m_rfidReader = new RfidReader(this, Settings.Default.DeviceID, Settings.Default.WriteToComPort, Settings.Default.RepaitTimeInMs);
            
            if(Settings.Default.StartMinimized)
                this.WindowState = WindowState.Minimized;
        }

        void OnClose(object sender, CancelEventArgs args)
        {
            MessageBoxResult res = MessageBox.Show("Wollen sie die Anwendung beenden?", "Warnung!", MessageBoxButton.YesNo,MessageBoxImage.Warning);
            if(res == MessageBoxResult.No)
            {
                args.Cancel = true;
            }
            else
            {
                m_rfidReader._myDevice_Removed();
                m_notifyIcon.Dispose();
                m_notifyIcon = null;
            }
        }

        void OnStateChanged(object sender, EventArgs args)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                if (m_notifyIcon != null)
                    m_notifyIcon.ShowBalloonTip(2000);
            }
            else
                m_storedWindowState = WindowState;
        }
        
        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            CheckTrayIcon();
        }

        void m_notifyIcon_Click(object sender, EventArgs e)
        {
            Show();
            WindowState = m_storedWindowState;
        }
        
        void CheckTrayIcon()
        {
            ShowTrayIcon(!IsVisible);
        }

        void ShowTrayIcon(bool show)
        {
            if (m_notifyIcon != null)
                m_notifyIcon.Visible = show;
        }

        /// <summary>
        /// Write to ListView 
        /// Write File if type is 3 (Error)
        /// </summary>
        /// <param name="text"></param>
        /// <param name="type">1=Info|2=Warning|3=Error|4=Success</param>
        /// <param name="clear"></param>
        public void WriteToLog(string text, int type = 1)
        {
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {

                if (listViewLogEntries.Items.Count > 20)
                    listViewLogEntries.Items.Clear();

                string sType = "Info: ";
                System.Windows.Media.Brush textColor = System.Windows.Media.Brushes.Black;
                if (type == 2)
                {
                    sType = "Warning!: ";
                    textColor = System.Windows.Media.Brushes.Orange;
                }
                else if (type == 3)
                {
                    sType = "Error!: ";
                    textColor = System.Windows.Media.Brushes.Red;
                }
                else if(type == 4)
                {
                    textColor = System.Windows.Media.Brushes.Green;
                }

                string logText = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + " # " + sType + text;

                if(type == 3)
                {
                    string logFilePath = DateTime.Now.ToShortDateString().Replace(".","") + "_RfidReader.log";
                    if (File.Exists(logFilePath))
                        File.WriteAllText(logFilePath, logText);
                    else
                        File.AppendAllText(logFilePath, logText);
                }
                

                listViewLogEntries.Items.Add(new System.Windows.Controls.ListViewItem()
                {
                    Foreground = textColor,
                    Content = logText,
                });
            }));
        }
    }
}

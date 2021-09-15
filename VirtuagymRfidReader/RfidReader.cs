using HidLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Threading;
using VirtuagymRfidReader.Properties;

namespace VirtuagymRfidReader
{
    public class RfidReader
    {
        enum Command
        {
            ReadTag,
            Beep
        }

        private MainWindow _frmMain;

        private HidDevice _myDevice;
        private bool _deviceAttached;
        private bool _isReading = false;
        private bool _deviceRemoved = false;

        private static string deviceID = "vid_0416&pid_b029";
        private static byte[] Read_CMD = { 0x01, 0x01, 0x13, 0x34, 0x00, 0xFF, 0x00, 0x65, 0x05, 0x1E, 0x48, 0xE8, 0x01, 0x00, 0x81, 0x01, 0x18, 0x01, 0x64, 0xFE };
        private static byte[] Beep_CMD = { 0x01, 0x01, 0x0F, 0x36, 0x00, 0xFF, 0x00, 0x40, 0x50, 0x04, 0x05, 0x01, 0x01, 0x01, 0x1E, 0xFE };

        private SerialPort _serialPortWrite;
        private string _writeToPort = "COM5";
        private string _lastRfidTag = String.Empty;

        private BackgroundWorker _worker;
        private Timer _timer;
        private int _repeatTime = 1500;//1000ms = 1s

        #region Init
        public RfidReader(MainWindow frm,string deviceId, string writeToComPort, int repaitTimeInMs) : base()
        {
            _frmMain = frm;
            deviceID = deviceId;
            _writeToPort = writeToComPort;
            _repeatTime = repaitTimeInMs;

            ConnectToDevice();
            InitWorker();
            InitTimer();
        }

        private void ConnectToDevice()
        {
            try
            {
                var devList = HidDevices.Enumerate();
                foreach (var item in devList)
                {
                    if (item.DevicePath.ToLower().Contains(deviceID.ToLower()))
                    {
                        if (Settings.Default.DebugMode)
                            _frmMain.WriteToLog("RFID reader '"+item.DevicePath+"' found!");

                        _myDevice = item;
                        break;
                    }
                }

                if (_myDevice == null)
                {
                    _frmMain.WriteToLog("Error on rfid reader not found!", 3);
                    return;
                }

                _myDevice.OpenDevice();
                _myDevice.ReadReport(OnReport);
                _myDevice.Inserted += _myDevice_Inserted;
                _myDevice.Removed += _myDevice_Removed;
                _myDevice.MonitorDeviceEvents = true;
                _frmMain.WriteToLog("RFID reader connected!",4);

                _frmMain.WriteToLog("Open COM port ...");
                _serialPortWrite = new SerialPort(_writeToPort);
                _serialPortWrite.WriteTimeout = Settings.Default.ComPortTimeOutInMs;
                if (!_serialPortWrite.IsOpen)
                {
                    _serialPortWrite.Open();
                    _serialPortWrite.Close();
                }
                   
                _frmMain.WriteToLog("COM port connected!");
            }
            catch (Exception ex)
            {
                _frmMain.WriteToLog("Error on ConnectToDevice()" + ex.Message, 3);
            }

        }
        private void InitWorker()
        {
            _worker = new BackgroundWorker();
            _worker.DoWork += _worker_DoWork;
        }

        private void InitTimer()
        {
            _timer = new Timer(new TimeSpan(0, 0, 0, 0, _repeatTime).TotalMilliseconds);
            _timer.Elapsed += Timer_Elapsed;
        }
        #endregion

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!_worker.IsBusy)
                _worker.RunWorkerAsync();
        }

        private void _worker_DoWork(object sender, DoWorkEventArgs e)
        {
            if (_deviceAttached == false && _isReading == false)
                return;

            if(Settings.Default.DebugMode)
                _frmMain.WriteToLog("Check for card ...");

            _isReading = true;
            SendCmd(Command.ReadTag);
        }


        #region RFI Reader
        private bool SendCmd(Command cmd)
        {
            switch (cmd)
            {
                case Command.ReadTag:
                    return _myDevice.Write(Read_CMD);
                case Command.Beep:
                    return _myDevice.Write(Beep_CMD);
                default:
                    return _myDevice.Write(Beep_CMD);
            }


        }

        public void _myDevice_Removed()
        {
            _deviceRemoved = true;
            _deviceAttached = false;
            try
            {
                _frmMain.WriteToLog("Card reader disconnected!",2);
                _timer.Stop();
                _serialPortWrite.Close();
                _myDevice.CloseDevice();
                _isReading = false;
                _lastRfidTag = String.Empty;
            }
            catch(Exception ex)
            {
                _frmMain.WriteToLog("Error on stop rfid reader!"+ex.Message, 3);
            }
        }

        private void _myDevice_Inserted()
        {
            //Device Manual removed from usb port
            if (_deviceRemoved)
            {
                _deviceRemoved = false;
                _frmMain.WriteToLog("RFID reader connected!", 4);
            }

            if (_timer == null)
                InitTimer();

            if (_worker == null)
                InitWorker();

            _frmMain.WriteToLog("Wait for card ...");
            _myDevice.ReadReport(OnReport);
            _deviceAttached = true;

            _timer.Start();
        }

        private void OnReport(HidReport report)
        {
            if (_deviceAttached == false)
            {
                return;
            }

            Card myCard = new Card(report.Data);
            if (myCard.IsValidTag)
            {
                if(myCard.TagNumber_Hex != _lastRfidTag)
                {
                    _lastRfidTag = myCard.TagNumber_Hex;
                    _frmMain.WriteToLog("Card read with tag id '" + myCard.TagNumber_10 + "'",4);
                    WriteToComPort(myCard.TagNumber_10);
                }
                else
                {
                    if (Settings.Default.DebugMode)
                        _frmMain.WriteToLog("Card alreadey readed!");
                }
            }
            else
            {
                if(Settings.Default.DebugMode)
                    _frmMain.WriteToLog("No card found!");

                _lastRfidTag = String.Empty;
            }
                

            _isReading = false;

            // we need to start listening again for more data
            _myDevice.ReadReport(OnReport);
        }

        #endregion

        #region ComPort Functions
        private bool WriteToComPort(string dataToWrite)
        {
            try
            {
                if (!_serialPortWrite.IsOpen)
                    _serialPortWrite.Open();

                _serialPortWrite.Write(dataToWrite+ (char)13);
                _serialPortWrite.Close();
                return true;
            }
            catch (Exception ex)
            {
                _frmMain.WriteToLog("Error on WriteToComport()" + ex.Message, 3);
                return false;
            }
        }
        #endregion

    }

    public class Card
    {
        private string hexNumberString;
        private string tagDec10Value;
        private string tagDec8Value;
        private bool isValidTag;

        public string TagNumber_Hex { get { return hexNumberString; } }
        public string TagNumber_10 { get { return tagDec10Value; } }
        public string TagNumber_8 { get { return tagDec8Value; } }
        public bool IsValidTag { get { return isValidTag; } }

        public Card(byte[] cardData)
        {
            //Hex
            //5D00926570
            string[] tagHexData = BitConverter.ToString(cardData).Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries);
            byte[] tagHexNumber = new byte[5];
            Array.Copy(cardData, 5, tagHexNumber, 0, 5);
            hexNumberString = BitConverter.ToString(tagHexNumber).Replace("-", "");

            //Dec10
            //0009594224
            string[] tagHexNumber10 = new string[3];
            Array.Copy(tagHexData, 7, tagHexNumber10, 0, 3);
            string tmp = String.Join("", tagHexNumber10);
            tagDec10Value = uint.Parse(tmp, System.Globalization.NumberStyles.HexNumber).ToString("D10");

            //check if tag was readed
            isValidTag = Convert.ToInt64(tagDec10Value) <= 0 ? false : true;

            //Dec8
            //14625968
            string[] tagHexNumber8 = new string[2];
            Array.Copy(tagHexData, 6, tagHexNumber8, 0, 2);
            tmp = String.Join("", tagHexNumber8);
            tagDec8Value = uint.Parse(tmp, System.Globalization.NumberStyles.HexNumber).ToString();

            tagHexNumber8 = new string[2];
            Array.Copy(tagHexData, 8, tagHexNumber8, 0, 2);
            tmp = String.Join("", tagHexNumber8);
            tagDec8Value += uint.Parse(tmp, System.Globalization.NumberStyles.HexNumber).ToString();
        }
    }
}

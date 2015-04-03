using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;
using System.Management;
using System.Management.Instrumentation;

namespace Variometer_client
{
    /// <summary>
    /// A soros port kezelésével összefüggő feladatok ellátására.
    /// </summary>
    class SerialComm
    {
        public struct rDataS
        {
            public Byte[] readData;
            public int count;
        }

//        private Action<rDataS> dataReceivedCb = null;
        public Action<rDataS> dataReceivedCb { get; set; }
        private SerialPort serialPort1;
        private rDataS receivedData;


        /// <summary>
        /// A konstruktor beállítja a soros port paramétereit.
        /// </summary>
        public SerialComm()
        {
            serialPort1 = new SerialPort("COM1", 115200, Parity.None, 8, StopBits.One);
            serialPort1.WriteBufferSize = 16;
            serialPort1.ReadBufferSize = 4096;
            serialPort1.ReadTimeout = 1000;
            serialPort1.ReceivedBytesThreshold = 8192;
            serialPort1.ErrorReceived += new SerialErrorReceivedEventHandler(onSerialErrorReveived);
            receivedData.readData = new Byte[32776];
            receivedData.count = 0;
        }

        /// <summary>
        /// Destruktor.
        /// </summary>
        ~SerialComm()
        {
            ClosePort();
        }

/*
        public void setDataReceivedCb(ref Action<rDataS> callbackFunction)
        {
            dataReceivedCb = callbackFunction;
        }
*/
        /// <summary>
        /// A soros port hiba eseményeit kezeli.
        /// </summary>
        private void onSerialErrorReveived(object sender, SerialErrorReceivedEventArgs e)
        {
            receivedData.count = 65535;
            try
            {
                Thread thread = new Thread(() => dataReceivedCb(receivedData));
                thread.Start();
            }
            catch
            { }

        }

        /// <summary>
        /// A soros portra "Adat érkezett" eseményt kezeli.
        /// </summary>
        private void onSerialDataReveived(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp1 = (SerialPort)sender;
            int btr = sp1.BytesToRead;

            try
            {
                sp1.Read(receivedData.readData, receivedData.count, sp1.BytesToRead);
                sp1.DiscardInBuffer();
                receivedData.count += (btr % 32777);
                Thread thread = new Thread(() => dataReceivedCb(receivedData));
                thread.Start();
            }
            catch (TimeoutException)
            {
                receivedData.count = 65535;
                Thread thread = new Thread(() => dataReceivedCb(receivedData));
                thread.Start();
            }
            catch
            { }
        }

        /// <summary>
        /// Felderíti, hogy csatlakoztatva van-e variométer valamelyik USB portra.
        /// A WMI-t használva az USB VID és PID alapján megállapítja, hogy variométer-e.
        /// </summary>
        /// <returns>
        /// List<String> - azon COM portok nevét tartalmazó lista, amelyiken variométer található.
        /// </returns>
        public List<String> exploreDevice()
        {
            List<String> usbDevs = new List<String>();
            ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                "\\root\\CIMV2", "SELECT DeviceID, Name FROM Win32_PnPEntity " +
                "WHERE Service = 'usbser' AND DeviceID like '%VID_1FC9&PID_0083%'");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                String toAdd = "";
                String[] splitted = queryObj["Name"].ToString().Trim().Split('(');
                toAdd = splitted[splitted.Length - 1];
                toAdd = toAdd.Remove(toAdd.Length - 1);
                usbDevs.Add(toAdd);
            }
            return usbDevs;
        }
/*
        /// <summary>
        /// Listát készít a rendelkezésre álló soros portokról.
        /// </summary>
        /// <returns>
        /// List<String> - a létező COM portok nevét tartalmazó lista.
        /// </returns>
        public List<String> GetAvailablePorts()
        {
            List<String> availComPorts = new List<String>();
            foreach (String portName in SerialPort.GetPortNames())
            {
                availComPorts.Add(portName);
            }
            return availComPorts;
        }
*/

        /// <summary>
        /// Az adott soros port megnyitása.
        /// </summary>
        /// <param name="port">
        /// A soros port neve.
        /// </param>
        /// <returns>
        /// Boolean - true, ha a port nyitása sikeres volt, egyébként false.
        /// </returns>
        public Boolean OpenPort(String port)
        {
            if (serialPort1.IsOpen)
            {
                serialPort1.Close();
            }
            serialPort1.PortName = port;

            try
            {
                serialPort1.Open();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Adatok küldése a soros porton keresztül.
        /// </summary>
        /// <param name="toWrite">
        /// Az írandó bájtok.
        /// </param>
        /// <returns>
        /// Boolean - true, ha a küldés sikeres volt, egyébként false.
        /// </returns>
        public Boolean Write(ref Byte[] toWrite)
        {
            try
            {
                serialPort1.Write(toWrite, 0, toWrite.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// A soros port lezárása.
        /// </summary>
        public void ClosePort()
        {
            try
            {
                serialPort1.Close();
            }
            catch
            { }
        }

        /// <summary>
        /// Engedélyezi a port olvasását úgy, hogy hozzáad egy event handlert a port objektumhoz.
        /// </summary>
        public void StartRead()
        {
            receivedData.count = 0;
            serialPort1.DataReceived += new SerialDataReceivedEventHandler(onSerialDataReveived);
        }

        /// <summary>
        /// Leállítja a port olvasását úgy, hogy törli az event handlert a port objektumból.
        /// </summary>
        public void StopRead()
        {
            serialPort1.DataReceived -= new SerialDataReceivedEventHandler(onSerialDataReveived);
        }
    }
}

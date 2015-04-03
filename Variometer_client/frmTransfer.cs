using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Variometer_client
{
    /// <summary>
    /// Az adatátvitellel összefüggő feladatok ellátására.
    /// </summary>
    public partial class frmTransfer : Form
    {
        [StructLayout(LayoutKind.Explicit, Size = 8, Pack = 1)]
        public struct log_rec_t
        {
            [MarshalAs(UnmanagedType.U1)]
            [FieldOffset(0)]
            public byte year;

            [MarshalAs(UnmanagedType.U1)]
            [FieldOffset(1)]
            public byte month;	//The MSB is the century bit; 0 - 1900, 1 - 2000

            [MarshalAs(UnmanagedType.U1)]
            [FieldOffset(2)]
            public byte day;

            [MarshalAs(UnmanagedType.U1)]
            [FieldOffset(3)]
            public byte hour;

            [MarshalAs(UnmanagedType.U1)]
            [FieldOffset(4)]
            public byte minute;

            [MarshalAs(UnmanagedType.U1)]
            [FieldOffset(5)]
            public byte second;

            [MarshalAs(UnmanagedType.U2)]
            [FieldOffset(6)]
            public short altitude;
        }

        private SerialComm sc1 = new SerialComm();
        private Timer readTimer;
        private Timer progressTimer;
        private String comPort = "";
        private List<log_rec_t> logRecs = new List<log_rec_t>();
        private Dictionary<String, int> places = new Dictionary<String, int>();

        public delegate void AddTextDelegate(String textToAdd);
        public delegate void SetProgressBarDelegate(int value, int maxValue);
        public delegate void SetControlsEnabledDelegate(Boolean isEnabled);

        /// <summary>
        /// Konstruktor.
        /// Beállítja a timerek paramétereit.
        /// </summary>
        public frmTransfer()
        {
            InitializeComponent();

            readTimer = new Timer();
            readTimer.Interval = 30000;
            readTimer.Tick += new EventHandler(onTimerTick);

            progressTimer = new Timer();
            progressTimer.Interval = 250;
            progressTimer.Tick += new EventHandler(onPTimerTick);
        }

        /// <summary>
        /// Az ablak betöltése előtt felderíti a csatlakoztatott variométert.
        /// </summary>
        private void frmTransfer_Load(object sender, EventArgs e)
        {
            List<String> devices = new List<string>();

            fillPlaces();

            devices = sc1.exploreDevice();
            lblInfo.Text = "";

            if (devices.Count == 0)
            {
                lblInfo.Text = "Kérem csatlakoztassa az eszközt, majd kattintson az Indít gombra!";
            }
            else if (devices.Count > 1)
            {
                lblInfo.Text = devices.Count.ToString() + " eszköz van csatlakoztatva, de a program egy időben csak egyet kezel." +
                    Environment.NewLine + "Kérem, csak egy eszközt csatlakoztasson!";
            }
            else
            {
                comPort = devices[0];
                lblInfo.Text = "Az eszköz csatlakoztatva van mint " + comPort + " port." + Environment.NewLine +
                    "Az adatátvitel megkezdéséhez kattintson az Indít gombra!";
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        /// <summary>
        /// Megvizsgálja, hogy minden feltétel adott-e az adatátvitelhez, illetve,
        /// ha a repülési helyszín nem létezik még az adatbázisban, akkor létrehozza.
        /// Amennyiben minden rendben van, elindítja az adatok áttöltését.
        /// </summary>
        private void btnOK_Click(object sender, EventArgs e)
        {
            if (cbPlace.Text.Length == 0)
            {
                lblInfo.Text = "Kérem, adja meg a repülés helyét!";
                return;
            }

            if (!places.ContainsKey(cbPlace.Text))
            {
                MsSQL.insertNewPlace(cbPlace.Text);
                fillPlaces();
            }

            SetProgressBar(0, 32768);

            if (comPort.Length == 0)
            {
                int i = 0;
                List<String> devices = new List<string>();
                lblInfo.Text = "Eszköz keresése...";
                do
                {
                    devices = sc1.exploreDevice();
                    ++i;
                    Application.DoEvents();
                } while ((devices.Count == 0) && (i < 100));

                if (devices.Count == 0)
                {
                    lblInfo.Text = "Nem található csatlakoztatott eszköz!";
                    return;
                }
                else if (devices.Count > 1)
                {
                    lblInfo.Text = devices.Count.ToString() + " eszköz van csatlakoztatva, de a program egy időben csak egyet kezel." +
                        Environment.NewLine + "Kérem, csak egy eszközt csatlakoztasson!";
                    return;
                }
                else
                {
                    comPort = devices[0];
                }
            }
            lblInfo.Text = "Az eszköz csatlakoztatva van mint " + comPort + " port." + Environment.NewLine +
                "Az adatátvitel megkezdődött, ami kb. 2 percet vesz igénybe.";

            Byte[] toWrite = new Byte[4] { 2, (byte)'R', (byte)'*', 3 };
            sc1.dataReceivedCb = serialCommCb;
            sc1.OpenPort(comPort);

            if (sc1.Write(ref toWrite) == true)
            {
                SetControlsEnabled(false);
                sc1.StartRead();
                readTimer.Start();
                progressTimer.Start();
            }
            else
            {
                sc1.StopRead();
                sc1.ClosePort();
                lblInfo.Text = "Kommunikációs hiba lépett fel, az áttöltés meghiúsult";
            }
        }

        /// <summary>
        /// Callback függvény, mely az "Adat érkezett a soros porta" esemény hatására hívódik.
        /// </summary>
        /// <param name="data">
        /// A soros porton érkezett adat és a bytok száma.
        /// Ha a struktúra count eleme == 65535, akkor hiba történt.
        /// </param>
        private void serialCommCb(SerialComm.rDataS data)
        {
            readTimer.Stop();
            readTimer.Start();

            SetProgressBar(data.count, 32768);

            if ((data.count == 32768) || (data.count == 65535))
            {
                readTimer.Stop();
                progressTimer.Stop();
                sc1.StopRead();
                sc1.ClosePort();
                int i = 0;
                Byte[] toWriteOut = new Byte[8];

                logRecs.Clear();

                for (i = 0; i < data.count; i += 8)
                {
                    Array.Copy(data.readData, i, toWriteOut, 0, 8);
                    logRecs.Add(ByteArrayToLogRec(ref toWriteOut));
                }
                AddText("Az adatáttöltés befejeződött." + Environment.NewLine +
                        "Az adatok adatbázisba töltése folyik...");

                if (MsSQL.loadDataToDB(ref logRecs, places["Bükk"], this))
                {
                    AddText("Az adatok bekerültek az adatbázisba.");
                }
                else
                {
                    AddText("Hiba történt az adatbázisba töltés közben!");
                }
                SetControlsEnabled(true);
            }
        }

        /// <summary>
        /// A read timeout időzítő eseménykezelője.
        /// Ha adott időn belül nem érkezik adat a soros porton, hibának tekintjük.
        /// </summary>
        private void onTimerTick(Object sender, EventArgs ea)
        {
            readTimer.Stop();
            progressTimer.Stop();
            sc1.StopRead();
            sc1.ClosePort();
            lblInfo.Text = "Nem sikerült minden adatot áttölteni, kérem ismételje meg!";
            progressBar1.Value = 0;
        }

        /// <summary>
        /// A progress bar időzítőjének eseménykezelője.
        /// Erre azért volt szükség, mert az "Adat érkezett" esemény nem a beállított
        /// küszöbértéknél hívódik meg, mert az operációs rendszer felülbírálja, így
        /// csak minden 4096. bájt után történik a hívás. Ezért a progress bár sokáig
        /// egy helyben állna, ami megtévesztő lehet. Ezzel az időzítővel lassan léptetem előre.
        /// </summary>
        private void onPTimerTick(Object sender, EventArgs ea)
        {
            progressBar1.Value += 64;
        }

        /// <summary>
        /// Deserializer - Bájt tömbről log_rec_t típusra alakítja a kapott adatokat.
        /// </summary>
        /// <param name="bytes">
        /// Az szétbontandó adatbájtok tömbje.
        /// </param>
        /// <returns>
        /// log_rec_t - az átalakított adat.
        /// </returns>
        private log_rec_t ByteArrayToLogRec(ref Byte[] bytes)
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            log_rec_t stuff = (log_rec_t)Marshal.PtrToStructure(
                handle.AddrOfPinnedObject(), typeof(log_rec_t));
            handle.Free();
            return stuff;
        }

        /// <summary>
        /// Delegált függvény, ami másik szálból is hívható.
        /// A formon elhelyzkedő címke szövegét módosítja.
        /// </summary>
        /// <param name="textToAdd">
        /// A kiírandó szöveg.
        /// </param>
        public void AddText(String textToAdd)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new AddTextDelegate(AddText), new object[] { textToAdd });  // invoking itself
            }
            else
            {
                lblInfo.Text = textToAdd;
            }
        }

        /// <summary>
        /// Delegált függvény, ami másik szálból is hívható.
        /// A formon elhelyzkedő progress bar értékeit módosítja.
        /// </summary>
        /// <param name="value">
        /// A progress bar értéke.
        /// </param>
        /// <param name="maxValue">
        /// A progress bar felső határértéke.
        /// </param>
        public void SetProgressBar(int value, int maxValue)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new SetProgressBarDelegate(SetProgressBar), new object[] { value, maxValue });  // invoking itself
            }
            else
            {
                progressBar1.Value = value;
                progressBar1.Maximum = maxValue;
            }
        }

        /// <summary>
        /// Delegált függvény, ami másik szálból is hívható.
        /// A formon elhelyzkedő vezérlők állapotát módosítja.
        /// </summary>
        /// <param name="isEnabled">
        /// True esetén a vezérlők engedélyezettek, false esetén tiltottak lesznek.
        /// </param>
        public void SetControlsEnabled(Boolean isEnabled)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new SetControlsEnabledDelegate(SetControlsEnabled), new object[] { isEnabled });  // invoking itself
            }
            else
            {
                btnCancel.Enabled = isEnabled;
                btnOK.Enabled = isEnabled;
                cbPlace.Enabled = isEnabled;
            }
        }

        /// <summary>
        /// A formon elhelyzkedő repülési helyszíneket tartalmazó ComboBoxot tölti fel.
        /// </summary>
        private void fillPlaces()
        {
            cbPlace.Items.Clear();
            places.Clear();
            places = MsSQL.getPlaces();

            if (places.Count > 0)
            {
                foreach (KeyValuePair<String, int> kv in places)
                {
                    cbPlace.Items.Add(kv.Key);
                }
            }
        }
    }
}

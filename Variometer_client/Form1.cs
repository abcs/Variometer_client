using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Data.SqlClient;

namespace Variometer_client
{
    /// <summary>
    /// A főablak.
    /// </summary>
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Az ablak betöltődésekor csatlakozik az adatbáziskezelőhöz,
        /// szükség esetén létrehozza az adatbázist és tábláit, valamint
        /// feltölti az ablak bal oldalán lévő TreeView-t.
        /// </summary>
        private void MainForm_Load(object sender, EventArgs e)
        {
            Form caller = (Form)this;
            if (!MsSQL.connectToDatabase(ref caller))
            {
                this.Close();
                return;
            }
            if(!MsSQL.createDBifNotExists(ref caller))
            {
                this.Close();
                return;
            }

            fillTreeView(ref twFlies);
        }

        /// <summary>
        /// Megnyitja az adatok áttöltésére szolgáló párbeszéd ablakot.
        /// </summary>
        private void tsbLoad_Click(object sender, EventArgs e)
        {
            frmTransfer formTf = new frmTransfer();
            formTf.Show(this);
        }

        /// <summary>
        /// Feltölti az ablak bal oldalán lévő TreeView-t az adatbázisból.
        /// </summary>
        /// <param name="tw">
        /// A feltöltendő TreeView objektum referenciája.
        /// </param>
        private void fillTreeView(ref TreeView tw)
        {
            SqlDataAdapter daRoot = new SqlDataAdapter();
            SqlDataAdapter daChldrn = new SqlDataAdapter();
            SqlCommand scRoot = new SqlCommand();
            SqlCommand scChldrn = new SqlCommand();
            SqlParameter param = new SqlParameter();
            DataTable dtRoot = new DataTable();
            DataTable dtChldrn = new DataTable();

            scRoot.Connection = MsSQL.conn;
            scChldrn.Connection = MsSQL.conn;

            //if (rendezés) {
            //scRoot.CommandText = "SELECT DISTINCT CAST(f.LogDate As DATE) AS LogDate, f.UploadDate, p.PlaceName " +
            //                 "FROM Flyings f LEFT JOIN Places p on f.FlyingPlace = p.PlaceID ORDER BY LogDate;";

            scRoot.CommandText = "SELECT DISTINCT CAST(LogDate As DATE) AS LogDate FROM Flyings ORDER BY LogDate;";
            
            daRoot.SelectCommand = scRoot;
            daRoot.Fill(dtRoot);

            DataColumn dc = new DataColumn();
/*
            scChldrn.CommandText = "SELECT DISTINCT f.UploadDate, p.PlaceName " +
                              "FROM Flyings f LEFT JOIN Places p on f.FlyingPlace = p.PlaceID " +
                              "WHERE CAST(f.LogDate AS DATE)= @LogDate ORDER BY UploadDate;";
*/
            scChldrn.CommandText = "SELECT DISTINCT p.PlaceName " +
                              "FROM Flyings f LEFT JOIN Places p on f.FlyingPlace = p.PlaceID " +
                              "WHERE CAST(f.LogDate AS DATE)= @LogDate;";
            daChldrn.SelectCommand = scChldrn;
            daChldrn.SelectCommand.Parameters.Add("@LogDate", SqlDbType.Date);

            foreach (DataRow dr in dtRoot.Rows)
            {

                daChldrn.SelectCommand.Parameters[0].Value = dr[0];
                dtChldrn.Clear();
                daChldrn.Fill(dtChldrn);

                TreeNode[] uplDate = new TreeNode[dtChldrn.Rows.Count];
                for(int i = 0; i < dtChldrn.Rows.Count; i++)
                {
                    Console.WriteLine(dtChldrn.Rows[i][0]);
                    uplDate[i] = new TreeNode(dtChldrn.Rows[i][0].ToString());
                }
                TreeNode rootNode = new TreeNode(dr[0].ToString().Remove(10), uplDate);
                tw.Nodes.Add(rootNode);
            }
        }

        /// <summary>
        /// A TreeView-ben történt kattintást kezeli le.
        /// Amennyiben 2. szintű elemre történt a kattintás, a diagramot megrajzolja.
        /// </summary>
        private void twFlies_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeView tw = (TreeView)sender;
            if (tw.SelectedNode.Level != 1)
            {
                return;
            }

            chartFly.Series.Clear();
            chartFly.ChartAreas[0].AxisX.ScaleView.ZoomReset();
            chartFly.ChartAreas[0].AxisY.ScaleView.ZoomReset();
            chartFly.Titles[0].Text = tw.SelectedNode.Text + " - " + tw.SelectedNode.Parent.Text;


            DataTable dt = new DataTable();
            SqlDataAdapter da = new SqlDataAdapter();
            SqlCommand sc = new SqlCommand("SELECT f.LogDate, ((CAST(f.Altitude AS float))/10.0) AS Altitude, 0.0 AS Speed " +
                                           "FROM Flyings f LEFT JOIN Places p " +
                                           "on f.FlyingPLace = p. PlaceID WHERE " +
                                           "p.PlaceName = @placeName AND " +
                                           "CAST(f.LogDate AS Date)=@logDate ORDER BY f.LogDate;");
            sc.Parameters.Add("placeName", SqlDbType.VarChar, 255).Value = tw.SelectedNode.Text;
            sc.Parameters.Add("logDate", SqlDbType.Date).Value = tw.SelectedNode.Parent.Text;
            sc.Connection = MsSQL.conn;
            da.SelectCommand = sc;

            da.Fill(dt);

            for (int i = 1; i < dt.Rows.Count; i++)
            {
                dt.Rows[i]["Speed"] = ((double)dt.Rows[i - 1]["Altitude"] - (double)dt.Rows[i]["Altitude"]) /
                                      ((DateTime)dt.Rows[i - 1]["LogDate"] - (DateTime)dt.Rows[i]["LogDate"]).TotalSeconds;
            }

            chartFly.Series.Add("Alti");
            chartFly.Series["Alti"].Color = Color.DarkMagenta; //DarkSlateBlue
            chartFly.Series["Alti"].XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Time;
            chartFly.Series["Alti"].YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            chartFly.Series["Alti"].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            chartFly.Series["Alti"].ChartArea = "ChartArea1";
            chartFly.Series["Alti"].XValueMember = "LogDate";
            chartFly.Series["Alti"].YValueMembers = "Altitude";
            chartFly.Series["Alti"].XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Time;
            chartFly.Series["Alti"].YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            chartFly.Series["Alti"].Legend = "Legend1";
            chartFly.Series["Alti"].LegendText = "Magasság";
            chartFly.DataSource = dt;
            chartFly.DataBind();

            chartFly.Series.Add("Speed");
            chartFly.Series["Speed"].Color = Color.DarkGreen; //Coral
            chartFly.Series["Speed"].ChartArea = "ChartArea1";
            chartFly.Series["Speed"].ChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType.Line;
            chartFly.Series["Speed"].XValueMember = "LogDate";
            chartFly.Series["Speed"].YAxisType = System.Windows.Forms.DataVisualization.Charting.AxisType.Secondary;
            chartFly.Series["Speed"].YValueMembers = "Speed";
            chartFly.Series["Speed"].XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Time;
            chartFly.Series["Speed"].YValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Double;
            chartFly.Series["Speed"].Legend = "Legend1";
            chartFly.Series["Speed"].LegendText = "Sebesség";

            chkAltitude.Enabled = true;
            chkSpeed.Enabled = true;
        }

        /// <summary>
        /// Kinyomtatja a diagramot.
        /// </summary>
        private void tsbPrint_Click(object sender, EventArgs e)
        {
            chartFly.Printing.Print(true);
        }

        /// <summary>
        /// XML fájlba menti, exportálja a kijelölt repülés adatait.
        /// </summary>
        private void tsbExport_Click(object sender, EventArgs e)
        {
            if (chartFly.DataSource == null)
            {
                return;
            }

            SaveFileDialog fd = new SaveFileDialog();
            //fd.Title = "Mentendő fájl";
            fd.AddExtension = true;
            fd.DefaultExt = "xml";
            fd.Filter = "XML fájlok | *.xml";
            if (fd.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
            {
                DataTable dt = (DataTable)chartFly.DataSource;
                dt.TableName = "Flyings";
                dt.WriteXml(fd.FileName);
            }
        }

        /// <summary>
        /// Kiválasztható, hogy a diagramon a magasság értékek megjelenjenek-e.
        /// </summary>
        private void chkAltitude_CheckedChanged(object sender, EventArgs e)
        {
            if (chartFly.Series.FindByName("Alti") == null)
            {
                return;
            }

            CheckBox cb = (CheckBox)sender;
            if (cb.CheckState == CheckState.Checked)
            {
                chartFly.Series["Alti"].Enabled = true;
            }
            else
            {
                chartFly.Series["Alti"].Enabled = false;
            }
        }

        /// <summary>
        /// Kiválasztható, hogy a diagramon a sebesség értékek megjelenjenek-e.
        /// </summary>
        private void chkSpeed_CheckedChanged(object sender, EventArgs e)
        {
            if (chartFly.Series.FindByName("Speed") == null)
            {
                return;
            }

            CheckBox cb = (CheckBox)sender;
            if (cb.CheckState == CheckState.Checked)
            {
                chartFly.Series["Speed"].Enabled = true;
            }
            else
            {
                chartFly.Series["Speed"].Enabled = false;
            }

        }
    }
}

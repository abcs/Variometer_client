using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;
using System.Data;
using System.Threading;

namespace Variometer_client
{
    /// <summary>
    /// MS SQL szerverrel összefüggő feladatok ellátására.
    /// </summary>
    class MsSQL
    {
        private static String connString = "Server = .\\SQLEXPRESS;" +
                                           "Integrated Security = SSPI;" +
                                           "Connection Timeout=5;";
        private static List<String> createIfNotExists = new List<string>();
        public static SqlConnection conn;

        public Action<int> newRecordAddedCb { get; set; }

        /// <summary>
        /// Kapcsolódás az adatbáziskezelőhöz, hiba esetén hibaüzenet.
        /// </summary>
        /// <param name="caller">
        /// A hívó objektum.
        /// </param>
        /// <returns>
        /// Boolean - true, ha a kapcsloódás sikeres, egyébként false.
        /// </returns>
        public static Boolean connectToDatabase(ref Form caller)
        {
            try
            {
                conn = new SqlConnection(connString);
                conn.Open();
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(caller, "Az alábbi hiba lépett fel az adatbázishoz való kapcsolódás közben:" + Environment.NewLine +
                    e.Message, "Adatbázis hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Létrehozza az adatbázist és a szükséges táblákat, ha nem léteznek.
        /// </summary>
        /// <param name="caller">
        /// A hívó objektum.
        /// </param>
        /// <returns>
        /// Boolean - true, ha a létrehozás sikeres, egyébként false.
        /// </returns>
        public static Boolean createDBifNotExists(ref Form caller)
        {
            createIfNotExists.Add("IF DB_ID('Variometer') IS NULL" + Environment.NewLine +
                                  "BEGIN" + Environment.NewLine +
	                                "create database Variometer;"  + Environment.NewLine +
                                  "END");
            createIfNotExists.Add("USE Variometer;");
            createIfNotExists.Add("IF NOT EXISTS " +
                                  "(SELECT * FROM sys.objects WHERE " +
                                  " object_id = OBJECT_ID('dbo.Places') " +
                                  " AND type in ('U'))" + Environment.NewLine +
                                  "BEGIN" + Environment.NewLine +
                                    "create table dbo.Places (" + Environment.NewLine +
                                    "PlaceID int IDENTITY," + Environment.NewLine +
                                    "PlaceName varchar(255) NOT NULL," + Environment.NewLine +
                                    "CONSTRAINT PK_Places_PlaceID " +
                                    "PRIMARY KEY CLUSTERED (PlaceID)," + Environment.NewLine +
                                    ");" + Environment.NewLine +
                                  "END");
            createIfNotExists.Add("IF NOT EXISTS " +
                                  "(SELECT * FROM sys.objects WHERE " +
                                  " object_id = OBJECT_ID('dbo.Flyings') " +
                                  " AND type in ('U'))" + Environment.NewLine +
                                  "BEGIN" + Environment.NewLine +
                                    "create table dbo.Flyings (" + Environment.NewLine +
                                    "LogDate datetime NOT NULL," + Environment.NewLine +
                                    "UploadDate datetime NOT NULL," + Environment.NewLine +
                                    "Altitude int," + Environment.NewLine +
                                    "FlyingPlace int," + Environment.NewLine +
                                    "CONSTRAINT PK_Flyings_LogDate " +
                                    "PRIMARY KEY CLUSTERED (LogDate)," + Environment.NewLine +
                                    "CONSTRAINT FK_Flyings_Places " +
                                    "FOREIGN KEY (FlyingPlace) " +
                                    "REFERENCES Places (PlaceID)" + Environment.NewLine +
                                    ");" + Environment.NewLine +
                                  "END");
            try
            {
                SqlCommand comm = new SqlCommand();
                comm.Connection = conn;
                foreach (String commandText in createIfNotExists)
                {
                    comm.CommandText = commandText;
                    comm.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show(caller, "Az alábbi hiba lépett fel az adatbázis létrehozásakor:" + Environment.NewLine +
                    e.Message, "Adatbázis hiba", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// Beírja az adatbázisba a variométerről áttöltött adatokat.
        /// </summary>
        /// <param name="logRecord">
        /// log_rec_t típusú elemeket tartalmazó lista.
        /// </param>
        /// <param name="placeID">
        /// A repülés helyének azonosítója a Places tábla alpján.
        /// </param>
        /// <param name="caller">
        /// A hívó objektum.
        /// </param>
        /// <returns>
        /// Boolean - true, ha a létrehozás sikeres, egyébként false.
        /// </returns>
        public static Boolean loadDataToDB(ref List<frmTransfer.log_rec_t> logRecord,
                                        int placeID, frmTransfer caller)
        {
            int pbVal = 0;
            SqlDataAdapter da = new SqlDataAdapter("select * from Flyings", conn);
            SqlCommandBuilder cb = new SqlCommandBuilder(da);
            DataSet ds = new DataSet("Flyings");
            DataRow dr; // = new DataRow();
            DataColumn[] prKeys = new DataColumn[1];
            DateTime logDate = new DateTime();

            da.Fill(ds);
            prKeys[0] = ds.Tables[0].Columns[0];
            ds.Tables[0].PrimaryKey = prKeys;

            DateTime uplDate = DateTime.Now;
            
            foreach(frmTransfer.log_rec_t logR in logRecord)
            {
                logDate = new DateTime(2000 + logR.year,
                                       logR.month,
                                       logR.day,
                                       logR.hour,
                                       logR.minute,
                                       logR.second, DateTimeKind.Local);

                if (!ds.Tables[0].Rows.Contains(logDate))
                {
                    dr = ds.Tables[0].NewRow();
                    dr[0] = logDate;
                    dr[1] = uplDate;
                    dr[2] = logR.altitude;
                    dr[3] = placeID;

                    ds.Tables[0].Rows.Add(dr);
                }
                pbVal++;
                caller.SetProgressBar(pbVal, 4096);
            }

            cb.GetInsertCommand(); //.GetUpdateCommand();
            try
            {
                da.Update(ds);
            }
            catch
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// A Places tábla tartalmát kéri le és adja vissza eredményül.
        /// </summary>
        /// <returns>
        /// Dictionary<String, int> - A helyek nevei és ID-jai.
        /// </returns>
        public static Dictionary<String, int> getPlaces()
        {
            Dictionary<String, int> retWith = new Dictionary<String, int>();
            SqlDataAdapter da = new SqlDataAdapter("select * from Places order by PlaceName", conn);
            DataSet ds = new DataSet("Places");

            da.Fill(ds);
            
            foreach(DataRow dr in ds.Tables[0].Rows)
            {
                retWith.Add((String)dr["PlaceName"], (int)dr["PlaceID"]);
            }

            return retWith;
        }

        /// <summary>
        /// Új repülési helyszín hozzáadása a Places táblához.
        /// </summary>
        /// <param name="place">
        /// A helyszín neve.
        /// </param>
        public static void insertNewPlace(String place)
        {
            SqlCommand comm = new SqlCommand("insert into Places (PlaceName) values('" +
                place + "')", conn);
            comm.ExecuteNonQuery();
        }
    }
}

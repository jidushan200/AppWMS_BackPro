using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using CommonClass;

namespace TAIAMESControlServer
{
    public class Tracking
    {
        public static void Insert(SqlConnection conn, string strStationCode, string strAction, string strSerialNumber)
        {
            SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "sch_tracking_ins",
            new SqlParameter("stationcode", strStationCode),
            new SqlParameter("action", strAction),
            new SqlParameter("serialnumber", strSerialNumber));
        }
    }
}

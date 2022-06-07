using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using CommonClass;

namespace TAIAMESControlServer
{
    public class VarComm
    {
        public static string GetVar(SqlConnection conn, string sectionname, string name)
        {
            DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "bas_comm_getvar",
            new SqlParameter("sectionname", sectionname), new SqlParameter("name", name));

            return ds.Tables[0].Rows[0]["value"].ToString();
        }

        public static void SetVar(SqlConnection conn, string sectionname, string name, string value)
        {
            SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "bas_comm_setvar",
            new SqlParameter("sectionname", sectionname), new SqlParameter("name", name), new SqlParameter("value", value));
        }

        public static DateTime GetLastTime(SqlConnection conn, string sectionname)
        {
            DateTime ret = new DateTime(2000, 1, 1);

            DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "bas_comm_getlasttime",
                new SqlParameter("sectionname", sectionname));
            if(ds.Tables[0].Rows.Count > 0)
            {
                ret = (DateTime)ds.Tables[0].Rows[0][0];
            }

            return ret;
        }
    }
}

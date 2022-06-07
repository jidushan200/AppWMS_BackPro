/******************************************************************************************
 *                                  作者：章向忠                                          *
 *                                  日期：2017-08-04                                       *
 ******************************************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
using System.Configuration;

namespace CommonClass
{
    public class SQLHelper
    {

        public static string connStr;
        public static int timeout = 60;

        public SQLHelper()
        {
        }

        public static void ExecuteNonQuery(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                ExecuteNonQuery(conn, null, cmdType, cmdText, cmdParms);
            }
        }

        public static void ExecuteNonQuery(SqlConnection conn, SqlTransaction trans, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            SqlCommand cmd = new SqlCommand();
            cmd.Connection = conn;
            if (trans != null)
                cmd.Transaction = trans;
            cmd.CommandText = cmdText;
            cmd.CommandType = cmdType;
            cmd.CommandTimeout = timeout;
            if (cmdParms != null)
            {
                foreach (SqlParameter parm in cmdParms)
                {
                    cmd.Parameters.Add(parm);
                }
            }
            cmd.ExecuteNonQuery();
        }

        public static object ExecuteScalar(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();
                return ExecuteScalar(conn, null, cmdType, cmdText, cmdParms);
            }
        }

        public static object ExecuteScalar(SqlConnection conn, SqlTransaction trans, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            SqlCommand cmd = new SqlCommand();
            cmd.Connection = conn;
            if (trans != null)
                cmd.Transaction = trans;
            cmd.CommandText = cmdText;
            cmd.CommandType = cmdType;
            cmd.CommandTimeout = timeout;
            if (cmdParms != null)
            {
                foreach (SqlParameter parm in cmdParms)
                {
                    cmd.Parameters.Add(parm);
                }
            }
            return cmd.ExecuteScalar();
        }

        public static DataSet QueryDataSet(CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                return QueryDataSet(conn, null, cmdType, cmdText, cmdParms);
            }
        }

        public static DataSet QueryDataSet(SqlConnection conn, SqlTransaction trans, CommandType cmdType, string cmdText, params SqlParameter[] cmdParms)
        {
            SqlCommand cmd = new SqlCommand();
            cmd.Connection = conn;
            if (trans != null)
                cmd.Transaction = trans;
            cmd.CommandText = cmdText;
            cmd.CommandType = cmdType;
            cmd.CommandTimeout = timeout;
            if (cmdParms != null)
            {
                foreach (SqlParameter parm in cmdParms)
                {
                    cmd.Parameters.Add(parm);
                }
            }
            SqlDataAdapter adapter = new SqlDataAdapter(cmd);
            DataSet ds = new DataSet();
            adapter.Fill(ds);
            return ds;
        }

        /// <summary> 
        /// 将class实例对象转换为SqlParameter列表
        /// </summary>
        /// <param name="model">类实例对象,实例的成员变量不可为null</param>
        /// <returns>SqlParameter列表</returns>
        public static List<SqlParameter> ModelToParameterList(object model)
        {
            List<SqlParameter> list = new List<SqlParameter>();
            //Type t = model.GetType();
            PropertyInfo[] modelpro = model.GetType().GetProperties();
            if (modelpro.Length > 0)                                                     //model是类，有属性
            {
                for (int i = 0; i < modelpro.Length; i++)
                {
                    SqlParameter parm = new SqlParameter();
                    parm.Direction = ParameterDirection.Input;
                    parm.ParameterName = modelpro[i].Name;
                    object o = modelpro[i].GetValue(model, null);
                    Type t = modelpro[i].PropertyType;
                    if (modelpro[i].PropertyType.IsGenericType && modelpro[i].PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        t = modelpro[i].PropertyType.GetGenericArguments()[0];
                    }
                    parm.SqlDbType = GetSqlDbType(t);
                    if (o == null)
                        parm.Value = DBNull.Value;
                    else
                        parm.Value = o;
                    list.Add(parm);
                }
            }
            if (list.Count > 0)                                                         //传入参数不是类，返回null
                return list;
            return null;
        }

        /// <summary> 
        /// 获得c#类型的对应sqlDbType类型
        /// </summary>
        /// <param name="t">c#类型</param>
        /// <returns>SqlDBType</returns>
        public static SqlDbType GetSqlDbType(Type t)
        {
            SqlDbType dbType = SqlDbType.Variant;
            switch (t.Name)
            {
                case "Int16":
                    dbType = SqlDbType.SmallInt;
                    break;
                case "Int32":
                    dbType = SqlDbType.Int;
                    break;
                case "Int64":
                    dbType = SqlDbType.BigInt;
                    break;
                case "Single":
                    dbType = SqlDbType.Real;
                    break;
                case "Decimal":
                    dbType = SqlDbType.Decimal;
                    break;
                case "Byte[]":
                    dbType = SqlDbType.VarBinary;
                    break;
                case "Boolean":
                    dbType = SqlDbType.Bit;
                    break;
                case "String":
                    dbType = SqlDbType.NVarChar;
                    break;
                case "Char[]":
                    dbType = SqlDbType.Char;
                    break;
                case "DateTime":
                    dbType = SqlDbType.DateTime;
                    break;
                case "DateTime2":
                    dbType = SqlDbType.DateTime2;
                    break;
                case "DateTimeOffset":
                    dbType = SqlDbType.DateTimeOffset;
                    break;
                case "TimeSpan":
                    dbType = SqlDbType.Time;
                    break;
                case "Guid":
                    dbType = SqlDbType.UniqueIdentifier;
                    break;
                case "Xml":
                    dbType = SqlDbType.Xml;
                    break;
                case "Object":
                    dbType = SqlDbType.Variant;
                    break;
            }
            return dbType;
        }
    }
}
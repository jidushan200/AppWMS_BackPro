using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Client;
using OpcUaHelper;
using System.Data;
using System.Data.SqlClient;
using CommonClass;

namespace TAIAMESControlServer
{
    class BeltTaskC
    {
        static log4net.ILog log = log4net.LogManager.GetLogger("BeltTaskC");
        static FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

        //线程控制变量
        public static bool bStop = false;                                                           //线程停止信号
        public static bool bRunningFlag = false;                                                    //线程运行标志

        static int iStage = 0;                                                                      //状态机

        //OPC节点
        const string opcCanExChangeData = "ns=2;IMMES.Line.MES.InteractR_CanExChangeData";          //PLC-MES，RFID可以读取
        const string opcExchangeFinish = "ns=2;IMMES.Line.MES.InteractW_ExchangeFinish";            //MES-PLC，RFID读取完成
        const string opcPalletNumber = "ns=2;IMMES.Line.MES.InteractR_DataPallet01";                //PLC-MES，产线托盘号
        const string opcMaterialSN = "ns=2;IMMES.Line.MES.InteractW_OrderIDBytes";                  //MES-PLC，原料SN写入
        const string opcCarArrivaledC = "ns=2;IMMES.Line.MES.InteractW_CarArrivaled";               //MES-PLC，当AGV到达时设置true通知PLC
        const string opcCanLeaveC = "ns=2;IMMES.Line.MES.InteractR_Canleave";                       //PLC-MES，AGV是否可以离开B1点：true：可以离开
        const string opcCanIncomeA = "ns=2;Warehouse.Storage.MESControl.InteractR_FirstCanIN";      //PLC-MES，C点是否运行进入：true：允许进入
        const string opcFinishC = "ns=2;IMMES.Line.MES.InteractW_Finish";                           //MES-PLC，AGV离开后设置true通知PLC

        static void thTaskBFunc()
        {
            string strmsg = "";
            int iTimeWait = 0;

            bool bCanExChangeData = false;
            bool bCanLeaveC = false;
            bool bCanIncomeA = false;

            string robotcode = "";                                                                  //AGV车号
            string materialsn = "";                                                                 //原料序号
            int palletnumber = 0;                                                                  //1工位托盘号
            string productsn = "";                                                                  //1工位产品序号

            OpcUaClient ua = new OpcUaClient();
            DataValue dv;

            strmsg = "BeltTaskC线程启动";
            formmain.logToView(strmsg);
            log.Info(strmsg);

            //初始化数据
            iTimeWait = 0;
            while (true)
            {
                if (bStop)                                                                          //结束线程
                    break;

                //延时等待
                if (iTimeWait > 0)
                {
                    iTimeWait--;
                    Thread.Sleep(1000);
                    continue;
                }

                using (SqlConnection conn = new SqlConnection(Properties.Settings.Default.ConnString))
                {
                    try
                    {
                        conn.Open();

                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "belt_comm_init", null);

                        strmsg = "BeltTaskC初始化";
                        formmain.logToView(strmsg);
                        log.Info(strmsg);

                        break;
                    }
                    catch (Exception ex)
                    {
                        strmsg = "Error: " + ex.Message + " 等待一会儿再试!";
                        formmain.logToView(strmsg);
                        log.Info(strmsg);
                        iTimeWait = 10;
                        continue;
                    }
                }
            }

            bRunningFlag = true;                                                                    //设置任务C线程运行标志
            while (true)
            {
                if (bStop && !WmsTaskA.bRunningFlag && iStage == 0)                                 //结束线程
                    break;

                //延时等待
                if (iTimeWait > 0)
                {
                    iTimeWait--;
                    Thread.Sleep(1000);
                    continue;
                }

                using (SqlConnection conn = new SqlConnection(Properties.Settings.Default.ConnString))
                {
                    try
                    {
                        conn.Open();

                        var t = ua.ConnectServer(Properties.Settings.Default.OpcUrl);               //连接OPCUA服务
                        Task.WaitAll(t);

                        while (true)
                        {
                            if (bStop && !WmsTaskA.bRunningFlag && iStage == 0)                     //结束线程
                                break;

                            //延时等待
                            if (iTimeWait > 0)
                            {
                                iTimeWait--;
                                Thread.Sleep(1000);
                                continue;
                            }

                            //读取OPC数据点
                            dv = ua.ReadNode(new NodeId(opcCanExChangeData));
                            string teststr = dv.Value.ToString();
                            bCanExChangeData = (dv.Value.ToString() == "True");
                            dv = ua.ReadNode(new NodeId(opcCanLeaveC));
                            bCanLeaveC = (dv.Value.ToString() == "True");
                            dv = ua.ReadNode(new NodeId(opcCanIncomeA));
                            bCanIncomeA = (dv.Value.ToString() == "True");

                            //状态机：
                            //0：等待AGV到达
                            //1：按车号读取原料序号
                            //2：C点进入追踪
                            //3：发送AGV到达信号
                            //4：获取托盘号和成品SN
                            //5：写入原料SN
                            //6：等待C点操作完成
                            //7：AGV发车
                            //8：等待AGV发车
                            //9：C点离开追踪

                            #region 等待AGV到达（0）
                            if (iStage == 0)
                            {
                                //检测AGV到达C点
                                bool bArrivedC = false;
                                if (VarComm.GetVar(conn, "AGV", "ArrivedC") != "")
                                    bArrivedC = true;


                                if (bArrivedC)
                                {
                                    //获取AGV车号
                                    robotcode = VarComm.GetVar(conn, "AGV", "RobotCodeC");
                                    VarComm.SetVar(conn, "BELT", "RobotCode", robotcode);

                                    strmsg = "AGV已到达C点";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 1;
                                }
                            }
                            #endregion

                            #region 按车号读取原料序号（1）
                            if (iStage == 1)
                            {
                                materialsn = "";
                                DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "agv_sn_get",
                                    new SqlParameter("agvnumber", robotcode));
                                materialsn = ds.Tables[0].Rows[0]["serialnumber"].ToString();
                                VarComm.SetVar(conn, "BELT", "MaterialSN", materialsn);

                                strmsg = "产品序号读取成功，车号：" + robotcode + "，原料序号：" + materialsn;
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 2;
                            }
                            #endregion

                            #region C点进入追踪（2）
                            if (iStage == 2)
                            {
                                if (materialsn != "")
                                {
                                    Tracking.Insert(conn, "BELT", "C点进入", materialsn);               //写入Tracking信息
                                }
                                iStage = 3;
                            }
                            #endregion

                            #region 发送AGV到达信号（3）
                            if (iStage == 3)
                            {
                                ua.WriteNode(opcCarArrivaledC, true);

                                strmsg = "C点操作开始，等待进行数据交换...";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 4;
                            }
                            #endregion

                            #region 获取托盘号和成品SN（4）
                            if (iStage == 4)
                            {
                                if (bCanExChangeData)
                                {
                                    //托盘号
                                    dv = ua.ReadNode(new NodeId(opcPalletNumber));
                                    palletnumber = int.Parse(dv.Value.ToString());
                                    VarComm.SetVar(conn, "BELT", "PalletNumber", palletnumber.ToString());

                                    //成品序号
                                    {
                                        DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "belt_sn_get",
                                        new SqlParameter("palletnumber", palletnumber));
                                        productsn = ds.Tables[0].Rows[0]["serialnumber"].ToString();
                                    }
                                    VarComm.SetVar(conn, "BELT", "ProductSN", productsn);

                                    iStage = 5;
                                }
                            }
                            #endregion

                            #region 写入原料SN（5）
                            if (iStage == 5)
                            {
                                //写入原料序号
                                byte[] bytearray = new byte[18];
                                for (int i = 0; i < bytearray.Length; i++)
                                    bytearray[i] = 0;
                                byte[] arr = Encoding.Default.GetBytes(materialsn);
                                for(int i = 0; i < arr.Length; i++)
                                {
                                    if(i < bytearray.Length)
                                    {
                                        bytearray[i] = arr[i];
                                    }
                                }
                                ua.WriteNode(opcMaterialSN, bytearray);
                                SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "belt_sn_set",
                                    new SqlParameter("palletnumber", palletnumber), new SqlParameter("serialnumber", materialsn));

                                ua.WriteNode(opcExchangeFinish, true);                          //数据交换完成

                                strmsg = "C点数据交换已完成";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 6;
                            }
                            #endregion

                            #region 等待操作完成（6）
                            if (iStage == 6)
                            {
                                if (bCanLeaveC)
                                {
                                    ua.WriteNode(opcFinishC, true);                                 //置C点操作完成信号

                                    strmsg = "C点操作完成信号已发送，等待A点允许进入...";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 7;
                                }
                            }
                            #endregion

                            #region AGV发车（7）
                            if (iStage == 7)
                            {
                                if (bCanIncomeA)
                                {
                                    SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "agv_sn_set",
                                        new SqlParameter("agvnumber", robotcode), new SqlParameter("serialnumber", productsn));
                                    VarComm.SetVar(conn, "AGV", "ContinueC", "1");

                                    strmsg = "AGV离开C点，车号：" + robotcode + "，成品序号：" + productsn;
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 8;
                                }
                            }
                            #endregion

                            #region 等待AGV发车（8）
                            if (iStage == 8)
                            {
                                bool bArrivedC = false;
                                if (VarComm.GetVar(conn, "AGV", "ArrivedC") != "")
                                    bArrivedC = true;


                                if (!bArrivedC)
                                {
                                    strmsg = "AGV已发车";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 9;
                                }
                            }
                            #endregion

                            #region C点离开追踪（9）
                            if (iStage == 9)
                            {
                                if (productsn != "")
                                {
                                    Tracking.Insert(conn, "BELT", "C点离开", productsn);                //写入Tracking信息

                                    iStage = 0;
                                }
                                else
                                {
                                    iStage = 0;
                                }
                            }
                            #endregion

                            Thread.Sleep(200);
                        }
                    }
                    catch (Exception ex)
                    {
                        strmsg = "Error: " + ex.Message + " 等待一会儿再试!";
                        formmain.logToView(strmsg);
                        log.Info(strmsg);
                        iTimeWait = 10;
                        continue;
                    }
                }
            }
            strmsg = "BeltTaskC线程停止";
            formmain.logToView(strmsg);
            log.Info(strmsg);
            bRunningFlag = false;                                                                   //设置任务C线程停止标志
            return;
        }

        public static void Start()
        {
            //启动任务C控制
            Task.Run(() => thTaskBFunc());
        }

        public static void Stop()
        {
            //停止任务A控制
            bStop = true;
        }
    }

    //BeltTaskC变量显示
    class BeltTaskCVarRefresh
    {
        static log4net.ILog log = log4net.LogManager.GetLogger("BeltTaskCVarRefresh");
        static FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

        public static bool bStop = false;                                                           //线程停止信号
        public static bool bRunningFlag = false;                                                    //线程运行标志

        public static void thTaskCVarRefreshFunc()
        {
            log.Info("BeltTaskC变量显示线程启动");
            bRunningFlag = true;

            int iTimeWait = 0;
            string strmsg = "";
            DateTime oldts = new DateTime(1970, 1, 1);                                              //数据库最后一次刷新时戳

            while (true)
            {
                if (bStop && !BeltTaskC.bRunningFlag)
                    break;

                //延时等待
                if (iTimeWait > 0)
                {
                    iTimeWait--;
                    Thread.Sleep(1000);
                    continue;
                }
                using (SqlConnection conn = new SqlConnection(Properties.Settings.Default.ConnString))
                {
                    try
                    {
                        conn.Open();

                        while (true)
                        {
                            if (bStop && !BeltTaskC.bRunningFlag)
                                break;

                            //延时等待
                            if (iTimeWait > 0)
                            {
                                iTimeWait--;
                                Thread.Sleep(1000);
                                continue;
                            }

                            //查询变量最新操作时戳
                            DateTime ts = VarComm.GetLastTime(conn, "BELT");
                            if (ts > oldts)
                            {
                                //读取变量列表
                                DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "bas_comm_getallvar",
                                    new SqlParameter("sectionname", "BELT"));

                                //刷新显示
                                formmain.Invoke(new EventHandler(delegate
                                {
                                    formmain.dgBelt.DataSource = ds.Tables[0];
                                }));
                                oldts = ts;
                            }
                            Thread.Sleep(200);
                        }
                    }
                    catch (Exception ex)
                    {
                        strmsg = "Error: " + ex.Message + " 等待一会儿再试!";
                        formmain.logToView(strmsg);
                        log.Error(strmsg);
                        iTimeWait = 10;
                        continue;
                    }
                }
            }
            bRunningFlag = false;
            log.Info("BeltTaskC变量显示线程结束");
        }

        public static void Start()
        {
            //启动BeltTaskC变量显示线程
            Task.Run(() => thTaskCVarRefreshFunc());

        }

        public static void Stop()
        {
            //停止BeltTaskC变量显示线程
            bStop = true;
        }
    }
}

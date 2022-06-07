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
    class WmsControl
    {
        static log4net.ILog log = log4net.LogManager.GetLogger("WmsControl");
        static FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

        //线程控制变量
        public static bool bStop = false;                                                           //线程停止信号
        public static bool bRunningFlag = false;                                                    //线程运行标志

        static int iStage = 0;                                                                      //状态机

        //MES-PLC，立库任务类型
        //1：RF自动口入库
        //2：RF人工口入库
        //3：RF自动口出库+机械手操作
        //4：RF人工口出库
        //5：U盘机械手操作
        //6：清线
        const string opcType = "ns=2;Warehouse.Storage.Stereo.InteractW_Type";
        //MES-PLC，原料抓取类型
        //1：传送RF原料
        //2：传送U盘原料
        //3：无动作
        const string opcMaterialType = "ns=2;Warehouse.Storage.Stereo.InteractW_MaterialType";
        //MES-PLC，成品抓取类型
        //1：传送RF成品
        //2：传送U盘成品
        //3：无动作
        const string opcProductType = "ns=2;Warehouse.Storage.Stereo.InteractW_ProductType";

        //const string opcRobertEnable = "ns=2;Warehouse.Storage.Stereo.IntractW_RobertEnable";               //MES-PLC，机械手使能
        const string opcSTEP = "ns=2;Warehouse.Storage.Stereo.InteractR_STEP";                              //PLC-MES，立库状态机：3：立库空闲
        const string opcCMDStart = "ns=2;Warehouse.Storage.Stereo.InteractW_CMD_LK";                        //MES-PLC，设置为true立库命令开始执行
        const string opcOUT_House = "ns=2;Warehouse.Storage.Stereo.InteractW_OUT_House";                    //MES-PLC，出库库位（1-20）
        const string opcIN_House = "ns=2;Warehouse.Storage.Stereo.InteractW_IN_House";                      //MES-PLC，入库库位（1-20）
        const string opcIsFinish = "ns=2;Warehouse.Storage.Stereo.InteractR_IsFinish";                      //PLC-MES，操作完成通知状态：true：操作完成
        const string opcReadFinish = "ns=2;Warehouse.Storage.Stereo.InteractW_ReadFinish";                  //MES-PLC，设置为true通知PLC复位操作完成通知
        const string opcRFID_Robot = "ns=2;Warehouse.Storage.Stereo.InteractR_RFID_Robot";                  //PLC-MES，自动口托盘状态：true：有托盘
        //const string opcRFID_Man = "ns=2;Warehouse.Storage.Stereo.InteractR_RFID_Man";                      //PLC-MES，人工口托盘状态：true：有托盘
        //const string opcUSCanOut = "ns=2;Warehouse.Storage.MESControl.InteractR_USCanOut";                  //PLC-MES，提示U盘可以取走
        //const string opcUSOutFinish = "ns=2;Warehouse.Storage.MESControl.InteractW_USOutExecute";           //MES-PLC，人工确认U盘被取走，滑台将自动复位

        public static void thWmsFunc()
        {
            string strmsg = "";
            int iTimeWait = 0;

            //临时变量
            int id = 0;                                                                             //wms任务记录id
            int mastertasktype = 0;                                                                 //主任务类型号
            int materialtasktype = 0;                                                               //原料任务类型号
            int producttasktype = 0;                                                                //成品任务类型号
            int dstloc = 0;                                                                         //入库库位号
            int srcloc = 0;                                                                         //出库库位号

            OpcUaClient ua = new OpcUaClient();
            DataValue dv;

            strmsg = "WmsControl线程启动";
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

                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "wms_task_init", null);

                        strmsg = "WmsControl初始化";
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

            bRunningFlag = true;                                                                    //设置WMS任务线程运行标志
            while (true)
            {
                if (bStop && !WmsTaskA.bRunningFlag && iStage == 1)                                 //结束线程
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
                            if (bStop && !WmsTaskA.bRunningFlag && iStage == 1)                     //结束线程
                                break;

                            //延时等待
                            if (iTimeWait > 0)
                            {
                                iTimeWait--;
                                Thread.Sleep(1000);
                                continue;
                            }

                            //状态机：
                            //0：检查立库状态空闲
                            //1：查找新的立库任务
                            //2：确认立库出口托盘状态
                            //3：等待托盘状态恢复正常
                            //4：任务发送到PLC
                            //5：写入执行时间
                            //6：查找任务更新
                            //7：任务更新发送到PLC
                            //8：等待立库完成信号，并更新数据
                            //9：给PLC发操作完成通知
                            //10：立库操作结束状态

                            #region 检查立库状态空闲（0）
                            if (iStage == 0)
                            {
                                dv = ua.ReadNode(new NodeId(opcSTEP));
                                if (dv.Value.ToString() == "3")
                                {
                                    strmsg = "立库控制系统处于空闲状态，查找新的立库任务...";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 1;
                                }
                            }
                            #endregion

                            #region 查找新的立库任务（1）
                            if (iStage == 1)
                            {
                                //初始化变量
                                id = 0;
                                mastertasktype = 0;
                                materialtasktype = 0;
                                producttasktype = 0;
                                dstloc = 0;
                                srcloc = 0;

                                //查找新任务
                                DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "wms_task_findnew", null);
                                if (ds.Tables.Count > 0)
                                {
                                    id = (int)ds.Tables[0].Rows[0]["id"];
                                    mastertasktype = (int)ds.Tables[0].Rows[0]["mastertasktype"];
                                    materialtasktype = (int)ds.Tables[0].Rows[0]["materialtasktype"];
                                    producttasktype = (int)ds.Tables[0].Rows[0]["producttasktype"];
                                    dstloc = (int)ds.Tables[0].Rows[0]["dstloc"];
                                    srcloc = (int)ds.Tables[0].Rows[0]["srcloc"];
                                    strmsg = "发现新任务，ID：" + id.ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 2;
                                }
                            }
                            #endregion

                            #region 确认立库出口托盘状态（2）
                            if(iStage == 2)
                            {
                                dv = ua.ReadNode(new NodeId(opcRFID_Robot));
                                if(mastertasktype == 1 && dv.Value.ToString() =="False")
                                {
                                    strmsg = "入库任务自动口没有托盘，等待手工修正...";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);
                                    iStage = 3;
                                }
                                else if(mastertasktype == 3 && dv.Value.ToString()=="True")
                                {
                                    strmsg = "出库任务自动口存在托盘，等待手工修正...";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);
                                    iStage = 3;
                                }
                                else
                                {
                                    iStage = 4;
                                }
                            }
                            #endregion

                            #region 等待托盘状态恢复正常（3）
                            if (iStage == 3)
                            {
                                dv = ua.ReadNode(new NodeId(opcRFID_Robot));
                                if(!(mastertasktype == 1 && dv.Value.ToString() == "False") && !(mastertasktype == 3 && dv.Value.ToString() == "True"))
                                {
                                    iStage = 4;
                                }
                            }
                            #endregion

                            #region 任务发送到PLC（4）
                            if (iStage == 4)
                            {
                                dv = ua.ReadNode(new NodeId(opcCMDStart));
                                if (dv.Value.ToString() == "False")
                                {
                                    strmsg = "立库启动信号已复位";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //设定立库操作参数
                                    ua.WriteNode(opcType, (UInt16)mastertasktype);
                                    ua.WriteNode(opcMaterialType, (UInt16)materialtasktype);
                                    ua.WriteNode(opcProductType, (UInt16)producttasktype);
                                    ua.WriteNode(opcIN_House, (UInt16)dstloc);
                                    ua.WriteNode(opcOUT_House, (UInt16)srcloc);

                                    strmsg = "立库操作参数发送完毕，主任务号：" + mastertasktype.ToString()
                                            + "，原料任务号：" + materialtasktype.ToString() + "，成品任务号：" + producttasktype.ToString()
                                            + "，入库库位：" + dstloc.ToString() + "，出库库位：" + srcloc;
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //启动
                                    ua.WriteNode(opcCMDStart, true);

                                    VarComm.SetVar(conn, "WMS", "TaskRunning", "1");

                                    strmsg = "立库启动信号已发送";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 5;
                                }
                                else
                                {
                                    iTimeWait = 1;                                                  //等待启动信号复位
                                }
                            }
                            #endregion

                            #region 写入执行时间（5）
                            if (iStage == 5)
                            {
                                SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "wms_task_execute", new SqlParameter("id", id));

                                strmsg = "立库任务执行时间写入，任务ID：" + id.ToString();
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 6;
                            }
                            #endregion

                            #region 查找任务更新（6）
                            if (iStage == 6)
                            {
                                if (mastertasktype==3 && producttasktype == 0)
                                {
                                    //提前出库操作中，等待更新机械手成品任务类型
                                    DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "wms_task_findupd",
                                        new SqlParameter("id", id));
                                    if (ds.Tables.Count > 0)
                                    {
                                        producttasktype = (int)ds.Tables[0].Rows[0]["producttasktype"];

                                        strmsg = "发现机械手成品任务号更新";
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);

                                        iStage = 7;
                                    }
                                }
                                else
                                {
                                    //无需查找任务更新
                                    iStage = 8;
                                }
                            }
                            #endregion

                            #region 任务更新发送到PLC（7）
                            if (iStage == 7)
                            {
                                ua.WriteNode(opcProductType, (UInt16)producttasktype);

                                strmsg = "机械手成品任务号更新完毕，成品任务号：" + producttasktype.ToString();
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 8;
                            }
                            #endregion

                            #region 等待立库完成信号，并更新数据（8）
                            if (iStage == 8)
                            {
                                iStage = 6;
                                dv = ua.ReadNode(new NodeId(opcIsFinish));
                                if (dv.Value.ToString() == "True")
                                {
                                    strmsg = "立库已操作完成，更新库位数据...";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "wms_task_finish", new SqlParameter("id", id));
                                    VarComm.SetVar(conn, "WMS", "TaskRunning", "");

                                    strmsg = "库位数据已更新完毕";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 9;
                                }
                            }
                            #endregion

                            #region 给PLC发操作完成通知（9）
                            if (iStage == 9)
                            {
                                ua.WriteNode(opcReadFinish, true);

                                strmsg = "操作完毕通知已发送";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 10;
                            }
                            #endregion

                            #region 立库操作结束状态（10）
                            if(iStage == 10)
                            {
                                VarComm.SetVar(conn, "WMS", "TaskRunning", "");

                                iStage = 0;
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

            strmsg = "WmsControl线程停止";
            formmain.logToView(strmsg);
            log.Info(strmsg);
            bRunningFlag = false;                                                                   //设置WMS任务线程停止标志
            return;
        }

        public static void Start()
        {
            //启动立库控制任务
            Task.Run(() => thWmsFunc());
        }

        public static void Stop()
        {
            //停止立库控制任务
            bStop = true;
        }

    }
}

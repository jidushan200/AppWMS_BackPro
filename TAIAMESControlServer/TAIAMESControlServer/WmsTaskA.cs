﻿using System;
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
    public class WmsTaskA
    {
        static log4net.ILog log = log4net.LogManager.GetLogger("WmsTaskA");
        static FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

        //线程控制变量
        public static bool bStop = false;                                                           //线程停止信号
        public static bool bRunningFlag = false;                                                    //线程运行标志

        static bool bProductRunning = false;                                                        //生产状态
        static bool bProductEnable = false;                                                         //生产使能标志

        static int iStage = 0;                                                                      //状态机

        //数据变量

        //OPC节点
        const string opcCanIncomeA = "ns=2;Warehouse.Storage.MESControl.InteractR_FirstCanIN";              //PLC-MES，A点AGV可安全进入：true：允许进入
        const string opcCanExChangeData = "ns=2;Warehouse.Storage.MESControl.InteractR_CanExChangeData";    //PLC-MES，RFID可以读取
        const string opcExchangeFinish = "ns=2;Warehouse.Storage.MESControl.InteractW_ExchangeFinish";      //MES-PLC，RFID读取完成
        const string opcCarArrivaledA = "ns=2;Warehouse.Storage.MESControl.InteractW_CarArrivaled";         //MES-PLC，当AGV到达时设置true通知PLC
        const string opcCanLeaveA = "ns=2;Warehouse.Storage.MESControl.InteractR_Canleave";                 //PLC-MES，AGV是否可以离开A点：true：可以离开
        const string opcFinishA = "ns=2;Warehouse.Storage.MESControl.InteractW_Finish";                     //MES-PLC，AGV离开后设置true通知PLC
        const string opcCanIncomeB1 = "ns=2;IMMES.Line.CNC.InteractR_CanIncome";                            //PLC-MES，B1点是否允许进入：true：允许进入
        const string opcCanIncomeB2 = "ns=2;IMMES.Line.CNC.InteractR_CanIncomeB2";                          //PLC-MES，B2点AGV可安全进入：true：允许进入

        static void thTaskAFunc()
        {
            string strmsg = "";
            int iTimeWait = 0;

            int id = 0;                                                                             //wms任务记录id
            bool bAgvDispatchRunning = false;                                                       //AGV调度正在运行
            bool bAgvRunTask = false;                                                               //AGV有运行任务
            string robotcode = "";                                                                  //车号
            string productsn = "";                                                                  //成品序号
            string materialsn = "";                                                                 //原料序号
            string mastertasktype = "";                                                             //主任务类型号
            string materialtasktype = "";                                                           //原料任务类型号
            string producttasktype = "";                                                            //成品任务类型号
            string stockloc = "";                                                                   //库位号
            string orderid = "";                                                                    //订单id
            string productid = "";                                                                  //产品id
            string productname = "";                                                                //产品名称
            string materialid = "";                                                                 //原料id
            string materialname = "";                                                               //原料名称
            string cause = "";                                                                      //问题原因

            bool bCanExChangeData = false;
            bool bCanIncomeA = false;
            bool bCanLeaveA = false;
            bool bCanIncomeB1 = false;
            bool bCanIncomeB2 = false;

            OpcUaClient ua = new OpcUaClient();
            DataValue dv;

            strmsg = "WmsTaskA线程启动";
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

                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "agv_sn_init", null);
                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "wms_comm_init", null);

                        strmsg = "WmsTaskA初始化";
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

            bRunningFlag = true;                                                                    //设置任务A线程运行标志
            while (true)
            {
                if (bStop && !bProductRunning)                                                      //结束线程
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
                            if (bStop && !bProductRunning)                                          //结束线程
                                break;

                            //延时等待
                            if (iTimeWait > 0)
                            {
                                iTimeWait--;
                                Thread.Sleep(1000);
                                continue;
                            }

                            dv = ua.ReadNode(new NodeId(opcCanExChangeData));
                            bCanExChangeData = (dv.Value.ToString() == "True");
                            dv = ua.ReadNode(new NodeId(opcCanIncomeA));
                            bCanIncomeA = (dv.Value.ToString() == "True");
                            dv = ua.ReadNode(new NodeId(opcCarArrivaledA));
                            bCanLeaveA = (dv.Value.ToString() == "True");
                            dv = ua.ReadNode(new NodeId(opcCanIncomeB1));
                            bCanIncomeB1 = (dv.Value.ToString() == "True");
                            dv = ua.ReadNode(new NodeId(opcCanIncomeB2));
                            bCanIncomeB2 = (dv.Value.ToString() == "True");

                            //状态机：
                            //0：判断生产状态
                            //1：启动AGV调度
                            //2：创建RF提前出库任务
                            //3：存储交互变量
                            //4：等待AGV到达A点
                            //5：按车号读取成品序号
                            //6：A点进入追踪
                            //7：更新RF出库任务
                            //8：车到A点后创建RF或U盘任务
                            //9：存储交互变量
                            //10：存储交互变量
                            //11：发送AGV到达信号
                            //12：发送数据交换完成信号
                            //13：等待立库任务完成
                            //14：生成立库入库任务
                            //15：存储交互变量
                            //16：原料序号关联到车号
                            //17：AGV发车
                            //18：AGV任务停止
                            //19：等待AGV发车或停止
                            //20：A点离开追踪
                            //21：等待立库任务完成
                            //22：通知A点操作完成
                            //23：AGV任务停止
                            //24：等待AGV直接停止

                            if (VarComm.GetVar(conn, "WMS", "ProductEnable") != "")
                                bProductEnable = true;
                            else
                                bProductEnable = false;

                            if (VarComm.GetVar(conn, "AGV", "DispatchRunning") != "")
                                bAgvDispatchRunning = true;
                            else
                                bAgvDispatchRunning = false;

                            #region 判断生产状态（0）
                            if (iStage == 0)
                            {
                                //读取生产使能标志变量
                                if (bProductEnable && !bProductRunning)
                                {
                                    VarComm.SetVar(conn, "WMS", "ProductRunning", "1");
                                    bProductRunning = true;

                                    strmsg = "立库生产启动";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);
                                }
                                //读取AGV运行状态
                                {
                                    bAgvRunTask = false;
                                    string ret = VarComm.GetVar(conn, "AGV", "RunCnt");
                                    if (ret != "" && ret != "0")
                                        bAgvRunTask = true;
                                }
                                if (!bProductEnable && bProductRunning && !bAgvRunTask)
                                {
                                    VarComm.SetVar(conn, "WMS", "ProductRunning", "");
                                    bProductRunning = false;

                                    strmsg = "立库生产停止";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);
                                }

                                if (bProductRunning)
                                    iStage = 1;
                            }
                            #endregion

                            #region  启动AGV调度（1）
                            if (iStage == 1)
                            {
                                //读取生产使能标志变量
                                {
                                    if (VarComm.GetVar(conn, "WMS", "ProductEnable") != "")
                                        bProductEnable = true;
                                    else
                                        bProductEnable = false;
                                }
                                //读取AGV调度运行状态
                                {
                                    if (VarComm.GetVar(conn, "AGV", "DispatchRunning") != "")
                                        bAgvDispatchRunning = true;
                                    else
                                        bAgvDispatchRunning = false;
                                }
                                if (bProductEnable)
                                {
                                    //生产状态
                                    if (!bAgvDispatchRunning)
                                    {
                                        //检查A点升降机位置是否放下
                                        if (bCanIncomeA)
                                        {
                                            //检查是否需要启动AGV调度
                                            strmsg = "A点接驳装置正常，检查AGV调度启动条件...";
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);

                                            VarComm.SetVar(conn, "AGV", "DispatchStart", "1");          //启动agv调度

                                            strmsg = "启动AGV调度";
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);

                                            iStage = 2;
                                        }
                                        else
                                        {
                                            strmsg = "A点升降机未复位，等一会儿再试...";
                                            formmain.logToView(strmsg);
                                            log.Error(strmsg);

                                            iStage = 0;
                                            iTimeWait = 10;
                                        }
                                    }
                                    else
                                    {
                                        //AGV已启动
                                        iStage = 2;
                                    }
                                }
                                else
                                {
                                    //停产状态
                                    iStage = 2;
                                    if (!bAgvDispatchRunning)
                                    {
                                        iStage = 0;
                                    }
                                }
                            }
                            #endregion

                            #region 创建RF提前出库任务（2）
                            if (iStage == 2)
                            {
                                //初始化变量
                                id = 0;
                                robotcode = "";
                                productsn = "";
                                materialsn = "";
                                mastertasktype = "";
                                materialtasktype = "";
                                producttasktype = "";
                                stockloc = "";
                                orderid = "";
                                productname = "";
                                materialname = "";
                                cause = "";

                                //读取AGV运行状态
                                {
                                    bAgvRunTask = false;
                                    string ret = VarComm.GetVar(conn, "AGV", "RunCnt");
                                    if (ret != "" && ret != "0")
                                        bAgvRunTask = true;
                                }
                                //检测AGV任务运行
                                if (bAgvRunTask)
                                {
                                    if (bProductRunning)
                                    {
                                        //生产状态下提前预取RF原料
                                        DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "wms_rftask_ins",
                                            new SqlParameter("productenable", (object)(bProductEnable ? 1 : 0)));

                                        id = (int)ds.Tables[0].Rows[0]["id"];
                                        materialsn = ds.Tables[0].Rows[0]["materialsn"].ToString();                 //原料的产品序号
                                        mastertasktype = ds.Tables[0].Rows[0]["mastertasktype"].ToString();         //主任务号
                                        materialtasktype = ds.Tables[0].Rows[0]["materialtasktype"].ToString();     //原料任务号
                                        producttasktype = ds.Tables[0].Rows[0]["producttasktype"].ToString();       //成品任务号
                                        stockloc = ds.Tables[0].Rows[0]["stockloc"].ToString();                     //库位号
                                        orderid = ds.Tables[0].Rows[0]["orderid"].ToString();                       //订单id
                                        productid = ds.Tables[0].Rows[0]["productid"].ToString();                   //产品id
                                        productname = ds.Tables[0].Rows[0]["productname"].ToString();               //产品name
                                        materialid = ds.Tables[0].Rows[0]["materialid"].ToString();                 //原料id
                                        materialname = ds.Tables[0].Rows[0]["materialname"].ToString();             //原料name
                                        cause = ds.Tables[0].Rows[0]["cause"].ToString();                           //问题原因

                                        if (id > 0)
                                        {
                                            strmsg = "提前预取RF出库任务发送完毕，立库任务ID：" + id.ToString()
                                                + "，原料序号：" + materialsn
                                                + "，主任务号：" + mastertasktype
                                                + "，原料任务号：" + materialtasktype
                                                + "，成品任务号：" + producttasktype
                                                + "，库位号：" + stockloc
                                                + "，订单ID：" + orderid
                                                + "，产品ID：" + productid + "，产品名称：" + productname
                                                + "，原料ID：" + materialid + "，原料名称：" + materialname;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);

                                            iStage = 3;
                                        }
                                        else if (id == 0)
                                        {
                                            strmsg = "不能提前预取RF原料出库，原因：" + cause;
                                            formmain.logToView(strmsg);
                                            log.Error(strmsg);

                                            iStage = 3;
                                        }
                                        else
                                        {
                                            iTimeWait = 1;                                          //WMS任务尚未执行完毕，延时等待
                                        }
                                    }
                                }
                                else
                                {
                                    strmsg = "AGV任务尚未启动，等一会儿再检测...";
                                    formmain.logToView(strmsg);
                                    log.Error(strmsg);

                                    iTimeWait = 10;
                                }
                            }
                            #endregion

                            #region 存储交互变量（3）
                            if (iStage == 3)
                            {
                                VarComm.SetVar(conn, "WMS", "RobotCode", "");
                                VarComm.SetVar(conn, "WMS", "ProductSN", "");
                                VarComm.SetVar(conn, "WMS", "MaterialSN", materialsn);
                                VarComm.SetVar(conn, "WMS", "MasterTaskType", mastertasktype);
                                VarComm.SetVar(conn, "WMS", "MaterialTaskType", materialtasktype);
                                VarComm.SetVar(conn, "WMS", "ProductTaskType", producttasktype);
                                VarComm.SetVar(conn, "WMS", "StockLoc", stockloc);
                                VarComm.SetVar(conn, "WMS", "OrderID", orderid);
                                VarComm.SetVar(conn, "WMS", "ProductID", productid);
                                VarComm.SetVar(conn, "WMS", "ProductName", productname);
                                VarComm.SetVar(conn, "WMS", "MaterialID", materialid);
                                VarComm.SetVar(conn, "WMS", "MaterialName", materialname);
                                VarComm.SetVar(conn, "WMS", "Cause", cause);

                                strmsg = "等待AGV到达A点...";
                                formmain.logToView(strmsg);
                                log.Error(strmsg);


                                iStage = 4;
                            }
                            #endregion

                            #region 等待AGV到达A点（4）
                            if (iStage == 4)
                            {
                                //检测AGV到达A点
                                bool bArrivedA = false;
                                if (VarComm.GetVar(conn, "AGV", "ArrivedA") != "")
                                    bArrivedA = true;

                                //读取AGV运行状态
                                {
                                    bAgvRunTask = false;
                                    string ret = VarComm.GetVar(conn, "AGV", "RunCnt");
                                    if (ret != "" && ret != "0")
                                        bAgvRunTask = true;
                                }

                                if (bAgvRunTask)
                                {
                                    //AGV调度运行，等待AGV到达
                                    if (bArrivedA)
                                    {
                                        //获取AGV车号
                                        robotcode = VarComm.GetVar(conn, "AGV", "RobotCodeA");
                                        VarComm.SetVar(conn, "WMS", "RobotCode", robotcode);

                                        strmsg = "AGV已到达A点";
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);

                                        iStage = 5;
                                    }
                                }
                                else
                                {
                                    //AGV调度已停止
                                    iStage = 0;
                                }
                            }
                            #endregion

                            #region 按车号读取成品序号（5）
                            if (iStage == 5)
                            {
                                productsn = "";
                                {
                                    DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "agv_sn_get",
                                        new SqlParameter("agvnumber", robotcode));
                                    productsn = ds.Tables[0].Rows[0]["serialnumber"].ToString();
                                }
                                VarComm.SetVar(conn, "WMS", "ProductSN", productsn);

                                strmsg = "成品序号读取成功，车号：" + robotcode + "，成品序号：" + productsn;
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 6;
                            }
                            #endregion

                            #region A点进入追踪（6）
                            if (iStage == 6)
                            {
                                if (productsn != "")
                                {
                                    Tracking.Insert(conn, "WMS", "A点进入", productsn);               //写入Tracking信息

                                    iStage = 7;
                                }
                                else
                                {
                                    iStage = 7;
                                }
                            }
                            #endregion

                            #region 更新RF出库任务（7）
                            if (iStage == 7)
                            {
                                if (id > 0)
                                {
                                    strmsg = "更新机械手任务，id：" + id;
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //已发送过RF出库任务
                                    DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "wms_rftask_upd",
                                        new SqlParameter("id", id), new SqlParameter("productsn", productsn));      //更新机械手任务：成品操作

                                    producttasktype = ds.Tables[0].Rows[0]["producttasktype"].ToString();       //成品任务号
                                    productid = ds.Tables[0].Rows[0]["productid"].ToString();                   //产品id
                                    productname = ds.Tables[0].Rows[0]["productname"].ToString();               //产品name

                                    strmsg = "更新机械手任务成功，id：" + id
                                        + "，成品任务号：" + producttasktype + "，产品ID：" + productid + "，产品名称：" + productname
;
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 9;
                                }
                                else
                                {
                                    iStage = 8;
                                }
                            }
                            #endregion

                            #region 车到A点后创建RF或U盘任务（8）
                            if (iStage == 8)
                            {
                                if (!bProductEnable && productsn == "")                         //生产停止、且空车返回
                                {
                                    iStage = 23;        //转去停止AGV
                                }
                                else
                                {
                                    strmsg = "发送RF或U盘任务，生产状态：" + (bProductEnable ? "True" : "False");
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "wms_rfustask_ins",
                                        new SqlParameter("productenable", (object)(bProductEnable ? 1 : 0)),
                                        new SqlParameter("productsn", productsn));

                                    id = (int)ds.Tables[0].Rows[0]["id"];
                                    materialsn = ds.Tables[0].Rows[0]["materialsn"].ToString();                 //原料的产品序号
                                    mastertasktype = ds.Tables[0].Rows[0]["mastertasktype"].ToString();         //主任务号
                                    materialtasktype = ds.Tables[0].Rows[0]["materialtasktype"].ToString();     //原料任务号
                                    producttasktype = ds.Tables[0].Rows[0]["producttasktype"].ToString();       //成品任务号
                                    stockloc = ds.Tables[0].Rows[0]["stockloc"].ToString();                     //库位号
                                    orderid = ds.Tables[0].Rows[0]["orderid"].ToString();                       //订单id
                                    productid = ds.Tables[0].Rows[0]["productid"].ToString();                   //产品id
                                    productname = ds.Tables[0].Rows[0]["productname"].ToString();               //产品name
                                    materialid = ds.Tables[0].Rows[0]["materialid"].ToString();                 //原料id
                                    materialname = ds.Tables[0].Rows[0]["materialname"].ToString();             //原料name
                                    cause = ds.Tables[0].Rows[0]["cause"].ToString();                           //问题原因

                                    if (id > 0)
                                    {
                                        strmsg = "出库任务发送完毕，立库任务ID：" + id.ToString()
                                            + "，原料序号：" + materialsn
                                            + "，成品序号：" + productsn
                                            + "，主任务号" + mastertasktype
                                            + "，原料任务号" + materialtasktype
                                            + "，成品任务号" + producttasktype
                                            + "，库位号：" + stockloc
                                            + "，订单ID：" + orderid
                                            + "，产品ID：" + productid + "，产品名称：" + productname
                                            + "，原料ID：" + materialid + "，原料名称：" + materialname;
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);

                                        iStage = 10;
                                    }
                                    else if (id == 0)
                                    {
                                        strmsg = "RF或U盘任务发送失败，原因：" + cause;
                                        formmain.logToView(strmsg);
                                        log.Error(strmsg);

                                        iStage = 10;
                                    }
                                    else
                                    {
                                        iTimeWait = 1;                                              //延时等待
                                    }
                                }
                            }
                            #endregion

                            #region 存储交互变量（9）
                            if (iStage == 9)
                            {
                                VarComm.SetVar(conn, "WMS", "MaterialTaskType", materialtasktype);
                                VarComm.SetVar(conn, "WMS", "ProductTaskType", producttasktype);
                                VarComm.SetVar(conn, "WMS", "ProductID", productid);
                                VarComm.SetVar(conn, "WMS", "ProductName", productname);

                                iStage = 11;
                            }
                            #endregion

                            #region 存储交互变量（10）
                            if (iStage == 10)
                            {
                                VarComm.SetVar(conn, "WMS", "MaterialSN", materialsn);
                                VarComm.SetVar(conn, "WMS", "MasterTaskType", mastertasktype);
                                VarComm.SetVar(conn, "WMS", "MaterialTaskType", materialtasktype);
                                VarComm.SetVar(conn, "WMS", "ProductTaskType", producttasktype);
                                VarComm.SetVar(conn, "WMS", "StockLoc", stockloc);
                                VarComm.SetVar(conn, "WMS", "OrderID", orderid);
                                VarComm.SetVar(conn, "WMS", "ProductID", productid);
                                VarComm.SetVar(conn, "WMS", "ProductName", productname);
                                VarComm.SetVar(conn, "WMS", "MaterialID", materialid);
                                VarComm.SetVar(conn, "WMS", "MaterialName", materialname);
                                VarComm.SetVar(conn, "WMS", "Cause", cause);

                                iStage = 11;
                            }
                            #endregion

                            #region 发送AGV到达信号（11）
                            if (iStage == 11)
                            {
                                ua.WriteNode(opcCarArrivaledA, true);                                //置AGV到达信号，启动立库/机械手操作

                                strmsg = "A点操作开始...";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 12;
                            }
                            #endregion

                            #region 发送数据交换完成信号（12）
                            if (iStage == 12)
                            {
                                if (bCanExChangeData)
                                {
                                    ua.WriteNode(opcExchangeFinish, true);                               //置RFID读取完成

                                    iStage = 13;
                                }
                            }
                            #endregion

                            #region 等待立库操作完成（13）
                            if (iStage == 13)
                            {
                                if (id > 0)
                                {
                                    Object o = SQLHelper.ExecuteScalar(conn, null, CommandType.StoredProcedure, "wms_task_isfinished", new SqlParameter("id", id));
                                    if (o.ToString() != "")
                                    {
                                        strmsg = "立库任务执行完成，立库任务ID：" + id.ToString();
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);

                                        iStage = 14;
                                    }
                                    else
                                    {
                                        iTimeWait = 1;          //延时等待，直至立库任务完成
                                    }
                                }
                                else
                                {
                                    iStage = 16;
                                }
                            }
                            #endregion

                            #region 生成立库入库任务（14）
                            if (iStage == 14)
                            {
                                strmsg = "生成立库入库任务";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "wms_intask_ins",
                                    new SqlParameter("id", id));

                                id = (int)ds.Tables[0].Rows[0]["id"];
                                mastertasktype = ds.Tables[0].Rows[0]["mastertasktype"].ToString();         //主任务号
                                materialtasktype = ds.Tables[0].Rows[0]["materialtasktype"].ToString();     //原料任务号
                                producttasktype = ds.Tables[0].Rows[0]["producttasktype"].ToString();       //成品任务号
                                stockloc = ds.Tables[0].Rows[0]["stockloc"].ToString();                     //库位号
                                cause = ds.Tables[0].Rows[0]["cause"].ToString();                           //问题原因

                                if (id > 0)
                                {
                                    strmsg = "入库任务发送完毕，立库任务ID：" + id.ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 15;
                                }
                                else if (id == 0)
                                {
                                    strmsg = "入库任务没生成：" + cause;
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 15;
                                }
                                else
                                {
                                    iTimeWait = 1;                                                  //延时等待
                                }
                            }
                            #endregion

                            #region 存储交互变量（15）
                            if (iStage == 15)
                            {
                                VarComm.SetVar(conn, "WMS", "MasterTaskType", mastertasktype);
                                VarComm.SetVar(conn, "WMS", "MaterialTaskType", materialtasktype);
                                VarComm.SetVar(conn, "WMS", "ProductTaskType", producttasktype);
                                VarComm.SetVar(conn, "WMS", "StockLoc", stockloc);
                                VarComm.SetVar(conn, "WMS", "Cause", cause);

                                iStage = 16;
                            }
                            #endregion

                            #region 原料序号关联到车号（16）
                            if (iStage == 16)
                            {
                                SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "agv_sn_set",
                                    new SqlParameter("agvnumber", (object)robotcode), new SqlParameter("serialnumber", materialsn));

                                strmsg = "原料序号写入成功，车号：" + robotcode + "，原料序号：" + materialsn;
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 17;
                            }
                            #endregion

                            #region AGV发车（17）
                            if (iStage == 17)
                            {
                                if (!bProductEnable && materialsn == "")                            //生产停止、且没有原料需要发送
                                {
                                    iStage = 18;                                                    //转去停止AGV
                                }
                                else
                                {

                                    //检测A-B1段没有正在执行的任务, 防止B1点异常时撞车
                                    bool b1free = true;
                                    try
                                    {
                                        AgvControl.lckTaskData.WaitOne();
                                        foreach (int fi in AgvControl.dicTaskData.Keys)
                                        {
                                            AgvTaskData data = AgvControl.dicTaskData[fi];
                                            if (data.srccode == "A" && data.destcode == "B1" ||
                                                data.srccode == "B1" && data.destcode == "C" && data.callbacktime == null)
                                            {
                                                b1free = false;
                                                break;
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        AgvControl.lckTaskData.Set();
                                    }

                                    if (b1free && bCanLeaveA && bCanIncomeB1 && !bCanIncomeB2)                //检测是否发车
                                    {
                                        VarComm.SetVar(conn, "AGV", "ContinueA", "1");

                                        strmsg = "AGV从A点发车，车号：" + robotcode + "，原料序号：" + materialsn;
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);

                                        iStage = 19;
                                    }
                                    else
                                    {
                                        iTimeWait = 1;      //延时等待发车条件满足
                                    }
                                }
                            }
                            #endregion

                            #region AGV任务停止（18）
                            if (iStage == 18)
                            {
                                VarComm.SetVar(conn, "AGV", "StopA", "1");

                                strmsg = "AGV停止，车号：" + robotcode;
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 19;
                            }
                            #endregion

                            #region 等待AGV发车或停止（19）
                            if (iStage == 19)
                            {
                                bool bArrivedA = false;
                                if (VarComm.GetVar(conn, "AGV", "ArrivedA") != "")
                                    bArrivedA = true;


                                if (!bArrivedA)
                                {
                                    strmsg = "AGV已发车或停止";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 20;
                                }
                            }
                            #endregion

                            #region A点离开追踪（20）
                            if (iStage == 20)
                            {
                                if (materialsn != "")
                                {
                                    Tracking.Insert(conn, "WMS", "A点离开", productsn);                //写入Tracking信息

                                    iStage = 21;
                                }
                                else
                                {
                                    iStage = 21;
                                }
                            }
                            #endregion

                            #region 通知A点操作完成（21）
                            if (iStage == 21)
                            {
                                ua.WriteNode(opcFinishA, true);                                     //置A点操作完成信号

                                strmsg = "A点操作完成信号已发送";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 22;
                            }
                            #endregion

                            #region 等待立库操作完成（22）
                            if (iStage == 22)
                            {
                                if (id > 0)
                                {
                                    Object o = SQLHelper.ExecuteScalar(conn, null, CommandType.StoredProcedure, "wms_task_isfinished", new SqlParameter("id", id));
                                    if (o.ToString() != "")
                                    {
                                        strmsg = "立库任务执行完成，立库任务ID：" + id.ToString();
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);

                                        iStage = 0;
                                    }
                                    else
                                    {
                                        iTimeWait = 1;          //延时等待，直至立库任务完成
                                    }
                                }
                                else
                                {
                                    //无立库任务
                                    iStage = 0;
                                }
                            }
                            #endregion

                            #region AGV任务停止（23）
                            if (iStage == 23)
                            {
                                VarComm.SetVar(conn, "AGV", "StopA", "1");

                                strmsg = "AGV直接停止，车号：" + robotcode;
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 24;
                            }
                            #endregion

                            #region 等待AGV直接停止（24）
                            if (iStage == 24)
                            {
                                bool bArrivedA = false;
                                if (VarComm.GetVar(conn, "AGV", "ArrivedA") != "")
                                    bArrivedA = true;


                                if (!bArrivedA)
                                {
                                    strmsg = "AGV已经直接停止";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

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
            strmsg = "WmsTaskA线程停止";
            formmain.logToView(strmsg);
            log.Info(strmsg);
            bRunningFlag = false;                                                                   //设置任务A线程停止标志
            return;
        }

        public static void Start()
        {
            //启动任务A控制
            Task.Run(() => thTaskAFunc());
        }

        public static void Stop()
        {
            //停止任务A控制
            bStop = true;
        }
    }

    //WmsTaskA变量显示
    class WmsTaskAVarRefresh
    {
        static log4net.ILog log = log4net.LogManager.GetLogger("WmsTaskAVarRefresh");
        static FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

        public static bool bStop = false;                                                           //线程停止信号
        public static bool bRunningFlag = false;                                                    //线程运行标志

        public static void thTaskAVarRefreshFunc()
        {
            log.Info("WmsTaskA变量显示线程启动");
            bRunningFlag = true;

            int iTimeWait = 0;
            string strmsg = "";
            DateTime oldts = new DateTime(1970, 1, 1);                                              //数据库最后一次刷新时戳

            while (true)
            {
                if (bStop && !WmsTaskA.bRunningFlag)
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
                            if (bStop && !WmsTaskA.bRunningFlag)
                                break;

                            //延时等待
                            if (iTimeWait > 0)
                            {
                                iTimeWait--;
                                Thread.Sleep(1000);
                                continue;
                            }

                            //查询变量最新操作时戳
                            DateTime ts = VarComm.GetLastTime(conn, "WMS");
                            if (ts > oldts)
                            {
                                //读取变量列表
                                DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "bas_comm_getallvar",
                                    new SqlParameter("sectionname", "WMS"));

                                //刷新显示
                                formmain.Invoke(new EventHandler(delegate
                                {
                                    formmain.dgWms.DataSource = ds.Tables[0];
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
            log.Info("WmsTaskA变量显示线程结束");
        }

        public static void Start()
        {
            //启动WmsTaskA变量显示线程
            Task.Run(() => thTaskAVarRefreshFunc());

        }

        public static void Stop()
        {
            //停止WmsTaskA变量显示线程
            bStop = true;
        }
    }
}

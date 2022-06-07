﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.ServiceModel.Description;
using System.ServiceModel.Activation;
using System.Data.SqlClient;
using CommonClass;
using Newtonsoft.Json;

namespace TAIAMESControlServer
{
    //AGV任务数据
    public class AgvTaskData
    {
        public int? id { get; set; }                                                                //记录ID
        public string taskcode { get; set; }                                                        //任务编码
        public string tasktype { get; set; }                                                        //AGV任务类型
        public string robotcode { get; set; }                                                       //AGV车号
        public string srccode { get; set; }                                                         //子任务起点
        public string destcode { get; set; }                                                        //子任务终点
        public DateTime? callbacktime { get; set; }                                                 //回调时间
        public int cmd { get; set; }                                                                //命令代码，1：创建，2：继续，3：取消
        public DateTime? sendtime { get; set; }                                                     //命令发送时间
    }

    class AgvControl
    {
        static log4net.ILog log = log4net.LogManager.GetLogger("AgvControl");
        static FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

        //AGV数据
        static int iTotalCnt = 0;                                                                   //AGV总数量
        static int iRunCnt = 0;                                                                     //AGV运行数量
        static string[] arRobots;                                                                   //AGV车号列表
        static string[] arRobotCodes;                                                               //AGV调度任务车号
        static List<int> listStage = new List<int>();                                               //AGV调度序号列表
        static List<DateTime> listRetryTime = new List<DateTime>();                                 //AGV初始任务重试时间
        static int iDispatchNo = -1;                                                                //启动AGV任务的调度序号
        //调度控制变量
        static bool bDispatchStart = false;                                                         //AGV调度启动标志：true：启动调度（AGV调度停止时自动置为false）
        static bool bDispatchRunning = false;                                                       //AGV调度运行状态：true：正在运行，false：已停止

        //数据变量
        public static AutoResetEvent lckTaskData = new AutoResetEvent(true);                                //任务变量锁
        public static Dictionary<int, AgvTaskData> dicTaskData = new Dictionary<int, AgvTaskData>();        //任务变量

        //线程控制变量
        public static bool bStop = false;                                                           //线程停止信号
        public static bool bRunningFlag = false;                                                    //线程运行标志

        //Web回调主机
        static ServiceHost host = null;

        //AGV主控线程函数
        static void thAgvFunc()
        {
            string strmsg = "";
            int iTimeWait = 0;

            //初始化AGV数据
            strmsg = "AgvControl线程启动";
            formmain.logToView(strmsg);
            log.Info(strmsg);

            //启动AGV回调接收
            string baseAddress = "http://localhost:8040";
            try
            {
                AgvCallback ss = new AgvCallback();
                host = new ServiceHost(ss, new Uri(baseAddress));
                host.AddServiceEndpoint(typeof(IAgvCallback), new WebHttpBinding(), "").Behaviors.Add(new WebHttpBehavior());
                host.Open();

                strmsg = "启动AGV回调接收服务成功";
                formmain.logToView(strmsg);
                log.Info(strmsg);
            }
            catch (Exception ex)
            {
                strmsg = "启动AGV回调接收服务失败：" + ex.Message;
                formmain.logToView(strmsg);
                log.Error(strmsg);
                return;
            }

            bRunningFlag = true;                                                                    //设置AGV主线程运行标志
            while (true)
            {
                //停止线程
                if (bStop)
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

                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "agv_comm_init", null);

                        strmsg = "AGV通讯变量初始化完毕";
                        formmain.logToView(strmsg);
                        log.Info(strmsg);

                        DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "agv_sn_list");

                        iTotalCnt = ds.Tables[0].Rows.Count;                                        //获取AGV总数量
                        arRobots = new string[iTotalCnt];
                        arRobotCodes = new string[iTotalCnt];
                        int i = 0;
                        foreach (DataRow dr in ds.Tables[0].Rows)
                        {
                            arRobots[i] = dr["robotcode"].ToString();                               //获取AGV车号
                            arRobotCodes[i] = "";                                                   //初始化调度任务车号
                            listStage.Add(0);                                                       //初始化AGV调度状态机
                            listRetryTime.Add(new DateTime());
                            i++;
                        }

                        VarComm.SetVar(conn, "AGV", "TotalCnt", iTotalCnt.ToString());
                        VarComm.SetVar(conn, "AGV", "RunCnt", iRunCnt.ToString());

                        strmsg = "AGV数量：" + iTotalCnt.ToString() + "，AGV列表：" + string.Join(",", arRobots);
                        formmain.logToView(strmsg);
                        log.Info(strmsg);
                        break;
                    }
                    catch (Exception ex)
                    {
                        strmsg = "DB Error: " + ex.Message + " 等待一会儿再试!";
                        formmain.logToView(strmsg);
                        log.Error(strmsg);
                        iTimeWait = 10;
                        continue;
                    }
                }
            }

            iTimeWait = 0;
            int tn = 0;                                                                             //调度序号
            bool b, bs;

            while (true)
            {
                //检测调度是否全部没有开始
                b = true;
                for (int i = 0; i < iTotalCnt; i++)
                {
                    if (listStage[i] != 0)
                    {
                        b = false;
                        break;
                    }
                }

                if (bStop && b)                                                                     //AGV调度任务尚未开始时，可以结束线程
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
                            //读取调度启动标志变量
                            {
                                if (VarComm.GetVar(conn, "AGV", "DispatchStart") != "")
                                    bDispatchStart = true;
                                else
                                    bDispatchStart = false;
                            }

                            //判断AGV调度启动
                            if (!bStop && bDispatchStart && !bDispatchRunning)
                            {
                                VarComm.SetVar(conn, "AGV", "DispatchRunning", "1");
                                bDispatchRunning = true;
                                strmsg = "AGV调度启动...";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);
                            }

                            //检测调度是否全部没有开始
                            b = true;
                            for (int i = 0; i < iTotalCnt; i++)
                            {
                                if (listStage[i] != 0)
                                {
                                    b = false;
                                    break;
                                }
                            }

                            if (bStop && b)                                                         //AGV调度任务尚未开始时，可以结束线程
                                break;

                            if (!bDispatchRunning)                                                  //尚未开始任务调度，延时等待
                            {
                                Thread.Sleep(1000);
                                continue;
                            }

                            //延时等待
                            if (iTimeWait > 0)
                            {
                                iTimeWait--;
                                Thread.Sleep(1000);
                                continue;
                            }

                            //AGV调度状态
                            //0：调度停止状态
                            //1：ykby任务启动
                            //2：保存状态到数据库
                            //3：ykby任务已到A点，等待回调
                            //4：保存状态到数据库
                            //5：设定交换变量
                            //6：ykby1任务启动A-B1
                            //7：保存状态到数据库
                            //8：设定交换变量
                            //9：ykby1任务已到B1点，等待回调
                            //10：保存状态到数据库
                            //11：设定交换变量
                            //12：ykby1任务启动B1-C
                            //13：保存状态到数据库
                            //14：设定交换变量
                            //15：ykby1任务已到C点，等待回调
                            //16：保存状态到数据库
                            //17：设定交换变量
                            //18：ykby1任务启动C-A
                            //19：保存状态到数据库
                            //20：设定交换变量
                            //21：ykby1任务已到A点，等待回调
                            //22：保存状态到数据库
                            //23：设定交换变量
                            //24：取消AGV任务
                            //25：保存状态到数据库
                            //26：设定交换变量
                            //27：检测AGV调度已全部停止
                            //28：保存状态到数据库


                            //循环处理每一个调度号
                            if (tn < iTotalCnt)
                            {
                                #region 启动调度（0）
                                if (listStage[tn] == 0 && bDispatchRunning && iDispatchNo == -1)
                                {
                                    iDispatchNo = tn;                                               //保存调度号，防止多个调度同时创建AGV任务
                                    listStage[tn] = 1;
                                }
                                #endregion

                                #region ykby任务启动（1）
                                if (listStage[tn] == 1)
                                {
                                    string reqcode = "";
                                    string taskcode = "";
                                    string robotcode = "";

                                    b = false;
                                    if (DateTime.Now > listRetryTime[tn].AddSeconds(10))            //防止不断重试
                                    {
                                        listRetryTime[tn] = DateTime.Now;
                                        //检查是否有空闲的AGV
                                        reqcode = Guid.NewGuid().ToString();                        //AGV请求代码
                                        strmsg = "查询AGV状态，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，reqcode：" + reqcode;
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        AgvStatusAnswerModel retstatus = AgvAPI.GetAgvStatus(reqcode, arRobots);
                                        if (retstatus.code == "0")
                                        {
                                            strmsg = "查询AGV状态成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                                + "，reqcode：" + retstatus.reqCode;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);

                                            if (retstatus.data.Count > iRunCnt)                     //发现可能空闲的AGV
                                                foreach (AgvStatusRetModel ret in retstatus.data)
                                                {
                                                    if (ret.statusStr == "任务空闲")
                                                    {
                                                        bool bNotExists = true;
                                                        foreach (int n in dicTaskData.Keys)
                                                        {
                                                            if (dicTaskData[n].robotcode == ret.robotCode)
                                                            {
                                                                bNotExists = false;
                                                                break;
                                                            }
                                                        }
                                                        if (bNotExists)
                                                        {
                                                            b = true;
                                                            break;
                                                        }
                                                    }
                                                }
                                        }
                                        else
                                        {
                                            strmsg = "查询AGV状态失败，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                                + "，reqcode：" + retstatus.reqCode + "，code：" + retstatus.code + "，message：" + retstatus.message;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);
                                            iTimeWait = 10;
                                            continue;
                                        }
                                    }
                                    if (b)
                                    {
                                        reqcode = Guid.NewGuid().ToString();                        //AGV请求代码
                                        taskcode = SQLHelper.ExecuteScalar(conn, null, CommandType.StoredProcedure, "bas_gencode_req",
                                                                           new SqlParameter("code", "agv_taskcode")).ToString();            //AGV任务代码

                                        //创建新任务ykby
                                        strmsg = "创建AGV任务ykby，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，reqcode：" + reqcode + "，taskcode：" + taskcode;
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        AgvAnswerModel ret = AgvAPI.CreatTask(reqcode, taskcode);

                                        if (ret.code == "0")
                                        {
                                            strmsg = "创建AGV任务ykby成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                                + "，reqcode：" + ret.reqCode;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);

                                            //生成任务数据
                                            AgvTaskData data = new AgvTaskData();
                                            data.id = null;
                                            data.taskcode = taskcode;
                                            data.tasktype = "ykby";
                                            data.robotcode = robotcode;
                                            data.srccode = "";
                                            data.destcode = "A";
                                            data.callbacktime = null;
                                            data.cmd = 1;
                                            data.sendtime = DateTime.Now;

                                            try
                                            {
                                                lckTaskData.WaitOne();
                                                dicTaskData[tn] = data;
                                            }
                                            finally
                                            {
                                                lckTaskData.Set();
                                            }
                                            iRunCnt++;                                              //AGV运行数量加1
                                            listStage[tn] = 2;
                                        }
                                        else
                                        {
                                            strmsg = "创建AGV任务ykby失败，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                                + "，reqcode：" + ret.reqCode + "，code：" + ret.code + "，message：" + ret.message;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);
                                            iTimeWait = 10;
                                            continue;
                                        }
                                    }
                                }
                                #endregion

                                #region 保存AGV状态到数据库（2）
                                if (listStage[tn] == 2)
                                {
                                    strmsg = "AGV任务数据追加到数据库，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //AGV任务记录到数据库
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        object o = SQLHelper.ExecuteScalar(conn, null, CommandType.StoredProcedure, "agv_task_ins",
                                                    SQLHelper.ModelToParameterList(data).ToArray());
                                        data.id = Convert.ToInt32(o);

                                        strmsg = "AGV任务数据追加到数据库成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，id：" + data.id + ",taskcode：" + data.taskcode
                                            + ",tasktype：" + data.tasktype + "，robotcode：" + data.robotcode
                                            + "，srccode：" + data.srccode + "，destcode：" + data.destcode + "，cmd：" + data.cmd
                                            + "，sendtime：" + data.sendtime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        listStage[tn] = 3;
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region ykby任务已到A点，等待回调（3）
                                if (listStage[tn] == 3)
                                {
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        if (data.callbacktime != null)
                                        {
                                            strmsg = "ykby任务已到A点，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);
                                            listStage[tn] = 4;
                                        }
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region 保存AGV状态到数据库（4）
                                if (listStage[tn] == 4)
                                {
                                    strmsg = "AGV任务数据更新到数据库，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //AGV回调更新到数据库
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        arRobotCodes[tn] = data.robotcode;
                                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "agv_task_upd",
                                                    SQLHelper.ModelToParameterList(data).ToArray());

                                        strmsg = "AGV任务数据更新到数据库成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，id：" + data.id + ",taskcode：" + data.taskcode
                                            + ",tasktype：" + data.tasktype + "，robotcode：" + data.robotcode
                                            + "，srccode：" + data.srccode + "，destcode：" + data.destcode + "，cmd：" + data.cmd
                                            + "，sendtime：" + data.sendtime.Value.ToString("yyyy-MM-dd HH:mm:ss")
                                            + "，callbacktime：" + data.callbacktime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        listStage[tn] = 5;
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region  设定交互变量（5）
                                if (listStage[tn] == 5)
                                {
                                    strmsg = "AGV已到达A点，设定交互变量";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    VarComm.SetVar(conn, "AGV", "RobotCodeA", arRobotCodes[tn]);
                                    VarComm.SetVar(conn, "AGV", "ContinueA", "");
                                    VarComm.SetVar(conn, "AGV", "StopA", "");
                                    VarComm.SetVar(conn, "AGV", "ArrivedA", "1");
                                    VarComm.SetVar(conn, "AGV", "RunCnt", iRunCnt.ToString());

                                    listStage[tn] = 6;
                                }
                                #endregion

                                #region ykby1任务启动A-B1（6）
                                if (listStage[tn] == 6)
                                {
                                    string reqcode = "";
                                    string taskcode = "";

                                    //查询AGV继续信号
                                    b = false;
                                    if (VarComm.GetVar(conn, "AGV", "ContinueA") != "")
                                        b = true;
                                    //查询AGV停止信号
                                    bs = false;
                                    if (VarComm.GetVar(conn, "AGV", "StopA") != "")
                                        bs = true;

                                    if (bs)                                                         //停止调度
                                    {
                                        listStage[tn] = 24;
                                    }
                                    else if (b)
                                    {

                                        reqcode = Guid.NewGuid().ToString();                        //AGV请求代码
                                        taskcode = SQLHelper.ExecuteScalar(conn, null, CommandType.StoredProcedure, "bas_gencode_req",
                                                                           new SqlParameter("code", "agv_taskcode")).ToString();            //AGV任务代码

                                        //创建新任务ykby1
                                        strmsg = "创建AGV任务ykby1，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，reqcode：" + reqcode + "，taskcode：" + taskcode + "，robotcode：" + arRobotCodes[tn];
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        AgvAnswerModel ret = AgvAPI.CreatTask1(reqcode, taskcode, arRobotCodes[tn]);

                                        if (ret.code == "0")
                                        {
                                            strmsg = "创建AGV任务ykby1成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                                + "，reqcode：" + ret.reqCode;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);

                                            //更新任务数据
                                            try
                                            {
                                                lckTaskData.WaitOne();
                                                AgvTaskData data = dicTaskData[tn];
                                                data.id = null;
                                                data.taskcode = taskcode;
                                                data.tasktype = "ykby1";
                                                data.robotcode = arRobotCodes[tn];
                                                data.srccode = "A";
                                                data.destcode = "B1";
                                                data.callbacktime = null;
                                                data.cmd = 1;
                                                data.sendtime = DateTime.Now;
                                            }
                                            finally
                                            {
                                                lckTaskData.Set();
                                            }
                                            listStage[tn] = 7;
                                        }
                                        else
                                        {
                                            strmsg = "创建AGV任务ykby1失败，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                                + "，reqcode：" + ret.reqCode + "，code：" + ret.code + "，message：" + ret.message;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);
                                            iTimeWait = 10;
                                            continue;
                                        }
                                    }
                                }
                                #endregion

                                #region 保存AGV状态到数据库（7）
                                if (listStage[tn] == 7)
                                {
                                    strmsg = "AGV任务数据追加到数据库，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //AGV任务记录到数据库
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        object o = SQLHelper.ExecuteScalar(conn, null, CommandType.StoredProcedure, "agv_task_ins",
                                                    SQLHelper.ModelToParameterList(data).ToArray());
                                        data.id = Convert.ToInt32(o);
                                        strmsg = "AGV任务数据追加到数据库成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，id：" + data.id + ",taskcode：" + data.taskcode
                                            + ",tasktype：" + data.tasktype + "，robotcode：" + data.robotcode
                                            + "，srccode：" + data.srccode + "，destcode：" + data.destcode + "，cmd：" + data.cmd
                                            + "，sendtime：" + data.sendtime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        listStage[tn] = 8;
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region  设定交互变量（8）
                                if (listStage[tn] == 8)
                                {
                                    strmsg = "AGV已离开A点，设定交互变量";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    VarComm.SetVar(conn, "AGV", "ArrivedA", "");
                                    VarComm.SetVar(conn, "AGV", "RobotCodeA", "");
                                    VarComm.SetVar(conn, "AGV", "ContinueA", "");
                                    VarComm.SetVar(conn, "AGV", "StopA", "");

                                    listStage[tn] = 9;
                                }
                                #endregion

                                #region ykby1任务已到B1点，等待回调（9）
                                if (listStage[tn] == 9)
                                {
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        if (data.callbacktime != null)
                                        {
                                            strmsg = "ykby1任务已到B1点，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);
                                            listStage[tn] = 10;
                                        }
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region 保存AGV状态到数据库（10）
                                if (listStage[tn] == 10)
                                {
                                    strmsg = "AGV任务数据更新到数据库，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //AGV回调更新到数据库
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "agv_task_upd",
                                                    SQLHelper.ModelToParameterList(data).ToArray());

                                        strmsg = "AGV任务数据更新到数据库成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，id：" + data.id + ",taskcode：" + data.taskcode
                                            + ",tasktype：" + data.tasktype + "，robotcode：" + data.robotcode
                                            + "，srccode：" + data.srccode + "，destcode：" + data.destcode + "，cmd：" + data.cmd
                                            + "，sendtime：" + data.sendtime.Value.ToString("yyyy-MM-dd HH:mm:ss")
                                            + "，callbacktime：" + data.callbacktime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        listStage[tn] = 11;
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region  设定交互变量（11）
                                if (listStage[tn] == 11)
                                {
                                    strmsg = "AGV已到达B1点，设定交互变量";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    VarComm.SetVar(conn, "AGV", "RobotCodeB1", arRobotCodes[tn]);
                                    VarComm.SetVar(conn, "AGV", "ContinueB1", "");
                                    VarComm.SetVar(conn, "AGV", "ArrivedB1", "1");
                                    iDispatchNo = -1;                                       //释放调度号

                                    listStage[tn] = 12;
                                }
                                #endregion

                                #region ykby1任务启动B1-C（12）
                                if (listStage[tn] == 12)
                                {
                                    string reqcode = "";
                                    string taskcode = "";

                                    //查询AGV继续信号
                                    if (VarComm.GetVar(conn, "AGV", "ContinueB1") != "")
                                        b = true;

                                    if (b)
                                    {
                                        try
                                        {
                                            lckTaskData.WaitOne();
                                            AgvTaskData data = dicTaskData[tn];
                                            taskcode = data.taskcode;
                                        }
                                        finally
                                        {
                                            lckTaskData.Set();
                                        }

                                        reqcode = Guid.NewGuid().ToString();                        //AGV请求代码

                                        //继续任务ykby1
                                        strmsg = "继续AGV任务ykby1，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，reqcode：" + reqcode + "，taskcode：" + taskcode + "，robotcode：" + arRobotCodes[tn];
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        AgvAnswerModel ret = AgvAPI.ContinueTask(reqcode, taskcode);

                                        if (ret.code == "0")
                                        {
                                            strmsg = "继续AGV任务ykby1成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                                + "，reqcode：" + ret.reqCode;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);

                                            //更新任务数据
                                            try
                                            {
                                                lckTaskData.WaitOne();
                                                AgvTaskData data = dicTaskData[tn];
                                                data.id = null;
                                                data.taskcode = taskcode;
                                                data.tasktype = "ykby1";
                                                data.robotcode = arRobotCodes[tn];
                                                data.srccode = "B1";
                                                data.destcode = "C";
                                                data.callbacktime = null;
                                                data.cmd = 2;
                                                data.sendtime = DateTime.Now;
                                            }
                                            finally
                                            {
                                                lckTaskData.Set();
                                            }
                                            listStage[tn] = 13;
                                        }
                                        else
                                        {
                                            strmsg = "继续AGV任务ykby1失败，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                                + "，reqcode：" + ret.reqCode + "，code：" + ret.code + "，message：" + ret.message;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);
                                            iTimeWait = 10;
                                            continue;
                                        }
                                    }
                                }
                                #endregion

                                #region 保存AGV状态到数据库（13）
                                if (listStage[tn] == 13)
                                {
                                    strmsg = "AGV任务数据追加到数据库，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //AGV任务记录到数据库
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        object o = SQLHelper.ExecuteScalar(conn, null, CommandType.StoredProcedure, "agv_task_ins",
                                                    SQLHelper.ModelToParameterList(data).ToArray());
                                        data.id = Convert.ToInt32(o);

                                        strmsg = "AGV任务数据追加到数据库成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，id：" + data.id + ",taskcode：" + data.taskcode
                                            + ",tasktype：" + data.tasktype + "，robotcode：" + data.robotcode
                                            + "，srccode：" + data.srccode + "，destcode：" + data.destcode + "，cmd：" + data.cmd
                                            + "，sendtime：" + data.sendtime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        listStage[tn] = 14;
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region  设定交互变量（14）
                                if (listStage[tn] == 14)
                                {
                                    strmsg = "AGV已离开B1点，设定交互变量";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    VarComm.SetVar(conn, "AGV", "ArrivedB1", "");
                                    VarComm.SetVar(conn, "AGV", "RobotCodeB1", "");
                                    VarComm.SetVar(conn, "AGV", "ContinueB1", "");

                                    listStage[tn] = 15;
                                }
                                #endregion

                                #region ykby1任务已到C点，等待回调（15）
                                if (listStage[tn] == 15)
                                {
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        if (data.callbacktime != null)
                                        {
                                            strmsg = "ykby1任务已到C点，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);
                                            listStage[tn] = 16;
                                        }
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region 保存AGV状态到数据库（16）
                                if (listStage[tn] == 16)
                                {
                                    strmsg = "AGV任务数据更新到数据库，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //AGV回调更新到数据库
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "agv_task_upd",
                                                    SQLHelper.ModelToParameterList(data).ToArray());

                                        strmsg = "AGV任务数据更新到数据库成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，id：" + data.id + ",taskcode：" + data.taskcode
                                            + ",tasktype：" + data.tasktype + "，robotcode：" + data.robotcode
                                            + "，srccode：" + data.srccode + "，destcode：" + data.destcode + "，cmd：" + data.cmd
                                            + "，sendtime：" + data.sendtime.Value.ToString("yyyy-MM-dd HH:mm:ss")
                                            + "，callbacktime：" + data.callbacktime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        listStage[tn] = 17;
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region  设定交互变量（17）
                                if (listStage[tn] == 17)
                                {
                                    strmsg = "AGV已到达C点，设定交互变量";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    VarComm.SetVar(conn, "AGV", "RobotCodeC", arRobotCodes[tn]);
                                    VarComm.SetVar(conn, "AGV", "ContinueC", "");
                                    VarComm.SetVar(conn, "AGV", "ArrivedC", "1");

                                    listStage[tn] = 18;
                                }
                                #endregion

                                #region ykby1任务启动C-A（18）
                                if (listStage[tn] == 18)
                                {
                                    string reqcode = "";
                                    string taskcode = "";

                                    //查询AGV继续信号
                                    b = false;
                                    if (VarComm.GetVar(conn, "AGV", "ContinueC") != "")
                                        b = true;

                                    if (b)
                                    {
                                        try
                                        {
                                            lckTaskData.WaitOne();
                                            AgvTaskData data = dicTaskData[tn];
                                            taskcode = data.taskcode;
                                        }
                                        finally
                                        {
                                            lckTaskData.Set();
                                        }

                                        reqcode = Guid.NewGuid().ToString();                        //AGV请求代码

                                        //继续任务ykby1
                                        strmsg = "继续AGV任务ykby1，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，reqcode：" + reqcode + "，taskcode：" + taskcode + "，robotcode：" + arRobotCodes[tn];
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        AgvAnswerModel ret = AgvAPI.ContinueTask(reqcode, taskcode);

                                        if (ret.code == "0")
                                        {
                                            strmsg = "继续AGV任务ykby1成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                                + "，reqcode：" + ret.reqCode;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);

                                            //更新任务数据
                                            try
                                            {
                                                lckTaskData.WaitOne();
                                                AgvTaskData data = dicTaskData[tn];
                                                data.id = null;
                                                data.taskcode = taskcode;
                                                data.tasktype = "ykby1";
                                                data.robotcode = arRobotCodes[tn];
                                                data.srccode = "C";
                                                data.destcode = "A";
                                                data.callbacktime = null;
                                                data.cmd = 2;
                                                data.sendtime = DateTime.Now;
                                            }
                                            finally
                                            {
                                                lckTaskData.Set();
                                            }
                                            listStage[tn] = 19;
                                        }
                                        else
                                        {
                                            strmsg = "继续AGV任务ykby1失败，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                                + "，reqcode：" + ret.reqCode + "，code：" + ret.code + "，message：" + ret.message;
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);
                                            iTimeWait = 10;
                                            continue;
                                        }
                                    }
                                }
                                #endregion

                                #region 保存AGV状态到数据库（19）
                                if (listStage[tn] == 19)
                                {
                                    strmsg = "AGV任务数据追加到数据库，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //AGV任务记录到数据库
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        object o = SQLHelper.ExecuteScalar(conn, null, CommandType.StoredProcedure, "agv_task_ins",
                                                    SQLHelper.ModelToParameterList(data).ToArray());
                                        data.id = Convert.ToInt32(o);

                                        strmsg = "AGV任务数据追加到数据库成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，id：" + data.id + ",taskcode：" + data.taskcode
                                            + ",tasktype：" + data.tasktype + "，robotcode：" + data.robotcode
                                            + "，srccode：" + data.srccode + "，destcode：" + data.destcode + "，cmd：" + data.cmd
                                            + "，sendtime：" + data.sendtime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        listStage[tn] = 20;
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region  设定交互变量（20）
                                if (listStage[tn] == 20)
                                {
                                    strmsg = "AGV已离开C点，设定交互变量";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    VarComm.SetVar(conn, "AGV", "ArrivedC", "");
                                    VarComm.SetVar(conn, "AGV", "RobotCodeC", "");
                                    VarComm.SetVar(conn, "AGV", "ContinueC", "");

                                    listStage[tn] = 21;
                                }
                                #endregion

                                #region ykby1任务已到A点，等待回调（21）
                                if (listStage[tn] == 21)
                                {
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        if (data.callbacktime != null)
                                        {
                                            strmsg = "ykby1任务已到A点，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                            formmain.logToView(strmsg);
                                            log.Info(strmsg);
                                            listStage[tn] = 22;
                                        }
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region 保存AGV状态到数据库（22）
                                if (listStage[tn] == 22)
                                {
                                    strmsg = "AGV任务数据更新到数据库，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //AGV回调更新到数据库
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "agv_task_upd",
                                                    SQLHelper.ModelToParameterList(data).ToArray());

                                        strmsg = "AGV任务数据更新到数据库成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，id：" + data.id + ",taskcode：" + data.taskcode
                                            + ",tasktype：" + data.tasktype + "，robotcode：" + data.robotcode
                                            + "，srccode：" + data.srccode + "，destcode：" + data.destcode + "，cmd：" + data.cmd
                                            + "，sendtime：" + data.sendtime.Value.ToString("yyyy-MM-dd HH:mm:ss")
                                            + "，callbacktime：" + data.callbacktime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        listStage[tn] = 23;
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region  设定交互变量（23）
                                if (listStage[tn] == 23)
                                {
                                    strmsg = "AGV已到达A点，设定交互变量";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    VarComm.SetVar(conn, "AGV", "RobotCodeA", arRobotCodes[tn]);
                                    VarComm.SetVar(conn, "AGV", "ContinueA", "");
                                    VarComm.SetVar(conn, "AGV", "StopA", "");
                                    VarComm.SetVar(conn, "AGV", "ArrivedA", "1");

                                    listStage[tn] = 6;
                                }
                                #endregion

                                #region AGV任务取消（24）
                                if (listStage[tn] == 24)
                                {
                                    string reqcode = "";
                                    string taskcode = "";
                                    string tasktype = "";

                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        taskcode = data.taskcode;
                                        tasktype = data.tasktype;
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }

                                    reqcode = Guid.NewGuid().ToString();                            //AGV请求代码

                                    //取消任务
                                    strmsg = "取消AGV任务，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                        + "，reqcode：" + reqcode + "，taskcode：" + taskcode + "，robotcode：" + arRobotCodes[tn] + "，tasktype" + tasktype;
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);
                                    AgvAnswerModel ret = AgvAPI.CancelTask(reqcode, taskcode);

                                    if (ret.code == "0")
                                    {
                                        strmsg = "取消AGV任务成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，reqcode：" + ret.reqCode;
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);

                                        //更新任务数据
                                        try
                                        {
                                            lckTaskData.WaitOne();
                                            AgvTaskData data = dicTaskData[tn];
                                            data.id = null;
                                            data.taskcode = taskcode;
                                            data.tasktype = "ykby1";
                                            data.robotcode = arRobotCodes[tn];
                                            data.srccode = "A";
                                            data.destcode = "";
                                            data.callbacktime = null;
                                            data.cmd = 3;
                                            data.sendtime = DateTime.Now;
                                        }
                                        finally
                                        {
                                            lckTaskData.Set();
                                        }
                                        arRobotCodes[tn] = "";                                      //清空AGV车号暂存

                                        listStage[tn] = 25;

                                        iRunCnt--;                                                  //AGV运行数量减1
                                    }
                                    else
                                    {
                                        strmsg = "取消AGV任务失败，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                            + "，reqcode：" + ret.reqCode + "，code：" + ret.code + "，message：" + ret.message;
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        iTimeWait = 10;
                                        continue;
                                    }
                                }
                                #endregion

                                #region 保存AGV状态到数据库（25）
                                if (listStage[tn] == 25)
                                {
                                    strmsg = "AGV任务数据追加到数据库，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString();
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    //AGV任务记录到数据库
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        AgvTaskData data = dicTaskData[tn];
                                        object o = SQLHelper.ExecuteScalar(conn, null, CommandType.StoredProcedure, "agv_task_ins",
                                                    SQLHelper.ModelToParameterList(data).ToArray());
                                        data.id = Convert.ToInt32(o);

                                        ; strmsg = "AGV任务数据追加到数据库成功，listStage[" + tn.ToString() + "]：" + listStage[tn].ToString()
                                              + "，id：" + data.id + ",taskcode：" + data.taskcode
                                              + ",tasktype：" + data.tasktype + "，robotcode：" + data.robotcode
                                              + "，srccode：" + data.srccode + "，destcode：" + data.destcode + "，cmd：" + data.cmd
                                              + "，sendtime：" + data.sendtime.Value.ToString("yyyy-MM-dd HH:mm:ss");
                                        formmain.logToView(strmsg);
                                        log.Info(strmsg);
                                        listStage[tn] = 26;                                         //调度状态更新
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }
                                }
                                #endregion

                                #region  设定交互变量（26）
                                if (listStage[tn] == 26)
                                {
                                    strmsg = "AGV任务已取消，设定交互变量";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    VarComm.SetVar(conn, "AGV", "ArrivedA", "");
                                    VarComm.SetVar(conn, "AGV", "RobotCodeA", "");
                                    VarComm.SetVar(conn, "AGV", "ContinueA", "");
                                    VarComm.SetVar(conn, "AGV", "StopA", "");
                                    VarComm.SetVar(conn, "AGV", "RunCnt", iRunCnt.ToString());

                                    listStage[tn] = 27;
                                }
                                #endregion

                                #region 检测AGV调度已全部停止（27）
                                if (listStage[tn] == 27)
                                {
                                    try
                                    {
                                        lckTaskData.WaitOne();
                                        dicTaskData.Remove(tn);                                     //删除任务数据
                                    }
                                    finally
                                    {
                                        lckTaskData.Set();
                                    }

                                    //检测调度是否全部停止
                                    b = true;
                                    for (int i = 0; i < iTotalCnt; i++)
                                    {
                                        if (listStage[i] != 0 && listStage[i] != 27)
                                        {
                                            b = false;
                                            break;
                                        }
                                    }

                                    if (b)
                                    {
                                        listStage[tn] = 28;
                                    }
                                }
                                #endregion

                                #region  保存状态到数据库（28）
                                if (listStage[tn] == 28)
                                {
                                    //状态变量复位
                                    VarComm.SetVar(conn, "AGV", "DispatchStart", "");
                                    VarComm.SetVar(conn, "AGV", "DispatchRunning", "");
                                    bDispatchStart = false;
                                    bDispatchRunning = false;
                                    for (int i = 0; i < iTotalCnt; i++)
                                        listStage[i] = 0;
                                    iDispatchNo = -1;

                                    strmsg = "AGV调度已停止";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);
                                }
                                #endregion 

                                tn++;
                            }
                            else
                                tn = 0;
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

            try
            {
                host.Close();

                strmsg = "关闭AGV回调接收服务成功";
                formmain.logToView(strmsg);
                log.Info(strmsg);
            }
            catch (Exception ex)
            {
                strmsg = "关闭AGV回调接收服务失败：" + ex.Message;
                formmain.logToView(strmsg);
                log.Error(strmsg);
            }

            log.Info("AgvControl线程停止");
            bRunningFlag = false;                                                                   //设置AGV主线程停止标志
            return;
        }

        //启动AGV控制系统
        public static void Start()
        {
            //启动AGV控制线程
            Task.Run(() => thAgvFunc());
        }

        //停止AGV控制系统
        public static void Stop()
        {
            //停止AGV控制系统
            bStop = true;
        }
    }

    [ServiceBehavior(UseSynchronizationContext = false, InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single, IncludeExceptionDetailInFaults = true)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class AgvCallback : IAgvCallback
    {
        static FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;
        log4net.ILog log = log4net.LogManager.GetLogger("AgvCallback");

        public AgvAnswerModel agvCallback(AgvCallbackModel o)
        {
            string strmsg = "";

            string taskcode = "";
            string robotcode = "";
            string destcode = "";

            bool b = false;

            AgvAnswerModel jsonmd = new AgvAnswerModel();
            if (o == null)
            {
                strmsg = "AGV回调参数为空";
                formmain.logToView(strmsg);
                log.Error(strmsg);
            }
            else
            {
                //foreach (PropertyInfo p in typeof(AgvCallbackModel).GetProperties())
                //{
                //    log.Info(string.Format("{0}:{1}", p.Name, p.GetValue(o)));
                //}

                taskcode = o.taskCode;
                robotcode = o.robotCode;
                destcode = o.currentCallCode;

                strmsg = "AGV回调参数，currentCallCode：" + destcode + "，taskCode：" + taskcode + "，robotCode：" + robotcode;
                formmain.logToView(strmsg);
                log.Info(strmsg);

                //更新任务数据
                b = false;
                try
                {
                    AgvControl.lckTaskData.WaitOne();
                    foreach (int tn in AgvControl.dicTaskData.Keys)
                    {
                        AgvTaskData data = AgvControl.dicTaskData[tn];
                        if (data.taskcode == taskcode)
                        {
                            data.callbacktime = DateTime.Now;
                            data.robotcode = robotcode;
                            b = true;
                            break;
                        }
                    }
                }
                finally
                {
                    AgvControl.lckTaskData.Set();
                }
                if (!b)
                {
                    strmsg = "AGV回调更新失败，任务没发现，currentCallCode：" + destcode + "，taskCode：" + taskcode + "，robotCode：" + robotcode;
                    formmain.logToView(strmsg);
                    log.Error(strmsg);
                }
            }
            jsonmd.code = "";
            jsonmd.message = "";
            jsonmd.reqCode = "";
            jsonmd.code = "0";
            jsonmd.message = "成功";
            return jsonmd;
        }
    }

    #region AGV变量显示
    class AgvVarRefresh
    {
        static log4net.ILog log = log4net.LogManager.GetLogger("AgvVarRefresh");
        static FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

        public static bool bStop = false;                                                           //线程停止信号
        public static bool bRunningFlag = false;                                                    //线程运行标志
        static void thAgvVarRefreshFunc()
        {
            log.Info("AGV变量显示线程启动");
            bRunningFlag = true;

            int iTimeWait = 0;
            string strmsg = "";
            DateTime oldts = new DateTime(1960, 1, 1);                                              //数据库最后一次刷新时戳

            while (true)
            {
                if (bStop && !AgvControl.bRunningFlag)
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
                            if (bStop && !AgvControl.bRunningFlag)
                                break;

                            //延时等待
                            if (iTimeWait > 0)
                            {
                                iTimeWait--;
                                Thread.Sleep(1000);
                                continue;
                            }

                            //查询变量最新操作时戳
                            DateTime ts = VarComm.GetLastTime(conn, "AGV");
                            if (ts > oldts)
                            {
                                //读取变量列表
                                DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "bas_comm_getallvar",
                                    new SqlParameter("sectionname", "AGV"));

                                //刷新显示
                                formmain.Invoke(new EventHandler(delegate
                                {
                                    formmain.dgAgv.DataSource = ds.Tables[0];
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
            log.Info("AGV变量显示线程结束");
        }

        public static void Start()
        {
            //启动AGV变量显示线程
            Task.Run(() => thAgvVarRefreshFunc());
        }

        public static void Stop()
        {
            //停止AGV变量显示线程
            bStop = true;
        }
    }
    #endregion
}

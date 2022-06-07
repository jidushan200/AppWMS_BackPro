using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SqlClient;
using CommonClass;

namespace TAIAMESControlServer
{
    public partial class FormMain : Form
    {
        log4net.ILog log = log4net.LogManager.GetLogger("FormMain");
        public bool bStop = false;                                                                  //程序结束标志

        string strmsg = "";

        //显示Log信息
        public delegate void logToViewDelegate(string strMsg);
        public void logToView(string strlog)
        {
            if (this.InvokeRequired)
            {
                logToViewDelegate de = new logToViewDelegate(logToView);
                strlog = "[" + Thread.CurrentThread.ManagedThreadId.ToString() + "]" + strlog;
                this.Invoke(de, new object[] { strlog });
                return;
            }
            int trlen = tbLog.MaxLength / 2;
            tbLog.Text = tbLog.Text + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "    " + strlog + "\r\n";
            int len = tbLog.Text.Length;
            if (len > trlen)                                                                        //记录超长则截断
            {
                int offset = tbLog.Text.IndexOf('\n', len - trlen / 4);
                tbLog.Text = tbLog.Text.Substring(offset + 1);
            }
            tbLog.Select(tbLog.Text.Length, 0);
            tbLog.ScrollToCaret();
            tbLog.Refresh();
        }

        public FormMain()
        {
            InitializeComponent();
        }

        //自动设置屏幕显示项位置
        private void SetFormSize()
        {
            int clientwidth = this.ClientSize.Width;
            int clientheight = this.ClientSize.Height - 1;

            if (clientwidth < 1000)
                clientwidth = 1000;
            if (clientheight < 600)
                clientheight = 600;

            //左侧上方
            panelButton.Top = 1;
            panelButton.Left = 1;
            panelButton.Width = (int)(0.4 * clientwidth);
            //左侧下方
            tbLog.Top = panelButton.Height + 2;
            tbLog.Left = 1;
            tbLog.Width = panelButton.Width;
            tbLog.Height = clientheight - tbLog.Top - 1;
            //右侧左上
            panelAGV.Top = panelButton.Top;
            panelAGV.Left = panelButton.Left + panelButton.Width + 1;
            panelAGV.Width = (clientwidth - panelAGV.Left) / 2;
            panelAGV.Height = (int)(0.6 * clientheight);
            //右侧右上
            panelWMS.Top = panelButton.Top;
            panelWMS.Left = panelAGV.Left + panelAGV.Width + 1;
            panelWMS.Width = clientwidth - panelAGV.Left - panelAGV.Width - 2;
            panelWMS.Height = panelAGV.Height;
            //右侧左下
            panelCNC.Top = panelAGV.Top + panelAGV.Height + 1;
            panelCNC.Left = panelAGV.Left;
            panelCNC.Width = panelAGV.Width;
            panelCNC.Height = clientheight - panelAGV.Top - panelAGV.Height - 2;
            //右侧右下
            panelBELT.Top = panelCNC.Top;
            panelBELT.Left = panelWMS.Left;
            panelBELT.Width = panelWMS.Width;
            panelBELT.Height = panelCNC.Height;

            //按钮
            btnStart.Left = 3;
            btnStart.Top = 3;
            btnStart.Width = 95;
            btnStart.Height = panelButton.ClientSize.Height - 6;

            btnStop.Left = btnStart.Left + btnStart.Width + 3;
            btnStop.Top = btnStart.Top;
            btnStop.Width = btnStart.Width;
            btnStop.Height = btnStart.Height;

            //AGV表格
            dgAgv.Top = lbAGVTitle.Height;
            dgAgv.Left = 1;
            dgAgv.Width = panelAGV.ClientSize.Width - 2;
            dgAgv.Height = panelAGV.ClientSize.Height - lbAGVTitle.Top - lbAGVTitle.Height - 1;
            //WMS表格
            dgWms.Top = lbWMSTitle.Height;
            dgWms.Left = 1;
            dgWms.Width = panelWMS.ClientSize.Width - 2;
            dgWms.Height = panelWMS.ClientSize.Height - lbWMSTitle.Top - lbWMSTitle.Height - 1;
            //CNC表格
            dgCnc.Top = lbCNCTitle.Height;
            dgCnc.Left = 1;
            dgCnc.Width = panelCNC.ClientSize.Width - 2;
            dgCnc.Height = panelCNC.ClientSize.Height - lbCNCTitle.Top - lbCNCTitle.Height - 1;
            //BELT表格
            dgBelt.Top = lbBELTTitle.Height;
            dgBelt.Left = 1;
            dgBelt.Width = panelBELT.ClientSize.Width - 2;
            dgBelt.Height = panelBELT.ClientSize.Height - lbBELTTitle.Top - lbBELTTitle.Height - 1;
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            this.skinEngine1.SkinFile = Application.StartupPath + @"\\SSK\\MSN.SSK";
            this.skinEngine1.Active = true;

            //启动订单生产启停控制
            ProductStartStopControl.Start();

            //启动物流任务A
            WmsTaskA.Start();

            //启动立库控制
            WmsControl.Start();

            //启动物流任务A变量刷新
            WmsTaskAVarRefresh.Start();

            //启动物流任务B
            CncTaskB.Start();

            //启动物流任务B变量刷新
            CncTaskBVarRefresh.Start();

            //启动物流任务C
            BeltTaskC.Start();

            //启动物流任务C变量刷新
            BeltTaskCVarRefresh.Start();

            //启动激光打标机控制任务
            //LaserMarkControl.Start();

            //启动AGV控制
            AgvControl.Start();

            //启动AGV变量刷新
            AgvVarRefresh.Start();

        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

                string strmsg = "";
                using (SqlConnection conn = new SqlConnection(Properties.Settings.Default.ConnString))
                {
                    try
                    {
                        strmsg = "订单生产启动命令发送开始...";
                        formmain.logToView(strmsg);
                        log.Info(strmsg);

                        conn.Open();
                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "sch_running_cmd", new SqlParameter("cmd", 1));

                        strmsg = "订单生产启动命令发送完毕";
                        formmain.logToView(strmsg);
                        log.Info(strmsg);
                    }
                    catch (Exception ex)
                    {
                        strmsg = "Error: " + ex.Message;
                        formmain.logToView(strmsg);
                        log.Error(strmsg);
                        formmain.Invoke(new EventHandler(delegate
                        {
                            MessageBox.Show(strmsg, "订单生产启动命令发送错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                }
            });
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

                string strmsg = "";
                using (SqlConnection conn = new SqlConnection(Properties.Settings.Default.ConnString))
                {
                    try
                    {
                        strmsg = "订单生产停止命令发送开始...";
                        formmain.logToView(strmsg);
                        log.Info(strmsg);

                        conn.Open();
                        SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "sch_running_cmd", new SqlParameter("cmd", (object)0));

                        strmsg = "订单生产停止命令发送完毕";
                        formmain.logToView(strmsg);
                        log.Info(strmsg);
                    }
                    catch (Exception ex)
                    {
                        strmsg = "Error: " + ex.Message;
                        formmain.logToView(strmsg);
                        log.Error(strmsg);
                        formmain.Invoke(new EventHandler(delegate
                        {
                            MessageBox.Show(strmsg, "订单生产停止命令发送错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }));
                    }
                }
            });
        }

        private void FormMain_FormClosed(object sender, FormClosedEventArgs e)
        {
            strmsg = "自动产线控制系统已停止";
            log.Info(strmsg);

        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            strmsg = "自动产线控制系统停止...";
            logToView(strmsg);
            log.Info(strmsg);

            bool b = true;
            Task.Run(() =>
            {
                FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

                string strmsg = "";
                int iTimeWait = 0;
                while (true)
                {
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
                            strmsg = "禁止启动标志入库开始...";
                            formmain.logToView(strmsg);
                            log.Info(strmsg);

                            conn.Open();
                            SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "sch_running_sysstop", null);

                            strmsg = "禁止启动标志入库已完成";
                            formmain.logToView(strmsg);
                            log.Info(strmsg);
                            break;
                        }
                        catch (Exception ex)
                        {
                            strmsg = "Error: " + ex.Message;
                            formmain.logToView(strmsg);
                            log.Error(strmsg);
                            iTimeWait = 10;
                        }
                    }
                }
                b = false;
            });

            //等待禁止启动标志入库完成
            while (b)
            {
                Application.DoEvents();
            }

            bStop = true;
            ProductStartStopControl.Stop();
            WmsTaskA.Stop();
            WmsTaskAVarRefresh.Stop();
            WmsControl.Stop();
            CncTaskB.Stop();
            CncTaskBVarRefresh.Stop();
            BeltTaskC.Stop();
            BeltTaskCVarRefresh.Stop();
            LaserMarkControl.Stop();
            AgvControl.Stop();
            AgvVarRefresh.Stop();
            strmsg = "子线程正在停止...";
            logToView(strmsg);
            log.Error(strmsg);
            while (AgvControl.bRunningFlag
                    || AgvVarRefresh.bRunningFlag
                    || WmsTaskA.bRunningFlag
                    || WmsControl.bRunningFlag
                    || WmsTaskAVarRefresh.bRunningFlag
                    || CncTaskB.bRunningFlag
                    || CncTaskBVarRefresh.bRunningFlag
                    || BeltTaskC.bRunningFlag
                    || BeltTaskCVarRefresh.bRunningFlag
                    || LaserMarkControl.bRunningFlag
                    || ProductStartStopControl.bRunningFlag)
            {
                Application.DoEvents();
            }

        }

        private void FormMain_SizeChanged(object sender, EventArgs e)
        {
            SetFormSize();
        }
    }

    #region 订单生产启停控制
    class ProductStartStopControl
    {
        static log4net.ILog log = log4net.LogManager.GetLogger("ProductStartStopControl");
        static FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

        public static bool bStop = false;                                                           //线程停止信号
        public static bool bRunningFlag = false;                                                    //线程运行标志

        public static int iStage = 0;                                                               //状态机

        static void thProductStartStopControlFunc()
        {
            string strmsg = "";
            int iTimeWait = 0;

            strmsg = "订单生产启停控制线程启动";
            formmain.logToView(strmsg);
            log.Info(strmsg);

            int s_state = 0;                                                                        //保存运行状态
            DateTime s_timestamp = new DateTime(2000, 1, 1);                                        //保存命令时戳
            DateTime s_exectimestamp = new DateTime(2000, 1, 1);                                    //保存命令执行时戳
            int EnStart = 0;                                                                        //启动按钮允许标志
            int EnStop = 0;                                                                         //停止按钮允许标志

            bRunningFlag = true;
            while (true)
            {
                //停止线程
                if (bStop && iStage <= 1 && EnStart == 1 && EnStop == 0)
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
                            //停止线程
                            if (bStop && iStage <= 1 && EnStart == 1 && EnStop == 0)
                                break;

                            //状态机变量：
                            //0：初始化
                            //1：按数据表设定按钮状态、等待启停命令
                            //2：启动
                            //3：等待启动完成
                            //4：停止
                            //5：等待停止完成

                            #region 初始化（0）
                            if (iStage == 0)
                            {
                                //关闭启动和停止按钮
                                formmain.Invoke(new EventHandler(delegate
                                {
                                    formmain.btnStart.Enabled = false;
                                    formmain.btnStop.Enabled = false;
                                }));

                                //初始化数据表sch_running
                                SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "sch_running_init", null);
                                iStage = 1;

                                strmsg = "生产订单启停控制初始化完成";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);
                            }
                            #endregion

                            #region 按数据表设定按钮状态、等待启停命令（1）
                            if (iStage == 1)
                            {
                                DataSet ds = SQLHelper.QueryDataSet(conn, null, CommandType.StoredProcedure, "sch_running_sel", null);

                                int state = (int)ds.Tables[0].Rows[0]["state"];                     //运行状态
                                DateTime timestamp = (DateTime)ds.Tables[0].Rows[0]["timestamp"];   //命令时戳
                                DateTime exectimestamp = (DateTime)ds.Tables[0].Rows[0]["exectimestamp"];       //命令执行时戳

                                EnStart = (int)ds.Tables[1].Rows[0]["EnStart"];                     //启动按钮状态
                                EnStop = (int)ds.Tables[1].Rows[0]["EnStop"];                       //停止按钮状态

                                //检测运行状态
                                bool b = false;                                                     //状态改变标志
                                if (timestamp != s_timestamp || exectimestamp != s_exectimestamp)
                                {
                                    b = true;                                                       //状态已改变需要刷新
                                    s_timestamp = timestamp;
                                    s_exectimestamp = exectimestamp;
                                }
                                //启动或停止订单生产
                                if (b)
                                {
                                    if (state != s_state)                                           //订单生产状态改变
                                    {
                                        s_state = state;
                                        if (state == 1)
                                            iStage = 2;                                             //启动订单生产
                                        else
                                            iStage = 4;                                             //停止订单生产
                                    }
                                }
                                //刷新运行状态显示
                                if (b)
                                {
                                    formmain.Invoke(new EventHandler(delegate
                                    {
                                        formmain.btnStart.Enabled = (EnStart == 1);                 //更新启动按钮状态
                                        formmain.btnStop.Enabled = (EnStop == 1);                   //更新停止按钮状态
                                    }));
                                }
                            }
                            #endregion

                            #region 启动（2）
                            if (iStage == 2)
                            {
                                VarComm.SetVar(conn, "WMS", "ProductEnable", "1");                  //设置启动生产变量标志

                                strmsg = "启动订单生产命令发布...";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 3;                                                         //等待启动完成
                                Thread.Sleep(1000);
                            }
                            #endregion

                            #region 等待启动完成（3）
                            if (iStage == 3)
                            {
                                //等待启动完成
                                if (VarComm.GetVar(conn, "WMS", "ProductRunning") != "")
                                {
                                    //订单生产已开始
                                    SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "sch_running_finish", null);

                                    strmsg = "启动订单生产命令已执行完成";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 1;                                                     //启动完成后转状态监测
                                }
                            }
                            #endregion

                            #region 停止（4）
                            if (iStage == 4)
                            {
                                //设置停止标志
                                VarComm.SetVar(conn, "WMS", "ProductEnable", "");

                                strmsg = "停止订单生产命令已发布...";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 5;                                                         //等待停止完成
                                Thread.Sleep(1000);
                            }
                            #endregion

                            #region 等待停止完成（5）
                            if (iStage == 5)
                            {
                                //等待停止完成
                                if (VarComm.GetVar(conn, "WMS", "ProductRunning") == "")
                                {
                                    //等待生产停止
                                    SQLHelper.ExecuteNonQuery(conn, null, CommandType.StoredProcedure, "sch_running_finish", null);

                                    strmsg = "停止订单生产命令已执行完成";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 1;
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
                        log.Error(strmsg);
                        iTimeWait = 10;
                        continue;
                    }
                }
            }

            bRunningFlag = false;
            strmsg = "订单生产启停控制线程停止";
            formmain.logToView(strmsg);
            log.Info(strmsg);
        }

        public static void Start()
        {
            //启动订单生产启停控制线程
            Task.Run(() => thProductStartStopControlFunc());
        }

        public static void Stop()
        {
            //停止订单生产启停控制线程
            bStop = true;
        }
    }
    #endregion
}

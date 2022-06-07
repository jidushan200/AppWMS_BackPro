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
    public class LaserMarkControl
    {
        static log4net.ILog log = log4net.LogManager.GetLogger("LaserMarkControl");
        static FormMain formmain = (FormMain)MyApplicationContext.CurrentContext.MainForm;

        //线程控制变量
        public static bool bStop = false;                                                           //线程停止信号
        public static bool bRunningFlag = false;                                                    //线程运行标志

        static int iStage = 0;

        //OPC节点
        const string opcPrintNeed = "ns=2;IMMES.Line.MES.InteractR_PrintNeed";                      //PLC-MES，需要激光打标
        const string opcPrintFinish = "ns=2;IMMES.Line.MES.InteractW_PrintFinish";                  //MES-PLC，打标完成

        static void thTaskLaserMarkFunc()
        {
            string strmsg = "";
            int iTimeWait = 0;

            bool bPrintNeed = false;

            string printproductsn = "";


            OpcUaClient ua = new OpcUaClient();
            DataValue dv;

            strmsg = "LaserMarkControl线程启动";
            formmain.logToView(strmsg);
            log.Info(strmsg);

            iTimeWait = 0;
            bRunningFlag = true;                                                                    //设置AlarmAudioControl线程停止标志
            while (true)
            {
                if (bStop && iStage == 0)                                                           //结束线程
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
                            if (bStop && iStage == 0)                                               //结束线程
                                break;

                            //延时等待
                            if (iTimeWait > 0)
                            {
                                iTimeWait--;
                                Thread.Sleep(1000);
                                continue;
                            }

                            //读取OPC数据点
                            dv = ua.ReadNode(new NodeId(opcPrintNeed));
                            bPrintNeed = (dv.Value.ToString() == "True");

                            #region 等待打标请求信号（0）
                            if (iStage == 0)
                            {
                                if (bPrintNeed)
                                {
                                    strmsg = "收到激光打标请求信号";
                                    formmain.logToView(strmsg);
                                    log.Info(strmsg);

                                    iStage = 1;
                                }
                            }
                            #endregion 打标操作（1）
                            if (iStage == 1)
                            {

                                iStage = 2;
                            }

                            #region 发送打标完成信号（2）
                            if (iStage == 2)
                            {
                                ua.WriteNode(opcPrintFinish, true);

                                strmsg = "打标完成信号已发送，等待请求信号复位...";
                                formmain.logToView(strmsg);
                                log.Info(strmsg);

                                iStage = 3;
                            }
                            #endregion

                            #region 等待请求信号复位
                            if (iStage == 3)
                            {
                                if (!bPrintNeed)
                                {
                                    strmsg = "打标请求信号已复位";
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

            strmsg = "LaserMarkControl线程停止";
            formmain.logToView(strmsg);
            log.Info(strmsg);
            bRunningFlag = false;                                                                   //设置AlarmAudioControl线程停止标志
            return;
        }

        public static void Start()
        {
            //启动LaserMarkControl线程
            Task.Run(() => thTaskLaserMarkFunc());
        }

        public static void Stop()
        {
            //停止LaserMarkControl线程
            bStop = true;
        }
    }
}

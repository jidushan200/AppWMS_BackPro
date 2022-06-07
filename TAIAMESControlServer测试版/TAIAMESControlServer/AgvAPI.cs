using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.ServiceModel.Description;
using System.ServiceModel.Activation;
using Newtonsoft.Json;
using System.IO;
using System.Net;

namespace TAIAMESControlServer
{
    [DataContract]
    public class AgvAnswerModel
    {
        [DataMember]//可被序列化
        public string code { get; set; }
        [DataMember]
        public string message { get; set; }
        [DataMember]
        public string reqCode { get; set; }
    }

    [DataContract]
    public class AgvCallbackModel
    {
        [DataMember]
        public string berthCode { get; set; }
        [DataMember]
        public string callCode { get; set; }
        [DataMember]
        public string clientCode { get; set; }      //客户端编号
        [DataMember]
        public string currentCallCode { get; set; }
        [DataMember]
        public string data { get; set; }            //自定义数据
        [DataMember]
        public string indBind { get; set; }
        [DataMember]
        public string method { get; set; }
        [DataMember]
        public string podCode { get; set; }
        [DataMember]
        public string reqCode { get; set; }         //请求编码
        [DataMember]
        public string reqTime { get; set; }         //请求时间
        [DataMember]
        public string robotCode { get; set; }       //Agv编码
        [DataMember]
        public string taskCode { get; set; }        //任务编码
        [DataMember]
        public string tokenCode { get; set; }       //令牌编码
        [DataMember]
        public List<string> userCallCodePath { get; set; }
    }

    [ServiceContract]
    public interface IAgvCallback
    {
        [OperationContract]
        [WebInvoke(UriTemplate = "/agvCallbackService/agvCallback.aspx", Method = "POST", ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json)]
        AgvAnswerModel agvCallback(AgvCallbackModel o);
    }

    //Agv创建任务数据
    public class AgvSchedulingTaskModel
    {
        public string reqCode { set; get; }
        public string reqTime { set; get; }
        public string clientCode { set; get; }
        public string tokenCode { set; get; }
        public string taskTyp { set; get; }
        public string userCallCode { set; get; }
        public string[] userCallCodePath { set; get; }
        public string podCode { set; get; }
        public string podDir { set; get; }
        public string priority { set; get; }
        public string taskCode { set; get; }
        public string robotCode { set; get; }
        public string data { set; get; }
    }

    //Agv继续执行任务数据
    public class AgvContinueTaskModel
    {
        public string reqCode { set; get; }
        public string reqTime { set; get; }
        public string clientCode { set; get; }
        public string tokenCode { set; get; }
        public string userCallCode { set; get; }
        public string taskCode { set; get; }
        public string data { set; get; }
    }

    //Agv取消任务数据
    public class AgvCancleTaskModel
    {
        public string reqCode { set; get; }
        public string reqTime { set; get; }
        public string clientCode { set; get; }
        public string tokenCode { set; get; }
        public string taskCode { set; get; }
    }

    //查询Agv状态数据
    public class AgvStatusModel
    {
        public string reqCode { set; get; }
        public string reqTime { set; get; }
        public string clientCode { set; get; }
        public string tokenCode { set; get; }
        public string robotCount { set; get; }
        public string[] robots { set; get; }
        public string mapShortName { set; get; }
    }

    public class AgvStatusRetModel
    {
        public string battery { set; get; }                 //机器人电量
        public string direction { set; get; }               //机器人方向
        public string podCode { set; get; }                 //背的货架 Code
        public string podDir { set; get; }                  //背的货架方向
        public string posX { set; get; }                    //机器人 X 坐标
        public string posY { set; get; }                    //机器人 Y 坐标
        public string exclude { set; get; }                 //是否已被排除,不接受新任务(1:排除, 0:未排除)
        public string excludeStr { set; get; }
        public string robotCode { set; get; }               //Agv 编号
        public string robotIp { set; get; }                 //机器人 IP
        public string status { set; get; }                  //是否在线(1:在线, 0:离线) 还有其他种类，是否异常的状态
        public string statusStr { set; get; }
        public string stop { set; get; }                    //是否暂停状态 0-否 1-是
        public string stopStr { set; get; }
    }

    public class AgvStatusAnswerModel
    {
        [DataMember]
        public string code { get; set; }
        [DataMember]
        public string message { get; set; }
        [DataMember]
        public string reqCode { get; set; }
        public List<AgvStatusRetModel> data{get;set;} 
    }

    public class HttpUtil
    {
        public static string HttpPost(string Url, string postDataStr)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = Encoding.UTF8.GetByteCount(postDataStr);
            Stream myRequestStream = request.GetRequestStream();
            StreamWriter myStreamWriter = new StreamWriter(myRequestStream, Encoding.GetEncoding("gb2312"));
            myStreamWriter.Write(postDataStr);
            myStreamWriter.Close();
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream myResponseStream = response.GetResponseStream();
            StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
            string retString = myStreamReader.ReadToEnd();
            myStreamReader.Close();
            myResponseStream.Close();
            return retString;
        }
        public static string HttpGet(string Url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url);
            request.Method = "GET";
            request.ContentType = "application/json";
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream myResponseStream = response.GetResponseStream();
            StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
            string retString = myStreamReader.ReadToEnd();
            myStreamReader.Close();
            myResponseStream.Close();
            return retString;
        }
    }

    class AgvAPI
    {
        private static string serverBaseUri = "http://192.168.10.186:9090/rcs/services/rest/hikRpcService";

        static log4net.ILog log = log4net.LogManager.GetLogger("AgvAPI");

        public AgvAPI()
        {

        }

        //创建Agv任务
        public static AgvAnswerModel CreatTask(string ReqCode, string TaskCode)
        {

            AgvSchedulingTaskModel agv = new AgvSchedulingTaskModel();
            agv.reqCode = ReqCode;
            agv.reqTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            agv.podCode = "";
            agv.podDir = "";
            agv.priority = "";
            agv.robotCode = "";
            agv.taskCode = TaskCode;
            agv.data = "";
            agv.clientCode = "1";
            agv.tokenCode = "1";
            agv.taskTyp = "ykby";
            agv.userCallCode = "A";
            //string[] path = { };
            //agv.userCallCodePath = path;

            string json = JsonConvert.SerializeObject(agv, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            log.Info("Agv创建任务参数：\n" + json);

            string strurl = serverBaseUri + "/genAgvSchedulingTask";
            string ret = HttpUtil.HttpPost(strurl, json);

            log.Info("Agv创建任务返回结果：\n" + ret);
            return JsonConvert.DeserializeObject(ret, typeof(AgvAnswerModel)) as AgvAnswerModel;
        }

        public static AgvAnswerModel CreatTask1(string ReqCode, string TaskCode, string robotCode)
        {

            AgvSchedulingTaskModel agv = new AgvSchedulingTaskModel();
            agv.reqCode = ReqCode;
            agv.reqTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            agv.podCode = "";
            agv.podDir = "";
            agv.priority = "";
            agv.robotCode = robotCode;
            agv.taskCode = TaskCode;
            agv.data = "";
            agv.clientCode = "1";
            agv.tokenCode = "1";
            agv.taskTyp = "ykby1";
            agv.userCallCode = "A";
            //string[] path = { };
            //agv.userCallCodePath = path;

            string json = JsonConvert.SerializeObject(agv, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            log.Info("Agv创建任务参数：\n" + json);

            string strurl = serverBaseUri + "/genAgvSchedulingTask";
            string ret = HttpUtil.HttpPost(strurl, json);

            log.Info("Agv创建任务返回结果：\n" + ret);
            return JsonConvert.DeserializeObject(ret, typeof(AgvAnswerModel)) as AgvAnswerModel;
        }

        //Agv继续执行任务
        public static AgvAnswerModel ContinueTask(string ReqCode, string TaskCode)
        {
            AgvContinueTaskModel agv = new AgvContinueTaskModel();
            agv.reqCode = ReqCode;
            agv.reqTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            agv.clientCode = "1";
            agv.tokenCode = "1";
            agv.taskCode = TaskCode;
            agv.data = "";

            string json = JsonConvert.SerializeObject(agv, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            log.Info("Agv继续执行任务参数：\n" + json);

            string strurl = serverBaseUri + "/continueTask";
            string ret = HttpUtil.HttpPost(strurl, json);

            log.Info("Agv继续执行任务返回结果：\n" + ret);
            return JsonConvert.DeserializeObject(ret, typeof(AgvAnswerModel)) as AgvAnswerModel;
        }

        //Agv取消任务
        public static AgvAnswerModel CancelTask(string ReqCode, string TaskCode)
        {
            AgvCancleTaskModel agv = new AgvCancleTaskModel();
            agv.reqCode = ReqCode;
            agv.reqTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            agv.clientCode = "1";
            agv.tokenCode = "1";
            agv.taskCode = TaskCode;

            string json = JsonConvert.SerializeObject(agv, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            log.Info("Agv取消任务参数：\n" + json);

            string strurl = serverBaseUri + "/cancelTask";
            string ret = HttpUtil.HttpPost(strurl, json);

            log.Info("Agv取消任务返回结果：\n" + ret);
            return JsonConvert.DeserializeObject(ret, typeof(AgvAnswerModel)) as AgvAnswerModel;
        }

        //查询Agv状态
        public static AgvStatusAnswerModel GetAgvStatus(string ReqCode, string[] robotsArray)
        {
            AgvStatusModel agv = new AgvStatusModel();
            agv.reqCode = ReqCode;
            agv.reqTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            agv.clientCode = "1";
            agv.tokenCode = "1";
            agv.robotCount = "";
            agv.robots = robotsArray;
            string json = JsonConvert.SerializeObject(agv, new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });
            log.Info("Agv状态查询参数：\n" + json);

            string strurl = serverBaseUri + "/getAgvStatus";
            string ret = HttpUtil.HttpPost(strurl, json);

            log.Info("Agv状态查询返回结果：\n" + ret);
            return JsonConvert.DeserializeObject(ret, typeof(AgvStatusAnswerModel)) as AgvStatusAnswerModel;
        }
    }
}

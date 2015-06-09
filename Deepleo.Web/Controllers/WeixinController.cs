using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Xml.Linq;
using Deepleo.Weixin.SDK;
using System.Xml;
using Tencent;
using Deepleo.Web.Services;

namespace Deepleo.Web.Controllers
{
    public class WeixinController : Controller
    {
        private string token = WeixinConfig.Token;//微信公众平台后台设置的Token
        public WeixinController()
        {

        }

        /// <summary>
        /// 微信后台验证地址（使用Get），微信后台的“接口配置信息”的Url
        /// </summary>
        [HttpGet]
        [ActionName("Index")]
        public ActionResult Get(string signature, string timestamp, string nonce, string echostr)
        {

            if (string.IsNullOrEmpty(token)) return Content("请先设置Token！");

            //在设置URL或token后，微信都会提交get请求，来访问我们后端服务。验证通过之后，微信其他请求都是通过POST方式提交。
            //所以在代码中，我们常常会根据请求的方式来判断是否进行签名验证。
            var ent = "";
            if (!BasicAPI.CheckSignature(signature, timestamp, nonce, token, out ent))
            {
                return Content("参数错误！");
            }
            return Content(echostr); //返回随机字符串则表示验证通过
        }

        /// <summary>
        /// 用户发送消息后，微信平台自动Post一个请求到这里，并等待响应XML。
        /// </summary>
        [HttpPost]
        [ActionName("Index")]
        public ActionResult Post(string signature, string timestamp, string nonce, string echostr)
        {


            //尽管微信其他请求是以POST提交的，但是其URL中同样携带了签名信息，我们同样需要进行签名认证。所以为了安全起见，建议每次请求都进行签名认证。
            var ent = "";
            if (!BasicAPI.CheckSignature(signature, timestamp, nonce, token, out ent))
            {
                return Content("参数错误！");
            }

            WeixinMessage message = null;
            var safeMode = Request.QueryString.Get("encrypt_type") == "aes";
            using (var streamReader = new StreamReader(Request.InputStream))
            {
                var decryptMsg = string.Empty;
                var msg = streamReader.ReadToEnd();

                #region 解密
                if (safeMode)
                {
                    var msg_signature = Request.QueryString.Get("msg_signature");
                    var wxBizMsgCrypt = new WXBizMsgCrypt(WeixinConfig.Token, WeixinConfig.EncodingAESKey, WeixinConfig.AppID);
                    var ret = wxBizMsgCrypt.DecryptMsg(msg_signature, timestamp, nonce, msg, ref decryptMsg);
                    if (ret != 0)//解密失败
                    {
                        //TODO：开发者解密失败的业务处理逻辑
                        //注意：本demo用log4net记录此信息，你可以用其他方法
                        LogWriter.Default.WriteError(string.Format("解密失败返回状态{0}, 用户发送消息后，微信平台Post到这里的XML数据{1}", ret, msg));
                    }
                }
                else
                {
                    decryptMsg = msg;
                }
                #endregion


                message = AcceptMessageAPI.Parse(decryptMsg);


                //测试时可开启此记录，帮助跟踪数据。
                string path = Server.MapPath("~/Log/" + DateTime.Now.Ticks + "_服务器接收用户_" + message.Body.FromUserName.Value + "的信息.txt");
                System.IO.File.AppendAllText(path, path + decryptMsg, Encoding.UTF8);


            }
            //执行微信处理过程
            var response = new WeixinExecutor().Execute(message);



            var encryptMsg = string.Empty;

            #region 加密
            if (safeMode)
            {
                var msg_signature = Request.QueryString.Get("msg_signature");
                var wxBizMsgCrypt = new WXBizMsgCrypt(WeixinConfig.Token, WeixinConfig.EncodingAESKey, WeixinConfig.AppID);
                var ret = wxBizMsgCrypt.EncryptMsg(response, timestamp, nonce, ref encryptMsg);
                if (ret != 0)//加密失败
                {
                    //TODO：开发者加密失败的业务处理逻辑
                    LogWriter.Default.WriteError(string.Format("加密失败返回状态{0},开发者发给微信平台的XML数据 {1}", ret, response));
                }
            }
            else
            {
                encryptMsg = response;
            }
            #endregion

            //测试时可开启此记录，帮助跟踪数据。
            string path2 = Server.MapPath("~/Log/" + DateTime.Now.Ticks + "_服务器发送给用户_" + message.Body.ToUserName.Value + "的信息.txt");
            System.IO.File.AppendAllText(path2, response, Encoding.UTF8);

            return new ContentResult
            {
                Content = encryptMsg,
                ContentType = "text/xml",
                ContentEncoding = System.Text.UTF8Encoding.UTF8
            };
        }

    }
}

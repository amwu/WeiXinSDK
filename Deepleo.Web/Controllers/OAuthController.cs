﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Deepleo.Web.Attribute;
using Deepleo.Weixin.SDK;
using Deepleo.Web.Services;
using System.Threading;

namespace Deepleo.Web.Controllers
{
    public class OAuthController : Controller
    {

        [WeixinOAuthAuthorize]
        public ActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public ActionResult Callback()
        {
            var code = Request.QueryString.Get("code");

            LogWriter.Default.WriteWarning("----------------------路过------------------------");

            if (string.IsNullOrEmpty(code))//没有code表示授权失败
            {
                return RedirectToAction("Failed", "OAuth");
            }


            var state = Request.QueryString.Get("state");

            var cache_status = System.Web.HttpContext.Current.Cache.Get(state);

            var redirect_url = cache_status == null ? "/OAuth" : cache_status.ToString();//没有获取到state,就跳转到首页


            var scope = WeixinConfig.OauthScope;
            var access_token_scope = "";
            double expires_in = 0;
            var access_token = "";
            var openId = "";

            string resultjson;

            var token = OAuth2API.GetAccessToken(code, WeixinConfig.AppID, WeixinConfig.AppSecret, out resultjson);

            //错误时微信会返回JSON数据包如下（示例为Code无效错误）:
            //{"errcode":40029,"errmsg":"invalid code"} 等于-1 不存在错误
            if (resultjson.IndexOf("errcode") >= 0)
            {
                LogWriter.Default.WriteError(resultjson);
                string rcode = WeixinReturnCode.CodeHashtable[Convert.ToInt32(token.errcode)].ToString();
                return Content(rcode);
            }

            dynamic userinfo;
            if (scope == "snsapi_userinfo")
            {
                var refreshAccess_token = OAuth2API.RefreshAccess_token(token.refresh_token, WeixinConfig.AppID, out resultjson);
                access_token = refreshAccess_token.access_token;//通过code换取的是一个特殊的网页授权access_token，与基础支持中的access_token（该access_token用于调用其他接口）不同。
                openId = refreshAccess_token.openid;
                access_token_scope = refreshAccess_token.scope;
                expires_in = refreshAccess_token.expires_in;
                userinfo = OAuth2API.GetUserInfo(access_token, openId, out resultjson);//snsapi_userinfo,可以用户在未关注公众号的情况下获取用户基本信息
            }
            else
            {
                access_token = WeixinConfig.TokenHelper.GetToken();//基础支持中的access_token
                openId = token.openid;
                expires_in = token.expires_in;
                //TODO: 如果用户已经关注，可以用openid，获取用户信息。
                userinfo = UserAdminAPI.GetInfo(access_token, openId, out resultjson);//如果本地已经存储了用户基本信息，建议在本地获取。
            }



            //错误时微信会返回JSON数据包如下（示例为Code无效错误）:
            //{"errcode":40029,"errmsg":"invalid code"} 等于-1 不存在错误
            if (resultjson.IndexOf("errcode") >= 0)
            {
                LogWriter.Default.WriteError(resultjson);
                string rcode = WeixinReturnCode.CodeHashtable[Convert.ToInt32(token.errcode)].ToString();
                return Content(rcode);
            }


            //写入cookies
            AuthorizationManager.SetTicket(true, 1, openId, userinfo.nickname);
            Thread.Sleep(500);//暂停半秒钟，以等待IOS设置Cookies的延迟

            //LogWriter.Default.WriteWarning(string.Format("OAuth success: identity: {0} , name: {1} , redirect_rul:{2} , expires_in: {3}s ", openId, userinfo.nickname, redirect_url, expires_in));


            //授权成功 取得用户信息 userinfo  跳转页面

            return new RedirectResult(redirect_url, true);
        }

        public ActionResult Failed()
        {
            ViewBag.message = "OAuth失败，您拒绝了授权申请或者公众好号没有此权限.";
            return View();
        }

        public ActionResult Success()
        {
            ViewBag.message = "微信OAuth成功 .";
            return View();
        }


    }
}

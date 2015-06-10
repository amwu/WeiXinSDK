using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Deepleo.Web.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/

        public ActionResult Index()
        {
            ViewBag.Useragent = Request.Browser.IsMobileDevice.ToString();
            ViewBag.Greeting = WeixinConfig.TokenHelper.GetToken();
            return View();
            //return Content("参数错误！");
        }

    }
}

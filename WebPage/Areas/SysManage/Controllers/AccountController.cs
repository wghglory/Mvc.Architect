using Common;
using Common.JsonHelper;
using Service.IService;
using System;
using System.Web.Mvc;

namespace WebPage.Areas.SysManage.Controllers
{
    public class AccountController : Controller
    {
        #region 声明容器
        /// <summary>
        /// 用户管理
        /// </summary>
        IUserManage UserManage { get; set; }
        /// <summary>
        /// 日志记录
        /// </summary>
        Common.Log4NetHelper.IExtLog log = Common.Log4NetHelper.ExtLogManager.GetLogger("dblog");
        #endregion

        #region 基本视图
        public ActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// 登录验证
        /// </summary>
        [ValidateAntiForgeryToken]
        public ActionResult Login(Domain.SYS_USER item)
        {
            var json = new JsonHelper() { Msg = "登录成功", Status = "n" };
            try
            {
                //获取表单验证码
                string code = Request.Form["code"];
                if (Session["gif"] != null)
                {
                    if (!string.IsNullOrEmpty(code) && code.ToLower() == Session["gif"].ToString().ToLower())
                    {
                        //调用登录验证接口 返回用户实体类
                        var user = UserManage.UserLogin(item.ACCOUNT.Trim(), item.PASSWORD.Trim());
                        if (user != null)
                        {
                            //是否锁定
                            if (user.ISCANLOGIN == 1)
                            {
                                json.Msg = "用户已锁定，禁止登录，请联系管理员进行解锁";
                                log.Warn(Utils.GetIP(), item.ACCOUNT, Request.Url.ToString(), "Login", "系统登录，登录结果：" + json.Msg);
                                return Json(json);
                            }

                            var account = this.UserManage.GetAccountByUser(user);
                            //写入Session 当前登录用户
                            SessionHelper.SetSession("CurrentUser", account);
                            //记录用户信息到Cookies
                            string cookieValue = "{\"id\":\"" + account.Id + "\",\"username\":\"" + account.LogName + "\",\"password\":\"" + account.PassWord + "\",\"ToKen\":\"" + Session.SessionID + "\"}";

                            CookieHelper.SetCookie("cookie_rememberme", new Common.CryptHelper.AESCrypt().Encrypt(cookieValue), null);

                            //更新用户本次登录IP
                            user.LastLoginIP = Utils.GetIP();
                            UserManage.Update(user);


                            json.Status = "y";
                            json.ReUrl = "/Sys/Home/Index";
                            log.Info(Utils.GetIP(), item.ACCOUNT, Request.Url.ToString(), "Login", "系统登录，登录结果：" + json.Msg);

                        }
                        else
                        {
                            json.Msg = "用户名或密码不正确";
                            log.Error(Utils.GetIP(), item.ACCOUNT, Request.Url.ToString(), "Login", "系统登录，登录结果：" + json.Msg);
                        }

                    }
                    else
                    {
                        json.Msg = "验证码不正确";
                        log.Error(Utils.GetIP(), item.ACCOUNT, Request.Url.ToString(), "Login", "系统登录，登录结果：" + json.Msg);
                    }

                }
                else
                {
                    json.Msg = "验证码已过期，请刷新验证码";
                    log.Error(Utils.GetIP(), item.ACCOUNT, Request.Url.ToString(), "Login", "系统登录，登录结果：" + json.Msg);
                }


            }
            catch (Exception e)
            {
                json.Msg = e.Message;
                log.Error(Utils.GetIP(), item.ACCOUNT, Request.Url.ToString(), "Login", "系统登录，登录结果：" + json.Msg);
            }
            return Json(json, JsonRequestBehavior.AllowGet);
        }
        #endregion

        #region 帮助方法
        /// <summary>
        /// 验证码
        /// </summary>
        public FileContentResult ValidateCode()
        {
            string code = "";
            System.IO.MemoryStream ms = new Captcha().Create(out code);
            Session["gif"] = code;//验证码存储在Session中，供验证。  
            Response.ClearContent();//清空输出流 
            return File(ms.ToArray(), @"image/png");
        }
        #endregion
    }
}
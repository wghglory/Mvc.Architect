using Service.IService;
using System.Linq;
using Domain;
using System;
using Common.JsonHelper;
using System.Collections.Generic;

namespace Service.ServiceImp
{
    public class UserManage : RepositoryBase<SYS_USER>, IUserManage
    {
        IDepartmentManage DepartmentManage { get; set; }
        IUserDepartmentManage UserDepartmentManage { get; set; }
        IUserInfoManage UserInfoManage { get; set; }
        IUserRoleManage UserRoleManage { get; set; }
        IUserPermissionManage UserPermissionManage { get; set; }
        IPostUserManage PostUserManage { get; set; }
        IPermissionManage PermissionManage { get; set; }

        /// <summary>
        /// 管理用户登录验证
        /// </summary>
        /// <param name="userAccount">用户名</param>
        /// <param name="password">加密密码（AES）</param>
        /// <returns></returns>
        public SYS_USER UserLogin(string userAccount, string password)
        {
            var entity = this.Get(p => p.ACCOUNT == userAccount);

            //因为我们用的是AES的动态加密算法，也就是没有统一的密钥，那么两次同样字符串的加密结果是不一样的，所以这里要通过解密来匹配
            //而不能通过再次加密输入的密码来匹配
            if (entity != null && new Common.CryptHelper.AESCrypt().Decrypt(entity.PASSWORD) == password)
            {
                return entity;
            }
            return null;
        }


        /// <summary>
        /// 是否超级管理员
        /// </summary>
        public bool IsAdmin(int userId)
        {
            //通过用户ID获取角色
            SYS_USER entity = this.Get(p => p.ID == userId);
            if (entity == null) return false;
            var roles = entity.SYS_USER_ROLE.Select(p => new SYS_ROLE
            {
                ID = p.SYS_ROLE.ID
            });
            return roles.ToList().Any(item => item.ID == Common.Enums.ClsDic.DicRole["超级管理员"]);
        }

        /// <summary>
        /// 根据用户ID获取用户名
        /// </summary>
        /// <param name="Id">用户ID</param>
        /// <returns></returns>
        public string GetUserName(int Id)
        {
            var query = this.LoadAll(c => c.ID == Id);
            if (query == null || !query.Any())
            {
                return "";
            }
            return query.First().NAME;
        }

        /// <summary>
        /// 根据用户ID获取部门名称
        /// </summary>
        public string GetUserDptName(int id)
        {
            if (id <= 0)
                return "";
            var dptid = this.Get(p => p.ID == id).DPTID;
            return this.DepartmentManage.Get(p => p.ID == dptid).NAME;
        }

        /// <summary>
        /// 根据用户ID删除用户相关记录
        /// 删除原则：1、删除用户档案
        ///           2、删除用户角色关系
        ///           3、删除用户权限关系
        ///           4、删除用户岗位关系
        ///           5、删除用户部门关系
        ///           6、删除用户
        /// </summary>
        public bool Remove(int userId)
        {
            try
            {
                //档案
                if (this.UserInfoManage.IsExist(p => p.USERID == userId))
                {
                    this.UserInfoManage.Delete(p => p.USERID == userId);
                }
                //用户角色
                if (this.UserRoleManage.IsExist(p => p.FK_USERID == userId))
                {
                    this.UserRoleManage.Delete(p => p.FK_USERID == userId);
                }
                //用户权限
                if (this.UserPermissionManage.IsExist(p => p.FK_USERID == userId))
                {
                    this.UserPermissionManage.Delete(p => p.FK_USERID == userId);
                }
                //用户岗位
                if (this.PostUserManage.IsExist(p => p.FK_USERID == userId))
                {
                    this.PostUserManage.Delete(p => p.FK_USERID == userId);
                }
                //用户部门
                if (this.UserDepartmentManage.IsExist(p => p.USER_ID == userId))
                {
                    this.UserDepartmentManage.Delete(p => p.USER_ID == userId);
                }
                //用户自身
                if (this.IsExist(p => p.ID == userId))
                {
                    this.Delete(p => p.ID == userId);
                }
                return true;
            }
            catch (Exception e) { throw e.InnerException; }
        }

        /// <summary>
        /// 根据用户构造用户基本信息
        /// </summary>
        public Account GetAccountByUser(SYS_USER users)
        {
            if (users == null) return null;
            //用户授权--->注意用户的授权是包括角色权限与自身权限的
            var permission = GetPermissionByUser(users);
            //用户角色
            var role = users.SYS_USER_ROLE.Select(p => p.SYS_ROLE).ToList();
            //用户部门
            var dpt = users.SYS_USER_DEPARTMENT.Select(p => p.SYS_DEPARTMENT).ToList();
            //用户岗位
            var post = users.SYS_POST_USER.ToList();
            //用户主部门
            var dptInfo = this.DepartmentManage.Get(p => p.ID == users.DPTID);
            //用户模块
            var module = permission.Select(p => p.SYS_MODULE).ToList().Distinct(new ModuleDistinct()).ToList();
            Account account = new Account()
            {
                Id = users.ID,
                Name = users.NAME,
                LogName = users.ACCOUNT,
                PassWord = users.PASSWORD,
                IsAdmin = IsAdmin(users.ID),
                DptInfo = dptInfo,
                Dpt = dpt,
                Face_Img = users.FACE_IMG,
                Permissions = permission,
                Roles = role,
                PostUser = post,
                Modules = module
            };
            return account;
        }


        /// <summary>
        /// 从Cookie中获取用户信息
        /// </summary>
        public Account GetAccountByCookie()
        {
            var cookie = Common.CookieHelper.GetCookie("cookie_rememberme");
            if (cookie != null)
            {
                //验证json的有效性
                if (!string.IsNullOrEmpty(cookie.Value))
                {
                    //解密
                    var cookievalue = new Common.CryptHelper.AESCrypt().Decrypt(cookie.Value);
                    //是否为json
                    if (!JsonSplit.IsJson(cookievalue)) return null;
                    try
                    {
                        var jsonFormat = JsonConverter.ConvertJson(cookievalue);
                        if (jsonFormat != null)
                        {
                            var users = UserLogin(jsonFormat.username, new Common.CryptHelper.AESCrypt().Decrypt(jsonFormat.password));
                            if (users != null)
                                return GetAccountByUser(users);
                        }
                    }
                    catch { return null; }
                }
            }
            return null;
        }



        /// <summary>
        /// 根据用户信息获取用户所有的权限
        /// </summary>
        private List<SYS_PERMISSION> GetPermissionByUser(SYS_USER users)
        {
            //1、超级管理员拥有所有权限
            if (IsAdmin(users.ID))
                return PermissionManage.LoadListAll(null);
            //2、普通用户，合并当前用户权限与角色权限
            var perlist = new List<SYS_PERMISSION>();
            //2.1合并用户权限
            perlist.AddRange(users.SYS_USER_PERMISSION.Select(p => p.SYS_PERMISSION).ToList());
            //2.2合同角色权限
            ////todo:经典多对多的数据查询Linq方法
            perlist.AddRange(users.SYS_USER_ROLE.Select(p => p.SYS_ROLE.SYS_ROLE_PERMISSION.Select(c => c.SYS_PERMISSION)).SelectMany(c => c.Select(e => e)).Cast<SYS_PERMISSION>().ToList());
            //3、去重
            ////todo:通过重写IEqualityComparer<T>实现对象去重
            perlist = perlist.Distinct(new PermissionDistinct()).ToList();
            return perlist;
        }

    }


    /// <summary>
    /// 权限去重，非常重要
    /// </summary>
    public class PermissionDistinct : IEqualityComparer<SYS_PERMISSION>
    {
        public bool Equals(SYS_PERMISSION x, SYS_PERMISSION y)
        {
            return x.ID == y.ID;
        }

        public int GetHashCode(SYS_PERMISSION obj)
        {
            return obj.ToString().GetHashCode();
        }
    }


}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Service.IService
{
    /// <summary>
    /// Service模型处理接口
    /// </summary>
    public interface IModuleManage : IRepository<Domain.SYS_MODULE>
    {
        /// <summary>
        /// 获取用户权限模块集合
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="permission">用户授权集合</param>
        /// <param name="siteId">站点ID</param>
        /// <returns></returns>
        List<Domain.SYS_MODULE> GetModule(int userId, List<Domain.SYS_PERMISSION> permission, string siteId);
        /// <summary>
        /// 递归模块列表，返回按级别排序
        /// </summary>
        List<Domain.SYS_MODULE> RecursiveModule(List<Domain.SYS_MODULE> list);

        /// <summary>
        /// 批量变更当前模块下其他模块的级别
        /// </summary>
        bool MoreModifyModule(int moduleId, int levels);
    }
}
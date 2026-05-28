using SimpleNavigation.Common;
using System.Windows.Controls;

namespace SimpleNavigation.Interface
{
    public interface IPageService
    {
        /// <summary>
        /// 导航到指定页面
        /// </summary>
        /// <typeparam name="T">页面类型</typeparam>
        /// <param name="regionName">区域名称</param>
        /// <param name="parameters">页面参数</param>
        void Navigate<T>(string regionName, DialogParameters? parameters = null) where T : Page;

        /// <summary>
        /// 导航到指定页面
        /// </summary>
        /// <param name="regionName">区域名称</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameters">页面参数</param>
        void Navigate(string regionName, Type targetType, DialogParameters? parameters = null);

        /// <summary>
        /// 获取Page的父容器
        /// </summary>
        /// <param name="regionName">区域名称</param>
        /// <returns>Page的父容器</returns>
        Frame? GetRegion(string regionName);

        /// <summary>
        /// 导航返回上一页
        /// </summary>
        /// <param name="region">区域名称</param>
        void Goback(string region);
    }
}

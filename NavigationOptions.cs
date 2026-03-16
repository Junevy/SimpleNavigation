namespace SimpleNavigation
{
    /// <summary>
    /// 导航配置，可配置导航的行为，如是否允许Region重复打开同一个Page、是否保留导航历史等。
    /// </summary>
    public class NavigationOptions
    {
        public enum PageMode
        {
            Singleton,
            Transient
        }

        public enum KeepHistory
        {
            Never,
            Always
        }

        /// <summary>
        /// 是否允许Region重复打开同一个Page，与<see cref="NavigationOptions.PageMode"/>相关联。
        /// Singleton：Region仅允许导航一次；
        /// Transient：Region允许导航该Page多次。
        /// </summary>
        public PageMode AllowMulti = PageMode.Transient;

        /// <summary>
        /// 是否允许保留导航历史，影响<see cref="NavigationService.Goback(string)"/>功能。
        /// </summary>
        public KeepHistory History = KeepHistory.Never;
    }
}

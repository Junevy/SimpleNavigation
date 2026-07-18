namespace SimpleNavigation.Interface.Awares
{
    /// <summary>
    /// View 初始化完毕后，若需要初始化UI，则建议继承该接口，将UI初始化逻辑（如导航、显示内容）写到Initialize方法中
    /// </summary>
    public interface IViewInitializeAware
    {
        /// <summary>
        /// 用于表示是否已进行过初始化
        /// </summary>
        bool IsViewInitialized { get; set; }

        /// <summary>
        /// View 初始化完毕后，若需要初始化UI，则建议继承该接口，将UI初始化逻辑（如导航、显示内容）写到Initialize方法中
        /// </summary>
        void Initialize();
    }
}

using SimpleNavigation.Common.Adapters;
using System.Runtime.ExceptionServices;
using System.Windows;

namespace SimpleNavigation.Services
{
    public static class Region
    {
        private static readonly object SyncRoot = new();
        private static readonly object PublicationGate = new();
        private static readonly List<Declaration> Declarations = new();
        private static readonly List<WeakReference<Action<RegionDeclarationChange>>> Subscribers = new();
        private static long nextActivationToken;

        #region Attached property
        public static readonly DependencyProperty RegionNameProperty =
            DependencyProperty.RegisterAttached(
                "RegionName",
                typeof(string),
                typeof(Region),
                new PropertyMetadata(null, OnRegionNameChanged));

        public static string? GetRegionName(DependencyObject obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            return (string?)obj.GetValue(RegionNameProperty);
        }

        public static void SetRegionName(DependencyObject obj, string value)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            // Before set value to confirm:
            //  1) The region name is valid;
            ValidateRegionName(value);
            //  2) The container is valid (container must be FrameworkElement);
            var host = GetRequiredFrameworkElement(obj);
            //  3) The adapter has been existed (the container has been have a matching adapter);
            RegionHostAdapterResolver.GetRequired(host);
            //  4) The region name and container is valid;
            ValidateAttachedNameAvailability(host, value);

            obj.SetValue(RegionNameProperty, value);
        }

        /// <summary>
        /// 附加属性 RegionName 注册回调
        /// </summary>
        /// <param name="dependencyObject">被附加的宿主容器</param>
        /// <param name="eventArgs">事件信息</param>
        private static void OnRegionNameChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.NewValue == null)
            {
                ClearDeclaration(dependencyObject); // Region name为空，移除该宿主容器
                return;
            }

            var regionName = (string)eventArgs.NewValue;
            try
            {
                ValidateRegionName(regionName);
                var host = GetRequiredFrameworkElement(dependencyObject);
                RegionHostAdapterResolver.GetRequired(host);    // 获取匹配的 Adapter
                ApplyRegionName(host, regionName);
            }
            catch (Exception exception)
            {
                var originalFailure = ExceptionDispatchInfo.Capture(exception);

                if (!HasMatchingDeclaration(dependencyObject, regionName))
                {
                    try
                    {
                        RestoreDependencyPropertyValue(dependencyObject, eventArgs.OldValue);   // 恢复发生异常前的 Region name
                    }
                    catch
                    {
                        // Preserve the validation or registration failure that caused the rollback.
                    }
                }

                originalFailure.Throw();
                throw;
            }
        }
        #endregion

        /// <summary>
        /// 注册回调方法
        /// </summary>
        /// <param name="subscriber"></param>
        /// <exception cref="ArgumentNullException"></exception>
        internal static void Subscribe(Action<RegionDeclarationChange> subscriber)
        {
            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            lock (SyncRoot)
            {
                RemoveDeadSubscribersUnderLock();
                Subscribers.Add(new WeakReference<Action<RegionDeclarationChange>>(subscriber));
            }
        }

        internal static void Unsubscribe(Action<RegionDeclarationChange> subscriber)
        {
            if (subscriber == null)
                return;

            lock (SyncRoot)
            {
                for (var index = Subscribers.Count - 1; index >= 0; index--)
                {
                    if (!Subscribers[index].TryGetTarget(out var existingSubscriber) ||
                        existingSubscriber.Equals(subscriber))
                    {
                        Subscribers.RemoveAt(index);
                    }
                }
            }
        }

        /// <summary>
        /// 获取所有未取消注册的宿主容器
        /// </summary>
        /// <returns></returns>
        internal static IReadOnlyList<RegionDeclarationChange> GetActiveSnapshot()
        {
            lock (SyncRoot)
            {
                RemoveDeadDeclarationsUnderLock();

                var snapshot = new List<RegionDeclarationChange>();
                foreach (var declaration in Declarations)
                {
                    if (declaration.IsActive && declaration.Host.TryGetTarget(out var host))
                    {
                        snapshot.Add(CreateChange(
                            declaration,
                            host,
                            RegionDeclarationChangeKind.Add));
                    }
                }

                return snapshot;
            }
        }

        /// <summary>
        /// 添加宿主容器，并指定 Region name
        /// </summary>
        /// <param name="host">注册的宿主容器</param>
        /// <param name="regionName">Region name</param>
        private static void ApplyRegionName(FrameworkElement host, string regionName)
        {
            lock (PublicationGate)
            {
                ApplyRegionNameUnderPublicationGate(host, regionName);
            }
        }

        private static void ApplyRegionNameUnderPublicationGate(FrameworkElement host, string regionName)
        {
            Declaration? createdDeclaration = null;
            List<RegionDeclarationChange> changes;
            Action<RegionDeclarationChange>[] subscribers;

            lock (SyncRoot)
            {
                RemoveDeadDeclarationsUnderLock();
                ValidateAttachedNameAvailabilityUnderLock(host, regionName);    // 校验Region name是否合法 与 宿主容器是否重复注册

                var declaration = FindDeclarationUnderLock(host);   // 查找宿主容器实例是否存在

                // 宿主容器已注册，且其Region name与 预注册的Region name相同，不执行任何操作
                if (declaration != null && string.Equals(declaration.Name, regionName, StringComparison.Ordinal))
                    return;

                changes = new List<RegionDeclarationChange>(2);

                if (declaration == null)    // 宿主容器未注册
                {
                    // 创建新的宿主容器
                    declaration = new Declaration(
                        new WeakReference<FrameworkElement>(host),
                        regionName,
                        isActive: true,
                        GetNextActivationTokenUnderLock());
                    Declarations.Add(declaration);
                    createdDeclaration = declaration;
                }
                else // 宿主容器已存在，更新宿主容器信息
                {
                    if (declaration.IsActive)
                    {
                        changes.Add(CreateChange(
                            declaration,
                            host,
                            RegionDeclarationChangeKind.Remove));
                    }

                    declaration.Name = regionName; //更新 Region name
                    declaration.IsActive = true;
                    declaration.ActivationToken = GetNextActivationTokenUnderLock();
                }

                changes.Add(CreateChange(
                    declaration,
                    host,
                    RegionDeclarationChangeKind.Add));
                subscribers = GetLiveSubscribersUnderLock();
            }

            if (createdDeclaration != null)
            {
                try
                {
                    AttachLifecycleHandlers(host);  //  订阅Loaded 和 UnLoaded 事件
                }
                catch
                {
                    DetachLifecycleHandlers(host);  //  取消订阅Loaded 和 UnLoaded 事件

                    lock (SyncRoot)
                    {
                        Declarations.Remove(createdDeclaration);    // 移除宿主容器
                    }

                    throw;
                }
            }

            PublishChanges(changes, subscribers);
        }

        /// <summary>
        /// Remove 指定的宿主容器
        /// </summary>
        /// <param name="dependencyObject"></param>
        private static void ClearDeclaration(DependencyObject dependencyObject)
        {
            if (dependencyObject is not FrameworkElement host)
                return;

            lock (PublicationGate)
            {
                ClearDeclarationUnderPublicationGate(host);
            }
        }

        private static void ClearDeclarationUnderPublicationGate(FrameworkElement host)
        {
            Declaration? removedDeclaration;
            List<RegionDeclarationChange> changes;
            Action<RegionDeclarationChange>[] subscribers;

            lock (SyncRoot)
            {
                RemoveDeadDeclarationsUnderLock();
                removedDeclaration = FindDeclarationUnderLock(host);
                if (removedDeclaration == null)
                {
                    return;
                }

                changes = new List<RegionDeclarationChange>(1);
                if (removedDeclaration.IsActive)
                {
                    changes.Add(CreateChange(
                        removedDeclaration,
                        host,
                        RegionDeclarationChangeKind.Remove));
                }

                Declarations.Remove(removedDeclaration);
                subscribers = GetLiveSubscribersUnderLock();
            }

            DetachLifecycleHandlers(host);
            PublishChanges(changes, subscribers);
        }

        #region 宿主容器Loaded 和 UnLoaded 事件
        private static void OnHostLoaded(object sender, RoutedEventArgs eventArgs)
        {
            if (sender is not FrameworkElement host)
                return;

            lock (PublicationGate)
            {
                ActivateHostUnderPublicationGate(host);
            }
        }

        private static void ActivateHostUnderPublicationGate(FrameworkElement host)
        {
            RegionDeclarationChange? change = null;
            Action<RegionDeclarationChange>[] subscribers = Array.Empty<Action<RegionDeclarationChange>>();

            lock (SyncRoot)
            {
                RemoveDeadDeclarationsUnderLock();
                var declaration = FindDeclarationUnderLock(host);
                if (declaration == null || declaration.IsActive)
                {
                    return;
                }

                declaration.IsActive = true;
                declaration.ActivationToken = GetNextActivationTokenUnderLock();
                change = CreateChange(declaration, host, RegionDeclarationChangeKind.Add);
                subscribers = GetLiveSubscribersUnderLock();
            }

            PublishChanges(new[] { change }, subscribers);
        }

        private static void OnHostUnloaded(object sender, RoutedEventArgs eventArgs)
        {
            //if (sender is not FrameworkElement host)
            //    return;

            //lock (PublicationGate)
            //{
            //    DeactivateHostUnderPublicationGate(host);
            //}
        }

        private static void DeactivateHostUnderPublicationGate(FrameworkElement host)
        {
            RegionDeclarationChange? change = null;
            Action<RegionDeclarationChange>[] subscribers = Array.Empty<Action<RegionDeclarationChange>>();

            lock (SyncRoot)
            {
                RemoveDeadDeclarationsUnderLock();
                var declaration = FindDeclarationUnderLock(host);
                if (declaration == null || !declaration.IsActive)   // 容器已失效并被移除 或 容器不存在
                    return;

                declaration.IsActive = false;
                change = CreateChange(declaration, host, RegionDeclarationChangeKind.Remove);   // 创建移除容器事件
                subscribers = GetLiveSubscribersUnderLock();
            }

            PublishChanges(new[] { change }, subscribers);  // 发布事件
        }
        #endregion

        /// <summary>
        /// 订阅宿主容器Loaded 和 UnLoaded 事件
        /// </summary>
        /// <param name="host"></param>
        private static void AttachLifecycleHandlers(FrameworkElement host)
        {
            host.Loaded += OnHostLoaded;
            host.Unloaded += OnHostUnloaded;
        }

        /// <summary>
        /// 取消订阅宿主容器Loaded 和 UnLoaded 事件
        /// </summary>
        /// <param name="host"></param>
        private static void DetachLifecycleHandlers(FrameworkElement host)
        {
            host.Loaded -= OnHostLoaded;
            host.Unloaded -= OnHostUnloaded;
        }

        private static void ValidateAttachedNameAvailability(FrameworkElement host, string regionName)
        {
            lock (SyncRoot)
            {
                RemoveDeadDeclarationsUnderLock();
                ValidateAttachedNameAvailabilityUnderLock(host, regionName);
            }
        }

        /// <summary>
        /// 查询是否存在符合条件的宿主容器
        /// </summary>
        /// <param name="dependencyObject">宿主容器对象</param>
        /// <param name="regionName">Region name</param>
        /// <returns>是否存在符合条件的宿主容器</returns>
        private static bool HasMatchingDeclaration(DependencyObject dependencyObject, string regionName)
        {
            if (dependencyObject is not FrameworkElement host)
                return false;

            lock (SyncRoot)
            {
                var declaration = FindDeclarationUnderLock(host);
                return declaration != null &&
                    string.Equals(declaration.Name, regionName, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// 在注册 Region name 发生异常时，回滚 Region name
        /// </summary>
        /// <param name="dependencyObject">预被注册的宿主容器</param>
        /// <param name="oldValue">发生异常前的Region name</param>
        private static void RestoreDependencyPropertyValue(DependencyObject dependencyObject, object? oldValue)
        {
            if (oldValue == null || ReferenceEquals(oldValue, DependencyProperty.UnsetValue))
            {
                dependencyObject.ClearValue(RegionNameProperty);
                return;
            }

            dependencyObject.SetValue(RegionNameProperty, oldValue);
        }

        /// <summary>
        /// 检查 Region name 与 预注册的宿主容器是否重复注册
        /// </summary>
        /// <param name="host">宿主容器</param>
        /// <param name="regionName">Region name</param>
        /// <exception cref="InvalidOperationException">宿主容器重复注册或Region name已存在</exception>
        private static void ValidateAttachedNameAvailabilityUnderLock(FrameworkElement host, string regionName)
        {
            foreach (var declaration in Declarations)
            {
                if (!string.Equals(declaration.Name, regionName, StringComparison.Ordinal) 
                    || !declaration.Host.TryGetTarget(out var existingHost) 
                    || ReferenceEquals(existingHost, host))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    $"Region '{regionName}' is already declared on another host.");
            }
        }

        /// <summary>
        /// 将附加对象转换为 FrameworkElement
        /// </summary>
        /// <param name="obj">需要转换的附加对象</param>
        /// <returns>转为为 FrameworkElement 的附加对象</returns>
        /// <exception cref="ArgumentException">转换失败抛出异常</exception>
        private static FrameworkElement GetRequiredFrameworkElement(DependencyObject obj)
        {
            if (obj is FrameworkElement host)
                return host;

            throw new ArgumentException(
                $"Region host type '{obj.GetType().FullName}' is not a FrameworkElement.",
                nameof(obj));
        }

        /// <summary>
        /// 校验 Region name 是否合法
        /// </summary>
        private static void ValidateRegionName(string regionName)
        {
            if (string.IsNullOrWhiteSpace(regionName))
            {
                throw new ArgumentException(
                    "Region name cannot be null, empty, or whitespace.",
                    nameof(regionName));
            }
        }

        /// <summary>
        /// 循环查找当前宿主容器是否已经存在并返回
        /// 当前程序集中存在 Content 宿主容器集 和 Page宿主容器集，他们互相独立，且Region name 可相同
        /// </summary>
        /// <param name="host">预被注册为宿主容器的控件（元素）</param>
        /// <returns>返回已被注册为宿主容器的控件，如果未被注册，则返回 null</returns>
        private static Declaration? FindDeclarationUnderLock(FrameworkElement host)
        {
            foreach (var declaration in Declarations)
            {
                if (declaration.Host.TryGetTarget(out var existingHost) && ReferenceEquals(existingHost, host))
                    return declaration;
            }

            return null;
        }

        /// <summary>
        /// 移除失效的宿主容器
        /// </summary>
        private static void RemoveDeadDeclarationsUnderLock()
        {
            for (var index = Declarations.Count - 1; index >= 0; index--)
            {
                if (!Declarations[index].Host.TryGetTarget(out _))
                {
                    Declarations.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// 移除失效的订阅者
        /// </summary>
        private static void RemoveDeadSubscribersUnderLock()
        {
            for (var index = Subscribers.Count - 1; index >= 0; index--)
            {
                if (!Subscribers[index].TryGetTarget(out _))
                {
                    Subscribers.RemoveAt(index);
                }
            }
        }

        /// <summary>
        /// 获取活跃（未取消注册）的订阅者
        /// </summary>
        /// <returns></returns>
        private static Action<RegionDeclarationChange>[] GetLiveSubscribersUnderLock()
        {
            var liveSubscribers = new List<Action<RegionDeclarationChange>>(Subscribers.Count);

            for (var index = 0; index < Subscribers.Count;)
            {
                if (Subscribers[index].TryGetTarget(out var subscriber))
                {
                    liveSubscribers.Add(subscriber);
                    index++;
                }
                else
                {
                    Subscribers.RemoveAt(index);
                }
            }

            return liveSubscribers.ToArray();
        }

        /// <summary>
        /// 生成下一个宿主容器的 Token（Id）
        /// </summary>
        /// <returns>宿主容器Id</returns>
        private static long GetNextActivationTokenUnderLock() => ++nextActivationToken;

        private static RegionDeclarationChange CreateChange(
            Declaration declaration,
            FrameworkElement host,
            RegionDeclarationChangeKind kind)
        {
            return new RegionDeclarationChange(
                declaration.Name,
                host,
                declaration.ActivationToken,
                kind);
        }

        private static void PublishChanges(
            IEnumerable<RegionDeclarationChange> changes,
            IReadOnlyList<Action<RegionDeclarationChange>> subscribers)
        {
            ExceptionDispatchInfo? firstFailure = null;

            foreach (var change in changes)
            {
                foreach (var subscriber in subscribers)
                {
                    try
                    {
                        subscriber(change);
                    }
                    catch (Exception exception)
                    {
                        firstFailure ??= ExceptionDispatchInfo.Capture(exception);
                    }
                }
            }

            firstFailure?.Throw();
        }

        /// <summary>
        /// 宿主容器对象
        /// </summary>
        private sealed class Declaration
        {
            public Declaration(WeakReference<FrameworkElement> host, string name, bool isActive, long activationToken)
            {
                Host = host;
                Name = name;
                IsActive = isActive;
                ActivationToken = activationToken;
            }

            public WeakReference<FrameworkElement> Host { get; }

            public string Name { get; set; }

            public bool IsActive { get; set; }

            public long ActivationToken { get; set; }
        }
    }

    /// <summary>
    /// Region 发生变化时的类型
    /// </summary>
    internal enum RegionDeclarationChangeKind
    {
        Add,
        Remove,
    }

    /// <summary>
    /// Region发生变化时的对象
    /// </summary>
    internal sealed class RegionDeclarationChange
    {
        public RegionDeclarationChange(
            string name,
            FrameworkElement host,
            long activationToken,
            RegionDeclarationChangeKind kind)
        {
            Name = name;
            Host = host;
            ActivationToken = activationToken;
            Kind = kind;
        }

        public string Name { get; }

        public FrameworkElement Host { get; }

        public long ActivationToken { get; }

        public RegionDeclarationChangeKind Kind { get; }
    }
}



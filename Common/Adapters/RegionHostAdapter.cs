using SimpleNavigation.Interface.Adapters;
using System.Windows;

namespace SimpleNavigation.Common.Adapters;

/// <summary>
/// 区域宿主可接受的内容类型
/// </summary>
internal enum RegionHostKind
{
    Page,
    Content,
}

/// <summary>
/// 获取适配导航元素的 Adapter
/// </summary>
internal static class RegionHostAdapterResolver
{
    private static readonly IRegionHostAdapter[] HostAdapters =
    {
        new FrameRegionAdapter(),
        new ContentControlRegionAdapter(),
    };

    /// <summary>
    /// 根据 附加对象的类型，筛选相匹配的 Adapter
    /// </summary>
    /// <param name="attachedRegion">附加对象</param>
    /// <returns>相匹配的 Adapter <see cref="IRegionHostAdapter"/></returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static IRegionHostAdapter GetRequired(FrameworkElement attachedRegion)
    {
        if (attachedRegion == null)
            throw new ArgumentNullException(nameof(attachedRegion));

        foreach (var adapter in HostAdapters)
        {
            if (adapter.CanHandle(attachedRegion))
                return adapter;
        }

        var regionType = attachedRegion.GetType();
        throw new ArgumentException(
            $"Region host type '{regionType.FullName}' is not supported.",
            nameof(attachedRegion));
    }
}

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SimpleNavigation.Common;
using SimpleNavigation.Tests.TestInfrastructure;

namespace SimpleNavigation.Tests;

public sealed class RegionManagerTests
{
    [Fact]
    public void RegisterRegion_ContentHost_RoundTripsAndUnregistersExactInstance()
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();
            var region = new ContentControl();

            manager.RegisterRegion("Main", region);

            Assert.Same(region, manager.GetRegion("Main"));
            Assert.Same(region, manager.GetRegion<ContentControl>("Main"));
            Assert.Null(manager.GetRegion<Frame>("Main"));
            Assert.True(manager.UnregisterRegion("Main", region));
            Assert.Null(manager.GetRegion("Main"));
        });
    }

    [Fact]
    public void RegisterRegion_FrameHost_RoundTripsTypedAndUntyped()
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();
            var region = new Frame();

            manager.RegisterRegion("Pages", region);

            Assert.Same(region, manager.GetRegion("Pages"));
            Assert.Same(region, manager.GetRegion<Frame>("Pages"));
        });
    }

    [Fact]
    public void RegisterRegion_SameHostRepeated_IsIdempotent()
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();
            var region = new ContentControl();

            manager.RegisterRegion("Main", region);
            manager.RegisterRegion("Main", region);

            Assert.Same(region, manager.GetRegion("Main"));
        });
    }

    [Fact]
    public void RegisterRegion_DifferentLiveHostForSameName_Throws()
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();
            var first = new ContentControl();
            var second = new ContentControl();
            manager.RegisterRegion("Main", first);

            var exception = Assert.Throws<InvalidOperationException>(
                () => manager.RegisterRegion("Main", second));

            Assert.Contains("Main", exception.Message);
            GC.KeepAlive(first);
        });
    }

    [Fact]
    public void UnregisterRegion_WrongHost_ReturnsFalseAndPreservesOwner()
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();
            var owner = new ContentControl();
            var other = new ContentControl();
            manager.RegisterRegion("Main", owner);

            Assert.False(manager.UnregisterRegion("Main", other));
            Assert.Same(owner, manager.GetRegion("Main"));
        });
    }

    [Fact]
    public void RegisterRegion_UnsupportedHost_ThrowsWithHostType()
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();

            var exception = Assert.Throws<ArgumentException>(
                () => manager.RegisterRegion("Main", new Grid()));

            Assert.Contains(typeof(Grid).FullName!, exception.Message);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterRegion_InvalidName_Throws(string? regionName)
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();

            Assert.Throws<ArgumentException>(
                () => manager.RegisterRegion(regionName!, new ContentControl()));
        });
    }

    [Fact]
    public void RegisterRegion_NullHost_Throws()
    {
        var manager = new RegionManager();

        var exception = Assert.Throws<ArgumentNullException>(
            () => manager.RegisterRegion("Main", null!));

        Assert.Equal("region", exception.ParamName);
    }

    [Fact]
    public void RegionManager_DoesNotKeepAbandonedHostAlive()
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();
            var weakRegion = RegisterAbandonedRegion(manager, "Main");

            ForceGarbageCollection();

            Assert.False(weakRegion.IsAlive);
            Assert.Null(manager.GetRegion("Main"));
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RegisterAbandonedRegion(RegionManager manager, string regionName)
    {
        var region = new ContentControl();
        manager.RegisterRegion(regionName, region);
        return new WeakReference(region);
    }

    private static void ForceGarbageCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}

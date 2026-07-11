using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SimpleNavigation.Common;
using SimpleNavigation.Services;
using SimpleNavigation.Tests.TestInfrastructure;

namespace SimpleNavigation.Tests;

public sealed class RegionTests
{
    [Fact]
    public void SetRegionName_BeforeManagerCreation_ReplaysActiveFrame()
    {
        StaTest.Run(() =>
        {
            var host = new Frame();

            try
            {
                Region.SetRegionName(host, "Pages");

                using var manager = new RegionManager();

                Assert.Same(host, manager.GetRegion("Pages"));
            }
            finally
            {
                ClearRegionName(host);
            }
        });
    }

    [Fact]
    public void SetRegionName_ContentControl_RegistersWithManager()
    {
        StaTest.Run(() =>
        {
            var host = new ContentControl();

            try
            {
                using var manager = new RegionManager();

                Region.SetRegionName(host, "Main");

                Assert.Same(host, manager.GetRegion("Main"));
            }
            finally
            {
                ClearRegionName(host);
            }
        });
    }

    [Fact]
    public void SetRegionName_UnsupportedFrameworkElement_ThrowsWithHostType()
    {
        StaTest.Run(() =>
        {
            var host = new Grid();

            var exception = Assert.Throws<ArgumentException>(
                () => Region.SetRegionName(host, "Main"));

            Assert.Contains(typeof(Grid).FullName!, exception.Message);
        });
    }

    [Fact]
    public void SetRegionName_NonFrameworkElement_ThrowsWithHostType()
    {
        StaTest.Run(() =>
        {
            var host = new DependencyObject();

            var exception = Assert.Throws<ArgumentException>(
                () => Region.SetRegionName(host, "NonElement"));

            Assert.Contains(typeof(DependencyObject).FullName!, exception.Message);
        });
    }

    [Fact]
    public void SetRegionName_Rename_RemovesOldNameAndAddsNewName()
    {
        StaTest.Run(() =>
        {
            var host = new ContentControl();

            try
            {
                using var manager = new RegionManager();
                Region.SetRegionName(host, "Old");

                Region.SetRegionName(host, "New");

                Assert.Null(manager.GetRegion("Old"));
                Assert.Same(host, manager.GetRegion("New"));
            }
            finally
            {
                ClearRegionName(host);
            }
        });
    }

    [Fact]
    public void SetRegionName_RejectedRename_PreservesOldDeclaration()
    {
        StaTest.Run(() =>
        {
            var first = new ContentControl();
            var second = new ContentControl();

            try
            {
                using var manager = new RegionManager();
                Region.SetRegionName(first, "Preserved");
                Region.SetRegionName(second, "Occupied");

                Assert.Throws<InvalidOperationException>(
                    () => Region.SetRegionName(first, "Occupied"));

                Assert.Equal("Preserved", Region.GetRegionName(first));
                Assert.Same(first, manager.GetRegion("Preserved"));
                Assert.Same(second, manager.GetRegion("Occupied"));
            }
            finally
            {
                ClearRegionName(first);
                ClearRegionName(second);
            }
        });
    }

    [Fact]
    public void ClearValue_ActiveDeclaration_RemovesRegistration()
    {
        StaTest.Run(() =>
        {
            var host = new ContentControl();

            try
            {
                using var manager = new RegionManager();
                Region.SetRegionName(host, "Main");

                host.ClearValue(Region.RegionNameProperty);

                Assert.Null(Region.GetRegionName(host));
                Assert.Null(manager.GetRegion("Main"));
            }
            finally
            {
                ClearRegionName(host);
            }
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetRegionName_InvalidName_Throws(string? regionName)
    {
        StaTest.Run(() =>
        {
            var host = new ContentControl();

            Assert.Throws<ArgumentException>(
                () => Region.SetRegionName(host, regionName!));
        });
    }

    [Fact]
    public void UnloadedAndLoaded_AttachedOnlyDeclaration_RemovesThenRestoresHost()
    {
        StaTest.Run(() =>
        {
            var host = new ContentControl();

            try
            {
                using var manager = new RegionManager();
                Region.SetRegionName(host, "Main");

                RaiseLifecycleEvent(host, FrameworkElement.UnloadedEvent);
                Assert.Null(manager.GetRegion("Main"));

                RaiseLifecycleEvent(host, FrameworkElement.LoadedEvent);
                Assert.Same(host, manager.GetRegion("Main"));
            }
            finally
            {
                ClearRegionName(host);
            }
        });
    }

    [Fact]
    public void ClearValue_WhileUnloaded_PreventsLaterReactivation()
    {
        StaTest.Run(() =>
        {
            var host = new ContentControl();

            try
            {
                using var manager = new RegionManager();
                Region.SetRegionName(host, "UnloadedClear");
                RaiseLifecycleEvent(host, FrameworkElement.UnloadedEvent);

                host.ClearValue(Region.RegionNameProperty);
                RaiseLifecycleEvent(host, FrameworkElement.LoadedEvent);

                Assert.Null(manager.GetRegion("UnloadedClear"));
            }
            finally
            {
                ClearRegionName(host);
            }
        });
    }

    [Fact]
    public void SetRegionName_DuplicateAttachedNameOnLiveHosts_ThrowsWithoutManager()
    {
        StaTest.Run(() =>
        {
            var first = new ContentControl();
            var second = new ContentControl();

            try
            {
                Region.SetRegionName(first, "Main");

                var exception = Assert.Throws<InvalidOperationException>(
                    () => Region.SetRegionName(second, "Main"));

                Assert.Contains("Main", exception.Message);
            }
            finally
            {
                ClearRegionName(first);
                ClearRegionName(second);
            }
        });
    }

    [Fact]
    public void ProgrammaticAndAttachedOwnership_AreRemovedIndependently()
    {
        StaTest.Run(() =>
        {
            var host = new ContentControl();

            try
            {
                using var manager = new RegionManager();
                manager.RegisterRegion("Main", host);
                Region.SetRegionName(host, "Main");

                RaiseLifecycleEvent(host, FrameworkElement.UnloadedEvent);
                Assert.Same(host, manager.GetRegion("Main"));

                Assert.True(manager.UnregisterRegion("Main", host));
                Assert.Null(manager.GetRegion("Main"));

                RaiseLifecycleEvent(host, FrameworkElement.LoadedEvent);
                Assert.Same(host, manager.GetRegion("Main"));

                host.ClearValue(Region.RegionNameProperty);
                Assert.Null(manager.GetRegion("Main"));
            }
            finally
            {
                ClearRegionName(host);
            }
        });
    }

    [Fact]
    public void UnregisterRegion_AttachedOnlyOwnership_ReturnsFalseAndPreservesHost()
    {
        StaTest.Run(() =>
        {
            var host = new ContentControl();

            try
            {
                using var manager = new RegionManager();
                Region.SetRegionName(host, "AttachedOnly");

                Assert.False(manager.UnregisterRegion("AttachedOnly", host));
                Assert.Same(host, manager.GetRegion("AttachedOnly"));
            }
            finally
            {
                ClearRegionName(host);
            }
        });
    }

    [Fact]
    public void DeclarationFailure_InOneManager_StillPublishesToOtherManagers()
    {
        StaTest.Run(() =>
        {
            var blocker = new ContentControl();
            var declaredHost = new ContentControl();

            try
            {
                using var throwingManager = new RegionManager();
                using var receivingManager = new RegionManager();
                throwingManager.RegisterRegion("Fanout", blocker);

                var exception = Assert.Throws<InvalidOperationException>(
                    () => Region.SetRegionName(declaredHost, "Fanout"));

                Assert.Contains("Fanout", exception.Message);
                Assert.Same(blocker, throwingManager.GetRegion("Fanout"));
                Assert.Same(declaredHost, receivingManager.GetRegion("Fanout"));
            }
            finally
            {
                ClearRegionName(declaredHost);
            }
        });
    }

    [Fact]
    public void DeclarationCatalog_DoesNotKeepAbandonedHostAlive()
    {
        StaTest.Run(() =>
        {
            var weakHost = DeclareAbandonedRegion("Abandoned");

            ForceGarbageCollection(weakHost);

            Assert.False(weakHost.IsAlive);
            using var manager = new RegionManager();
            Assert.Null(manager.GetRegion("Abandoned"));
        });
    }

    [Fact]
    public void ManagersCreatedAroundDeclaration_ReceiveItAndDisposeUnsubscribesOne()
    {
        StaTest.Run(() =>
        {
            var firstHost = new ContentControl();
            var secondHost = new Frame();
            var earlyManager = new RegionManager();

            try
            {
                Region.SetRegionName(firstHost, "Main");
                using var lateManager = new RegionManager();

                Assert.Same(firstHost, earlyManager.GetRegion("Main"));
                Assert.Same(firstHost, lateManager.GetRegion("Main"));

                earlyManager.Dispose();
                Region.SetRegionName(secondHost, "Pages");

                Assert.Same(secondHost, lateManager.GetRegion("Pages"));
                Assert.Throws<ObjectDisposedException>(() => earlyManager.GetRegion("Pages"));
            }
            finally
            {
                earlyManager.Dispose();
                ClearRegionName(firstHost);
                ClearRegionName(secondHost);
            }
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference DeclareAbandonedRegion(string regionName)
    {
        var host = new ContentControl();
        Region.SetRegionName(host, regionName);
        return new WeakReference(host);
    }

    private static void ClearRegionName(FrameworkElement host)
    {
        if (host.ReadLocalValue(Region.RegionNameProperty) != DependencyProperty.UnsetValue)
        {
            host.ClearValue(Region.RegionNameProperty);
        }
    }

    private static void RaiseLifecycleEvent(FrameworkElement host, RoutedEvent routedEvent)
    {
        host.RaiseEvent(new RoutedEventArgs(routedEvent, host));
    }

    private static void ForceGarbageCollection(WeakReference weakReference)
    {
        const int attemptLimit = 10;

        for (var attempt = 0; attempt < attemptLimit && weakReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}

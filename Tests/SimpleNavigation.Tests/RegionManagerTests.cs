using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SimpleNavigation.Common.Managers;
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
    public void RegisterRegion_NamesAreOrdinalAndCaseSensitive()
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();
            var upperCaseRegion = new ContentControl();
            var lowerCaseRegion = new ContentControl();

            manager.RegisterRegion("Main", upperCaseRegion);
            manager.RegisterRegion("main", lowerCaseRegion);

            Assert.Same(upperCaseRegion, manager.GetRegion("Main"));
            Assert.Same(lowerCaseRegion, manager.GetRegion("main"));
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
    public void RegisterRegion_ConcurrentDifferentHosts_AllowsExactlyOneOwner()
    {
        StaTest.Run(() =>
        {
            const int workerCount = 32;
            const int roundCount = 20;
            const string regionName = "Concurrent";

            var hosts = new ContentControl[workerCount];
            for (var index = 0; index < hosts.Length; index++)
            {
                hosts[index] = new ContentControl();
            }

            var managers = new RegionManager[roundCount];
            for (var round = 0; round < managers.Length; round++)
            {
                managers[round] = new RegionManager();
            }

            var outcomes = new Exception?[roundCount, workerCount];
            var workers = new Thread[workerCount];
            using var barrier = new Barrier(workerCount + 1);

            for (var index = 0; index < workers.Length; index++)
            {
                var workerIndex = index;
                workers[index] = new Thread(() =>
                {
                    for (var round = 0; round < roundCount; round++)
                    {
                        barrier.SignalAndWait();

                        try
                        {
                            managers[round].RegisterRegion(regionName, hosts[workerIndex]);
                        }
                        catch (Exception exception)
                        {
                            outcomes[round, workerIndex] = exception;
                        }

                        barrier.SignalAndWait();
                    }
                })
                {
                    IsBackground = true,
                };
                workers[index].Start();
            }

            for (var round = 0; round < roundCount; round++)
            {
                barrier.SignalAndWait();
                barrier.SignalAndWait();
            }

            foreach (var worker in workers)
            {
                Assert.True(worker.Join(TimeSpan.FromSeconds(5)), "A registration worker did not finish.");
            }

            for (var round = 0; round < roundCount; round++)
            {
                var successCount = 0;
                var successIndex = -1;

                for (var worker = 0; worker < workerCount; worker++)
                {
                    var outcome = outcomes[round, worker];
                    if (outcome == null)
                    {
                        successCount++;
                        successIndex = worker;
                    }
                    else
                    {
                        Assert.IsType<InvalidOperationException>(outcome);
                    }
                }

                Assert.True(
                    successCount == 1,
                    $"Round {round} accepted {successCount} owners instead of exactly one.");
                Assert.Same(hosts[successIndex], managers[round].GetRegion(regionName));
            }

            GC.KeepAlive(hosts);
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetRegion_InvalidName_Throws(string? regionName)
    {
        var manager = new RegionManager();

        Assert.Throws<ArgumentException>(() => manager.GetRegion(regionName!));
        Assert.Throws<ArgumentException>(() => manager.GetRegion<ContentControl>(regionName!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnregisterRegion_InvalidName_Throws(string? regionName)
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();

            Assert.Throws<ArgumentException>(
                () => manager.UnregisterRegion(regionName!, new ContentControl()));
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
    public void UnregisterRegion_NullHost_Throws()
    {
        var manager = new RegionManager();

        var exception = Assert.Throws<ArgumentNullException>(
            () => manager.UnregisterRegion("Main", null!));

        Assert.Equal("region", exception.ParamName);
    }

    [Fact]
    public void RegionManager_DoesNotKeepAbandonedHostAlive()
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();
            var weakRegion = RegisterAbandonedRegion(manager, "Main");

            ForceGarbageCollection(weakRegion);

            Assert.False(weakRegion.IsAlive);
            Assert.Null(manager.GetRegion("Main"));
        });
    }

    [Fact]
    public void RegisterRegion_CollectedOwner_CanBeReplacedWithoutLookup()
    {
        StaTest.Run(() =>
        {
            var manager = new RegionManager();
            var weakRegion = RegisterAbandonedRegion(manager, "Main");
            ForceGarbageCollection(weakRegion);
            Assert.False(weakRegion.IsAlive);

            var replacement = new ContentControl();
            manager.RegisterRegion("Main", replacement);

            Assert.Same(replacement, manager.GetRegion("Main"));
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference RegisterAbandonedRegion(RegionManager manager, string regionName)
    {
        var region = new ContentControl();
        manager.RegisterRegion(regionName, region);
        return new WeakReference(region);
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

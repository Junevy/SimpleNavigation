using System.Runtime.CompilerServices;
using System.Reflection;
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
    public void DirectSetValue_DuplicateName_RollsBackLoserDependencyProperty()
    {
        StaTest.Run(() =>
        {
            var first = new ContentControl();
            var second = new ContentControl();

            try
            {
                using var manager = new RegionManager();
                first.SetValue(Region.RegionNameProperty, "DirectTaken");

                var exception = Assert.Throws<InvalidOperationException>(
                    () => second.SetValue(Region.RegionNameProperty, "DirectTaken"));

                Assert.Contains("DirectTaken", exception.Message);
                Assert.Null(Region.GetRegionName(second));
                Assert.Same(first, manager.GetRegion("DirectTaken"));
            }
            finally
            {
                ClearRegionName(first);
                ClearRegionName(second);
            }
        });
    }

    [Fact]
    public void DirectSetValue_RejectedRename_RestoresOldValueAndOwnership()
    {
        StaTest.Run(() =>
        {
            var renamedHost = new ContentControl();
            var occupiedHost = new ContentControl();

            try
            {
                using var manager = new RegionManager();
                renamedHost.SetValue(Region.RegionNameProperty, "DirectOld");
                occupiedHost.SetValue(Region.RegionNameProperty, "DirectOccupied");

                Assert.Throws<InvalidOperationException>(
                    () => renamedHost.SetValue(Region.RegionNameProperty, "DirectOccupied"));

                Assert.Equal("DirectOld", Region.GetRegionName(renamedHost));
                Assert.Same(renamedHost, manager.GetRegion("DirectOld"));
                Assert.Same(occupiedHost, manager.GetRegion("DirectOccupied"));
            }
            finally
            {
                ClearRegionName(renamedHost);
                ClearRegionName(occupiedHost);
            }
        });
    }

    [Fact]
    public void DirectSetValue_ConcurrentSameName_OneWinsAndLoserRollsBack()
    {
        StaTest.Run(() =>
        {
            const string regionName = "DirectRace";
            var hosts = new ContentControl?[2];
            var outcomes = new Exception?[2];
            var effectiveNames = new string?[2];
            var unexpectedFailures = new Exception?[2];
            var workers = new Thread[2];
            using var hostsCreated = new CountdownEvent(2);
            using var start = new ManualResetEventSlim(false);
            using var setCompleted = new CountdownEvent(2);
            using var releaseCleanup = new ManualResetEventSlim(false);

            for (var index = 0; index < workers.Length; index++)
            {
                var workerIndex = index;
                workers[index] = new Thread(() =>
                {
                    ContentControl? host = null;

                    try
                    {
                        host = new ContentControl();
                        hosts[workerIndex] = host;
                        hostsCreated.Signal();
                        start.Wait();

                        try
                        {
                            host.SetValue(Region.RegionNameProperty, regionName);
                        }
                        catch (Exception exception)
                        {
                            outcomes[workerIndex] = exception;
                        }

                        effectiveNames[workerIndex] = Region.GetRegionName(host);
                    }
                    catch (Exception exception)
                    {
                        unexpectedFailures[workerIndex] = exception;
                    }
                    finally
                    {
                        setCompleted.Signal();
                        releaseCleanup.Wait(TimeSpan.FromSeconds(5));

                        if (host != null)
                        {
                            ClearRegionName(host);
                        }
                    }
                })
                {
                    IsBackground = true,
                };
                workers[index].SetApartmentState(ApartmentState.STA);
                workers[index].Start();
            }

            RegionManager? manager = null;

            try
            {
                Assert.True(hostsCreated.Wait(TimeSpan.FromSeconds(5)), "Region hosts were not created.");
                start.Set();
                Assert.True(setCompleted.Wait(TimeSpan.FromSeconds(5)), "Region setters did not finish.");
                Assert.All(unexpectedFailures, failure => Assert.Null(failure));

                var winnerIndex = Array.FindIndex(outcomes, outcome => outcome == null);
                var loserIndex = Array.FindIndex(outcomes, outcome => outcome != null);

                Assert.InRange(winnerIndex, 0, 1);
                Assert.InRange(loserIndex, 0, 1);
                Assert.NotEqual(winnerIndex, loserIndex);
                Assert.IsType<InvalidOperationException>(outcomes[loserIndex]);
                Assert.Equal(regionName, effectiveNames[winnerIndex]);
                Assert.Null(effectiveNames[loserIndex]);

                manager = new RegionManager();
                Assert.Same(hosts[winnerIndex], manager.GetRegion(regionName));
            }
            finally
            {
                manager?.Dispose();
                releaseCleanup.Set();

                foreach (var worker in workers)
                {
                    Assert.True(worker.Join(TimeSpan.FromSeconds(5)), "A region setter worker did not finish.");
                }
            }
        });
    }

    [Fact]
    public void DeclarationPublication_PreservesCatalogMutationOrderAcrossDispatchers()
    {
        StaTest.Run(() =>
        {
            const string regionName = "PublicationOrder";
            using var blocker = new PublicationOrderBlocker(regionName);
            var subscriber = SubscribePublicationBlocker(blocker);
            var firstHostReady = new ManualResetEventSlim(false);
            var secondHostReady = new ManualResetEventSlim(false);
            var clearFirst = new ManualResetEventSlim(false);
            var setSecond = new ManualResetEventSlim(false);
            var secondSetStarted = new ManualResetEventSlim(false);
            var firstCompleted = new ManualResetEventSlim(false);
            var secondCompleted = new ManualResetEventSlim(false);
            var releaseSecondCleanup = new ManualResetEventSlim(false);
            ContentControl? firstHost = null;
            ContentControl? secondHost = null;
            Exception? firstFailure = null;
            Exception? secondFailure = null;

            var firstWorker = new Thread(() =>
            {
                try
                {
                    firstHost = new ContentControl();
                    firstHost.SetValue(Region.RegionNameProperty, regionName);
                    firstHostReady.Set();
                    clearFirst.Wait();
                    firstHost.ClearValue(Region.RegionNameProperty);
                }
                catch (Exception exception)
                {
                    firstFailure = exception;
                }
                finally
                {
                    firstHostReady.Set();
                    firstCompleted.Set();
                }
            })
            {
                IsBackground = true,
            };
            firstWorker.SetApartmentState(ApartmentState.STA);

            var secondWorker = new Thread(() =>
            {
                try
                {
                    secondHost = new ContentControl();
                    secondHostReady.Set();
                    setSecond.Wait();
                    secondSetStarted.Set();
                    secondHost.SetValue(Region.RegionNameProperty, regionName);
                }
                catch (Exception exception)
                {
                    secondFailure = exception;
                }
                finally
                {
                    secondHostReady.Set();
                    secondSetStarted.Set();
                    secondCompleted.Set();
                    releaseSecondCleanup.Wait(TimeSpan.FromSeconds(10));

                    if (secondHost != null)
                    {
                        ClearRegionName(secondHost);
                    }
                }
            })
            {
                IsBackground = true,
            };
            secondWorker.SetApartmentState(ApartmentState.STA);

            RegionManager? manager = null;
            RegionManager? lateManager = null;
            firstWorker.Start();
            secondWorker.Start();

            try
            {
                Assert.True(firstHostReady.Wait(TimeSpan.FromSeconds(5)), "The first host was not declared.");
                Assert.True(secondHostReady.Wait(TimeSpan.FromSeconds(5)), "The second host was not created.");
                Assert.Null(firstFailure);
                manager = new RegionManager();
                Assert.Same(firstHost, manager.GetRegion(regionName));

                blocker.IsArmed = true;
                clearFirst.Set();
                Assert.True(blocker.RemoveObserved.Wait(TimeSpan.FromSeconds(5)), "Remove publication was not blocked.");

                setSecond.Set();
                Assert.True(secondSetStarted.Wait(TimeSpan.FromSeconds(5)), "The replacement setter did not start.");

                if (blocker.ReplacementAddObserved.Wait(TimeSpan.FromMilliseconds(500)))
                {
                    Assert.True(secondCompleted.Wait(TimeSpan.FromSeconds(5)), "The overtaking add did not finish.");
                }

                blocker.ReleaseRemove.Set();
                Assert.True(firstCompleted.Wait(TimeSpan.FromSeconds(5)), "The first clear did not finish.");
                Assert.True(secondCompleted.Wait(TimeSpan.FromSeconds(5)), "The replacement setter did not finish.");

                Assert.Null(firstFailure);
                Assert.Null(secondFailure);
                Assert.Same(secondHost, manager.GetRegion(regionName));
                lateManager = new RegionManager();
                Assert.Same(secondHost, lateManager.GetRegion(regionName));
            }
            finally
            {
                blocker.ReleaseRemove.Set();
                clearFirst.Set();
                setSecond.Set();
                releaseSecondCleanup.Set();
                manager?.Dispose();
                lateManager?.Dispose();
                Assert.True(firstWorker.Join(TimeSpan.FromSeconds(5)), "The first publication worker did not finish.");
                Assert.True(secondWorker.Join(TimeSpan.FromSeconds(5)), "The second publication worker did not finish.");
                UnsubscribePublicationBlocker(subscriber);
                firstHostReady.Dispose();
                secondHostReady.Dispose();
                clearFirst.Dispose();
                setSecond.Dispose();
                secondSetStarted.Dispose();
                firstCompleted.Dispose();
                secondCompleted.Dispose();
                releaseSecondCleanup.Dispose();
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
    public void RegionSubscribers_DoNotKeepUndisposedManagerAlive()
    {
        StaTest.Run(() =>
        {
            var weakManager = CreateAbandonedManager();

            ForceGarbageCollection(weakManager);

            Assert.False(weakManager.IsAlive);
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateAbandonedManager()
    {
        var manager = new RegionManager();
        return new WeakReference(manager);
    }

    private static Delegate SubscribePublicationBlocker(PublicationOrderBlocker blocker)
    {
        var changeType = typeof(Region).Assembly.GetType(
            "SimpleNavigation.Services.RegionDeclarationChange",
            throwOnError: true)!;
        var actionType = typeof(Action<>).MakeGenericType(changeType);
        var handler = typeof(PublicationOrderBlocker)
            .GetMethod(nameof(PublicationOrderBlocker.Handle), BindingFlags.Instance | BindingFlags.Public)!
            .MakeGenericMethod(changeType);
        var subscriber = Delegate.CreateDelegate(actionType, blocker, handler);
        var subscribe = typeof(Region).GetMethod("Subscribe", BindingFlags.Static | BindingFlags.NonPublic)!;
        subscribe.Invoke(null, new object[] { subscriber });
        return subscriber;
    }

    private static void UnsubscribePublicationBlocker(Delegate subscriber)
    {
        var unsubscribe = typeof(Region).GetMethod("Unsubscribe", BindingFlags.Static | BindingFlags.NonPublic)!;
        unsubscribe.Invoke(null, new object[] { subscriber });
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

    private sealed class PublicationOrderBlocker : IDisposable
    {
        private readonly string regionName;

        public PublicationOrderBlocker(string regionName)
        {
            this.regionName = regionName;
        }

        public bool IsArmed { get; set; }

        public ManualResetEventSlim RemoveObserved { get; } = new(false);

        public ManualResetEventSlim ReplacementAddObserved { get; } = new(false);

        public ManualResetEventSlim ReleaseRemove { get; } = new(false);

        public void Handle<TChange>(TChange change)
        {
            if (!IsArmed || change == null)
            {
                return;
            }

            var changeType = change.GetType();
            var name = (string?)changeType.GetProperty("Name")?.GetValue(change);
            var kind = changeType.GetProperty("Kind")?.GetValue(change)?.ToString();
            if (!string.Equals(name, regionName, StringComparison.Ordinal))
            {
                return;
            }

            if (string.Equals(kind, "Remove", StringComparison.Ordinal))
            {
                RemoveObserved.Set();
                if (!ReleaseRemove.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Timed out waiting to release declaration removal.");
                }

                return;
            }

            if (string.Equals(kind, "Add", StringComparison.Ordinal))
            {
                ReplacementAddObserved.Set();
            }
        }

        public void Dispose()
        {
            RemoveObserved.Dispose();
            ReplacementAddObserved.Dispose();
            ReleaseRemove.Dispose();
        }
    }
}

using System.Runtime.ExceptionServices;
using System.Windows;
using SimpleNavigation.Common;

namespace SimpleNavigation.Services;

public static class Region
{
    private static readonly object SyncRoot = new();
    private static readonly List<Declaration> Declarations = new();
    private static readonly List<Action<RegionDeclarationChange>> Subscribers = new();
    private static long nextActivationToken;

    public static readonly DependencyProperty RegionNameProperty =
        DependencyProperty.RegisterAttached(
            "RegionName",
            typeof(string),
            typeof(Region),
            new PropertyMetadata(null, OnRegionNameChanged));

    public static string? GetRegionName(DependencyObject obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        return (string?)obj.GetValue(RegionNameProperty);
    }

    public static void SetRegionName(DependencyObject obj, string value)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        ValidateRegionName(value);
        var host = GetRequiredFrameworkElement(obj);
        RegionHostAdapterResolver.GetRequired(host);
        ValidateAttachedNameAvailability(host, value);

        obj.SetValue(RegionNameProperty, value);
    }

    internal static void Subscribe(Action<RegionDeclarationChange> subscriber)
    {
        if (subscriber == null)
        {
            throw new ArgumentNullException(nameof(subscriber));
        }

        lock (SyncRoot)
        {
            Subscribers.Add(subscriber);
        }
    }

    internal static void Unsubscribe(Action<RegionDeclarationChange> subscriber)
    {
        if (subscriber == null)
        {
            return;
        }

        lock (SyncRoot)
        {
            Subscribers.Remove(subscriber);
        }
    }

    internal static IReadOnlyList<RegionDeclarationChange> GetActiveSnapshot()
    {
        lock (SyncRoot)
        {
            PruneDeadDeclarationsUnderLock();

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

    private static void OnRegionNameChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.NewValue == null)
        {
            ClearDeclaration(dependencyObject);
            return;
        }

        var regionName = (string)eventArgs.NewValue;
        ValidateRegionName(regionName);
        var host = GetRequiredFrameworkElement(dependencyObject);
        RegionHostAdapterResolver.GetRequired(host);

        ApplyRegionName(host, regionName);
    }

    private static void ApplyRegionName(FrameworkElement host, string regionName)
    {
        Declaration? createdDeclaration = null;
        List<RegionDeclarationChange> changes;
        Action<RegionDeclarationChange>[] subscribers;

        lock (SyncRoot)
        {
            PruneDeadDeclarationsUnderLock();
            ValidateAttachedNameAvailabilityUnderLock(host, regionName);

            var declaration = FindDeclarationUnderLock(host);
            if (declaration != null &&
                string.Equals(declaration.Name, regionName, StringComparison.Ordinal))
            {
                return;
            }

            changes = new List<RegionDeclarationChange>(2);

            if (declaration == null)
            {
                declaration = new Declaration(
                    new WeakReference<FrameworkElement>(host),
                    regionName,
                    isActive: true,
                    GetNextActivationTokenUnderLock());
                Declarations.Add(declaration);
                createdDeclaration = declaration;
            }
            else
            {
                if (declaration.IsActive)
                {
                    changes.Add(CreateChange(
                        declaration,
                        host,
                        RegionDeclarationChangeKind.Remove));
                }

                declaration.Name = regionName;
                declaration.IsActive = true;
                declaration.ActivationToken = GetNextActivationTokenUnderLock();
            }

            changes.Add(CreateChange(
                declaration,
                host,
                RegionDeclarationChangeKind.Add));
            subscribers = Subscribers.ToArray();
        }

        if (createdDeclaration != null)
        {
            try
            {
                AttachLifecycleHandlers(host);
            }
            catch
            {
                DetachLifecycleHandlers(host);

                lock (SyncRoot)
                {
                    Declarations.Remove(createdDeclaration);
                }

                throw;
            }
        }

        PublishChanges(changes, subscribers);
    }

    private static void ClearDeclaration(DependencyObject dependencyObject)
    {
        if (dependencyObject is not FrameworkElement host)
        {
            return;
        }

        Declaration? removedDeclaration;
        List<RegionDeclarationChange> changes;
        Action<RegionDeclarationChange>[] subscribers;

        lock (SyncRoot)
        {
            PruneDeadDeclarationsUnderLock();
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
            subscribers = Subscribers.ToArray();
        }

        DetachLifecycleHandlers(host);
        PublishChanges(changes, subscribers);
    }

    private static void OnHostLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not FrameworkElement host)
        {
            return;
        }

        RegionDeclarationChange? change = null;
        Action<RegionDeclarationChange>[] subscribers = Array.Empty<Action<RegionDeclarationChange>>();

        lock (SyncRoot)
        {
            PruneDeadDeclarationsUnderLock();
            var declaration = FindDeclarationUnderLock(host);
            if (declaration == null || declaration.IsActive)
            {
                return;
            }

            declaration.IsActive = true;
            declaration.ActivationToken = GetNextActivationTokenUnderLock();
            change = CreateChange(declaration, host, RegionDeclarationChangeKind.Add);
            subscribers = Subscribers.ToArray();
        }

        PublishChanges(new[] { change }, subscribers);
    }

    private static void OnHostUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not FrameworkElement host)
        {
            return;
        }

        RegionDeclarationChange? change = null;
        Action<RegionDeclarationChange>[] subscribers = Array.Empty<Action<RegionDeclarationChange>>();

        lock (SyncRoot)
        {
            PruneDeadDeclarationsUnderLock();
            var declaration = FindDeclarationUnderLock(host);
            if (declaration == null || !declaration.IsActive)
            {
                return;
            }

            declaration.IsActive = false;
            change = CreateChange(declaration, host, RegionDeclarationChangeKind.Remove);
            subscribers = Subscribers.ToArray();
        }

        PublishChanges(new[] { change }, subscribers);
    }

    private static void AttachLifecycleHandlers(FrameworkElement host)
    {
        host.Loaded += OnHostLoaded;
        host.Unloaded += OnHostUnloaded;
    }

    private static void DetachLifecycleHandlers(FrameworkElement host)
    {
        host.Loaded -= OnHostLoaded;
        host.Unloaded -= OnHostUnloaded;
    }

    private static void ValidateAttachedNameAvailability(
        FrameworkElement host,
        string regionName)
    {
        lock (SyncRoot)
        {
            PruneDeadDeclarationsUnderLock();
            ValidateAttachedNameAvailabilityUnderLock(host, regionName);
        }
    }

    private static void ValidateAttachedNameAvailabilityUnderLock(
        FrameworkElement host,
        string regionName)
    {
        foreach (var declaration in Declarations)
        {
            if (!string.Equals(declaration.Name, regionName, StringComparison.Ordinal) ||
                !declaration.Host.TryGetTarget(out var existingHost) ||
                ReferenceEquals(existingHost, host))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Region '{regionName}' is already declared on another host.");
        }
    }

    private static FrameworkElement GetRequiredFrameworkElement(DependencyObject obj)
    {
        if (obj is FrameworkElement host)
        {
            return host;
        }

        throw new ArgumentException(
            $"Region host type '{obj.GetType().FullName}' is not a FrameworkElement.",
            nameof(obj));
    }

    private static void ValidateRegionName(string regionName)
    {
        if (string.IsNullOrWhiteSpace(regionName))
        {
            throw new ArgumentException(
                "Region name cannot be null, empty, or whitespace.",
                nameof(regionName));
        }
    }

    private static Declaration? FindDeclarationUnderLock(FrameworkElement host)
    {
        foreach (var declaration in Declarations)
        {
            if (declaration.Host.TryGetTarget(out var existingHost) &&
                ReferenceEquals(existingHost, host))
            {
                return declaration;
            }
        }

        return null;
    }

    private static void PruneDeadDeclarationsUnderLock()
    {
        for (var index = Declarations.Count - 1; index >= 0; index--)
        {
            if (!Declarations[index].Host.TryGetTarget(out _))
            {
                Declarations.RemoveAt(index);
            }
        }
    }

    private static long GetNextActivationTokenUnderLock()
    {
        return ++nextActivationToken;
    }

    private static RegionDeclarationChange CreateChange(
        Declaration declaration,
        FrameworkElement host,
        RegionDeclarationChangeKind kind)
    {
        return new RegionDeclarationChange(
            declaration.Name,
            declaration.Host,
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

    private sealed class Declaration
    {
        public Declaration(
            WeakReference<FrameworkElement> host,
            string name,
            bool isActive,
            long activationToken)
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

internal enum RegionDeclarationChangeKind
{
    Add,
    Remove,
}

internal sealed class RegionDeclarationChange
{
    public RegionDeclarationChange(
        string name,
        WeakReference<FrameworkElement> hostReference,
        FrameworkElement host,
        long activationToken,
        RegionDeclarationChangeKind kind)
    {
        Name = name;
        HostReference = hostReference;
        Host = host;
        ActivationToken = activationToken;
        Kind = kind;
    }

    public string Name { get; }

    public WeakReference<FrameworkElement> HostReference { get; }

    public FrameworkElement Host { get; }

    public long ActivationToken { get; }

    public RegionDeclarationChangeKind Kind { get; }
}

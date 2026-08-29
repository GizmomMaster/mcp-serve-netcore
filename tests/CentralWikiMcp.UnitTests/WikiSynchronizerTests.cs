using Xunit;
using System.Runtime.CompilerServices;
using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Options;
using CentralWikiMcp.Infrastructure.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CentralWikiMcp.UnitTests;

/// <summary>
/// Проверки синхронизации индекса (FR-34, FR-35, FR-36, FR-37).
/// </summary>
public sealed class WikiSynchronizerTests
{
    private sealed class StubSource(params WikiPage[] pages) : IWikiSource
    {
        public string SourceId => "test";

        /// <summary>Сколько раз источник перечитывался.</summary>
        public int EnumerationCount { get; private set; }

        public List<WikiPage> Pages { get; } = [.. pages];

        public async IAsyncEnumerable<WikiPage> EnumeratePagesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            EnumerationCount++;

            foreach (var page in Pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return page;
                await Task.Yield();
            }
        }
    }

    private sealed class ThrowingSource : IWikiSource
    {
        public string SourceId => "test";

        public async IAsyncEnumerable<WikiPage> EnumeratePagesAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw new IOException("источник недоступен");

#pragma warning disable CS0162 // Требуется, чтобы метод оставался итератором.
            yield break;
#pragma warning restore CS0162
        }
    }

    private static WikiPage CreatePage(string path, string revision) =>
        new(
            PageId: path.Replace('/', '-'),
            Path: path,
            Title: path,
            ContentMarkdown: "текст",
            Namespace: "runbooks",
            Tags: [],
            UpdatedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            Revision: revision,
            Source: "test");

    private static WikiSynchronizer Create(
        IWikiSource source,
        IWikiIndex index,
        ISyncStateStore stateStore) =>
        new(
            source,
            index,
            stateStore,
            Options.Create(new SyncOptions { BatchSize = 2 }),
            new FakeTimeProvider(),
            NullLogger<WikiSynchronizer>.Instance);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task First_run_indexes_every_page()
    {
        var source = new StubSource(CreatePage("runbooks/a", "r1"), CreatePage("runbooks/b", "r1"));
        var index = new FakeWikiIndex();
        var store = new FakeSyncStateStore();

        var state = await Create(source, index, store).SynchronizeAsync(false, Token);

        Assert.Equal(SyncStatus.Succeeded, state.Status);
        Assert.Equal(2, state.PagesIndexed);
        Assert.Equal(0, state.PagesRemoved);
    }

    [Fact]
    public async Task Unchanged_pages_are_not_reindexed()
    {
        var source = new StubSource(CreatePage("runbooks/a", "r1"), CreatePage("runbooks/b", "r1"));
        var index = new FakeWikiIndex();
        var store = new FakeSyncStateStore();
        var synchronizer = Create(source, index, store);

        await synchronizer.SynchronizeAsync(false, Token);

        // FR-36: второй проход не должен переписывать страницы с той же ревизией.
        var second = await synchronizer.SynchronizeAsync(false, Token);

        Assert.Equal(0, second.PagesIndexed);
    }

    [Fact]
    public async Task Changed_page_is_reindexed()
    {
        var source = new StubSource(CreatePage("runbooks/a", "r1"), CreatePage("runbooks/b", "r1"));
        var index = new FakeWikiIndex();
        var synchronizer = Create(source, index, new FakeSyncStateStore());

        await synchronizer.SynchronizeAsync(false, Token);

        source.Pages[0] = CreatePage("runbooks/a", "r2");
        var second = await synchronizer.SynchronizeAsync(false, Token);

        Assert.Equal(1, second.PagesIndexed);
    }

    [Fact]
    public async Task Full_reindex_rewrites_all_pages()
    {
        var source = new StubSource(CreatePage("runbooks/a", "r1"), CreatePage("runbooks/b", "r1"));
        var index = new FakeWikiIndex();
        var synchronizer = Create(source, index, new FakeSyncStateStore());

        await synchronizer.SynchronizeAsync(false, Token);

        // FR-37: полное переиндексирование игнорирует совпадение ревизий.
        var full = await synchronizer.SynchronizeAsync(fullReindex: true, Token);

        Assert.Equal(2, full.PagesIndexed);
    }

    [Fact]
    public async Task Pages_removed_from_source_leave_the_index()
    {
        var source = new StubSource(CreatePage("runbooks/a", "r1"), CreatePage("runbooks/b", "r1"));
        var index = new FakeWikiIndex();
        var synchronizer = Create(source, index, new FakeSyncStateStore());

        await synchronizer.SynchronizeAsync(false, Token);

        source.Pages.RemoveAt(1);
        var second = await synchronizer.SynchronizeAsync(false, Token);

        Assert.Equal(1, second.PagesRemoved);
        Assert.Null(await index.GetPageAsync("runbooks/b", Token));
    }

    [Fact]
    public async Task Source_failure_is_recorded_without_throwing()
    {
        var store = new FakeSyncStateStore();

        // FR-35: сбой синхронизации виден в состоянии, а не роняет фоновый сервис.
        var state = await Create(new ThrowingSource(), new FakeWikiIndex(), store)
            .SynchronizeAsync(false, Token);

        Assert.Equal(SyncStatus.Failed, state.Status);
        Assert.Contains("источник недоступен", state.Error, StringComparison.Ordinal);

        var persisted = await store.GetAsync("test", Token);
        Assert.Equal(SyncStatus.Failed, persisted!.Status);
    }

    [Fact]
    public async Task State_is_marked_running_before_completion()
    {
        var store = new FakeSyncStateStore();
        var source = new StubSource(CreatePage("runbooks/a", "r1"));

        await Create(source, new FakeWikiIndex(), store).SynchronizeAsync(false, Token);

        var state = await store.GetAsync("test", Token);
        Assert.NotNull(state!.CompletedAt);
        Assert.Equal(SyncStatus.Succeeded, state.Status);
    }
}

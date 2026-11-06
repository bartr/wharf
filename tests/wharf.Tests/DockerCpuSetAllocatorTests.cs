using Wharf.Agent;
using Wharf.Docker;
using Wharf.Run;

namespace Wharf.Tests;

public sealed class DockerCpuSetAllocatorTests
{
    [Theory]
    [InlineData(1, 8)]
    [InlineData(4, 8)]
    [InlineData(8, 8)]
    [InlineData(8, 12)]
    [InlineData(4, 4)]
    public void OmitsAffinityWhenAvailableCpusDoNotExceedLimit(int available, int limit)
    {
        Assert.Null(DockerCpuSetAllocator.SelectCpuSet(
            Enumerable.Range(0, available).ToArray(), limit, 0));
    }

    [Theory]
    [InlineData(9, 8)]
    [InlineData(16, 8)]
    [InlineData(128, 8)]
    [InlineData(8, 4)]
    public void SelectsExactlyTheConfiguredNumberOfDistinctCpus(int available, int limit)
    {
        int[] ids = Enumerable.Range(0, available).ToArray();
        for (int trial = 0; trial < available; trial++)
        {
            string selected = Assert.IsType<string>(
                DockerCpuSetAllocator.SelectCpuSet(ids, limit, trial));
            int[] cpus = DockerCpuSetAllocator.ParseCpuList(selected);

            Assert.Equal(limit, cpus.Length);
            Assert.All(cpus, cpu => Assert.Contains(cpu, ids));
            Assert.Equal(selected, DockerCpuSetAllocator.SelectCpuSet(ids, limit, trial));
        }
    }

    [Fact]
    public void RotatesAcrossSparseCpuIdsAndWrapsWithoutOverflow()
    {
        int[] ids = [2, 4, 6, 8, 10, 12, 14, 16, 18, 20];

        Assert.Equal("2,4,6,8", DockerCpuSetAllocator.SelectCpuSet(ids, 4, 0));
        Assert.Equal("10,12,14,16", DockerCpuSetAllocator.SelectCpuSet(ids, 4, 1));
        Assert.Equal("2,4,18,20", DockerCpuSetAllocator.SelectCpuSet(ids, 4, 2));
        Assert.Equal("2,4,18,20", DockerCpuSetAllocator.SelectCpuSet(ids, 4, int.MaxValue));
    }

    [Fact]
    public void ParsesKernelCpuListIncludingIdsBeyond64()
    {
        Assert.Equal(
            new[] { 2, 3, 4, 8, 128, 129 },
            DockerCpuSetAllocator.ParseCpuList("\t2-4,8,128-129\n"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("2-1")]
    [InlineData("1,")]
    [InlineData("1-2-3")]
    [InlineData("one")]
    [InlineData("1,1")]
    [InlineData("2147483648")]
    public void RejectsMalformedCpuEvidence(string value)
    {
        Assert.Throws<AgentExecutionException>(() => DockerCpuSetAllocator.ParseCpuList(value));
    }

    [Fact]
    public async Task ConcurrentAllocationsDiscoverOnlyOnce()
    {
        int discoveries = 0;
        using var allocator = new DockerCpuSetAllocator(async (_, cancellationToken) =>
        {
            Interlocked.Increment(ref discoveries);
            await Task.Delay(10, cancellationToken);
            return "0-15";
        });

        string?[] sets = await Task.WhenAll(Enumerable.Range(0, 4).Select(index =>
            allocator.AllocateAsync("prepared-image", 8, index, CancellationToken.None)));

        Assert.Equal(1, discoveries);
        Assert.Equal(new[] { "0,1,2,3,4,5,6,7", "8,9,10,11,12,13,14,15",
            "0,1,2,3,4,5,6,7", "8,9,10,11,12,13,14,15" }, sets);
    }

    [Fact]
    public async Task FailedDiscoveryIsNotCached()
    {
        int discoveries = 0;
        using var allocator = new DockerCpuSetAllocator((_, _) =>
            ++discoveries == 1
                ? Task.FromException<string>(new AgentExecutionException("probe failed"))
                : Task.FromResult("0-15"));

        await Assert.ThrowsAsync<AgentExecutionException>(() =>
            allocator.AllocateAsync("prepared-image", 8, 0, CancellationToken.None));
        Assert.Equal("0,1,2,3,4,5,6,7", await allocator.AllocateAsync(
            "prepared-image", 8, 0, CancellationToken.None));
        Assert.Equal(2, discoveries);
    }

    [Fact]
    public async Task CancellationDoesNotPoisonLaterDiscovery()
    {
        using var allocator = new DockerCpuSetAllocator((_, _) => Task.FromResult("0-15"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            allocator.AllocateAsync("prepared-image", 8, 0, cancellation.Token));
        Assert.Equal("0,1,2,3,4,5,6,7", await allocator.AllocateAsync(
            "prepared-image", 8, 0, CancellationToken.None));
    }

    [Fact]
    public async Task LegacyUnrestrictedPolicyDoesNotProbeDocker()
    {
        using var allocator = new DockerCpuSetAllocator((_, _) =>
            throw new InvalidOperationException("legacy runs must not probe"));

        Assert.Null(await allocator.AllocateAsync("prepared-image", null, 0, CancellationToken.None));
    }

    [Theory]
    [InlineData("smoke", 1, 1, 1)]
    [InlineData("all", 1, 1, 4)]
    [InlineData("smoke", 2, 4, 4)]
    [InlineData("all", 1, 4, 1)]
    [InlineData("all", 2, 4, 1)]
    public async Task ExemptRunsDoNotProbeDockerOrAllocateACpuSet(
        string prefix, int taskCount, int k, int n)
    {
        using var allocator = new DockerCpuSetAllocator((_, _) =>
            throw new InvalidOperationException("exempt runs must not probe Docker"));
        int? limit = RunService.ResolveContainerCpuLimit(4, taskCount, k, n, prefix);

        Assert.Null(await allocator.AllocateAsync(
            "prepared-image", limit, 0, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentAttemptsOfOneTaskStillAllocateACpuSet()
    {
        using var allocator = new DockerCpuSetAllocator((_, _) => Task.FromResult("0-15"));
        int? limit = RunService.ResolveContainerCpuLimit(8, 1, 4, 4, "all");

        Assert.Equal("0,1,2,3,4,5,6,7", await allocator.AllocateAsync(
            "prepared-image", limit, 0, CancellationToken.None));
    }
}

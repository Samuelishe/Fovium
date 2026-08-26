using Fovium.Imaging;

namespace Fovium.Tests.Imaging;

public sealed class ImageDecoderConcurrencyTests
{
    [Fact]
    public async Task OneSharedGateBoundsExpensiveWorkAcrossDifferentBackends()
    {
        using var twoEntered = new CountdownEvent(2);
        using var release = new ManualResetEventSlim();
        var tracker = new ConcurrencyTracker(twoEntered, release);
        using var decoder = new ImageDecoder(
            [
                new SelectiveBlockingBackend(".heic", "heif", tracker),
                new SelectiveBlockingBackend(".avif", "heif", tracker),
                new SelectiveBlockingBackend(".tif", "tiff", tracker),
                new SelectiveBlockingBackend(null, "skia", tracker),
            ],
            maximumConcurrentDecodes: 2);
        var directory = Directory.CreateTempSubdirectory("Fovium.DecodeConcurrency.Tests.");
        try
        {
            var paths = new[] { "0.heic", "1.tif", "2.avif", "3.jpg", "4.heic", "5.jpg" }
                .Select(name => Path.Combine(directory.FullName, name))
                .ToArray();
            foreach (var path in paths)
            {
                await File.WriteAllBytesAsync(path, [1]);
            }

            var tasks = new List<Task<ImageLoadResult<DecodedImage>>>
            {
                decoder.LoadAsync(
                    paths[0],
                    new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                    CancellationToken.None),
                decoder.LoadAsync(
                    paths[1],
                    new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                    CancellationToken.None),
            };

            Assert.True(twoEntered.Wait(TimeSpan.FromSeconds(5)), "Two decode slots did not become active.");
            Assert.Equal(2, tracker.MaximumActive);
            Assert.True(tracker.Entered("heif") > 0);
            Assert.True(tracker.Entered("tiff") > 0);
            tasks.AddRange(paths.Skip(2).Select(path => decoder.LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None)));
            release.Set();
            var results = await Task.WhenAll(tasks);

            Assert.All(results, result => Assert.Equal(ImageLoadErrorKind.DecodeFailed, result.Error!.Kind));
            Assert.Equal(2, tracker.MaximumActive);
            Assert.True(tracker.Entered("heif") > 0);
            Assert.True(tracker.Entered("tiff") > 0);
            Assert.True(tracker.Entered("skia") > 0);
        }
        finally
        {
            release.Set();
            directory.Delete(true);
        }
    }

    [Fact]
    public void DisposingDispatcherDisposesEveryOwnedBackend()
    {
        var first = new DisposableBackend();
        var second = new DisposableBackend();
        var decoder = new ImageDecoder([first, second]);

        decoder.Dispose();

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }

    [Theory]
    [InlineData(new byte[] { 0x49, 0x49, 0x2A, 0x00 }, (int)TiffSignature.ClassicLittleEndian)]
    [InlineData(new byte[] { 0x4D, 0x4D, 0x00, 0x2A }, (int)TiffSignature.ClassicBigEndian)]
    [InlineData(new byte[] { 0x49, 0x49, 0x2B, 0x00 }, (int)TiffSignature.BigTiffLittleEndian)]
    [InlineData(new byte[] { 0x4D, 0x4D, 0x00, 0x2B }, (int)TiffSignature.BigTiffBigEndian)]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, (int)TiffSignature.NotTiff)]
    public void TiffSignatureSnifferSeparatesClassicBigTiffAndUnrelatedContent(
        byte[] bytes,
        int expected)
    {
        Assert.Equal((TiffSignature)expected, TiffSignatureSniffer.Detect(bytes));
    }

    private sealed class SelectiveBlockingBackend : IImageDecodeBackend
    {
        private readonly string? _extension;
        private readonly string _tag;
        private readonly ConcurrencyTracker _tracker;

        public SelectiveBlockingBackend(string? extension, string tag, ConcurrencyTracker tracker)
        {
            _extension = extension;
            _tag = tag;
            _tracker = tracker;
        }

        public ImageDecodeBackendResult Decode(
            string path,
            ImageLoadAllowance allowance,
            CancellationToken cancellationToken)
        {
            if (_extension is not null && !path.EndsWith(_extension, StringComparison.OrdinalIgnoreCase))
            {
                return ImageDecodeBackendResult.NotMyFormat();
            }

            _tracker.Enter(_tag);
            try
            {
                _tracker.Release.Wait(cancellationToken);
                return ImageDecodeBackendResult.Failure(
                    ImageDecodeBackendResultKind.DecodeFailed,
                    "Controlled backend completion.");
            }
            finally
            {
                _tracker.Exit();
            }
        }
    }

    private sealed class DisposableBackend : IImageDecodeBackend, IDisposable
    {
        public int DisposeCount { get; private set; }

        public ImageDecodeBackendResult Decode(
            string path,
            ImageLoadAllowance allowance,
            CancellationToken cancellationToken) => ImageDecodeBackendResult.NotMyFormat();

        public void Dispose() => DisposeCount++;
    }

    private sealed class ConcurrencyTracker
    {
        private readonly CountdownEvent _twoEntered;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _entered = new();
        private int _active;
        private int _maximumActive;

        public ConcurrencyTracker(CountdownEvent twoEntered, ManualResetEventSlim release)
        {
            _twoEntered = twoEntered;
            Release = release;
        }

        public ManualResetEventSlim Release { get; }

        public int MaximumActive => Volatile.Read(ref _maximumActive);

        public int Entered(string tag) => _entered.TryGetValue(tag, out var count) ? count : 0;

        public void Enter(string tag)
        {
            _entered.AddOrUpdate(tag, 1, static (_, count) => count + 1);

            var active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            if (_twoEntered.CurrentCount > 0)
            {
                _twoEntered.Signal();
            }
        }

        public void Exit() => Interlocked.Decrement(ref _active);

        private void UpdateMaximum(int active)
        {
            while (true)
            {
                var current = Volatile.Read(ref _maximumActive);
                if (active <= current || Interlocked.CompareExchange(ref _maximumActive, active, current) == current)
                {
                    return;
                }
            }
        }
    }
}

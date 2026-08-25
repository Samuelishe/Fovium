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
            [new SelectiveBlockingBackend(".tif", tracker), new SelectiveBlockingBackend(null, tracker)],
            maximumConcurrentDecodes: 2);
        var directory = Directory.CreateTempSubdirectory("Fovium.DecodeConcurrency.Tests.");
        try
        {
            var paths = Enumerable.Range(0, 6)
                .Select(index => Path.Combine(directory.FullName, index % 2 == 0 ? $"{index}.tif" : $"{index}.jpg"))
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
            Assert.True(tracker.TiffEntered > 0);
            Assert.True(tracker.OtherEntered > 0);
            tasks.AddRange(paths.Skip(2).Select(path => decoder.LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None)));
            release.Set();
            var results = await Task.WhenAll(tasks);

            Assert.All(results, result => Assert.Equal(ImageLoadErrorKind.DecodeFailed, result.Error!.Kind));
            Assert.Equal(2, tracker.MaximumActive);
        }
        finally
        {
            release.Set();
            directory.Delete(true);
        }
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
        private readonly ConcurrencyTracker _tracker;

        public SelectiveBlockingBackend(string? extension, ConcurrencyTracker tracker)
        {
            _extension = extension;
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

            _tracker.Enter(_extension is not null);
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

    private sealed class ConcurrencyTracker
    {
        private readonly CountdownEvent _twoEntered;
        private int _active;
        private int _maximumActive;
        private int _tiffEntered;
        private int _otherEntered;

        public ConcurrencyTracker(CountdownEvent twoEntered, ManualResetEventSlim release)
        {
            _twoEntered = twoEntered;
            Release = release;
        }

        public ManualResetEventSlim Release { get; }

        public int MaximumActive => Volatile.Read(ref _maximumActive);

        public int TiffEntered => Volatile.Read(ref _tiffEntered);

        public int OtherEntered => Volatile.Read(ref _otherEntered);

        public void Enter(bool isTiff)
        {
            if (isTiff)
            {
                Interlocked.Increment(ref _tiffEntered);
            }
            else
            {
                Interlocked.Increment(ref _otherEntered);
            }

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

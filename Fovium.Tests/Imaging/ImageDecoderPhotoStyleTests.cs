using Fovium.Imaging;
using Fovium.PhotoStyling;
using Fovium.Stage;
using Fovium.Tests.PhotoStyling;
using Fovium.Tests.Stage;

namespace Fovium.Tests.Imaging;

public sealed class ImageDecoderPhotoStyleTests
{
    [Fact]
    public async Task SuccessfulDecodeAttachesAnalysisInsideOffUiDecodeWork()
    {
        var analyzer = new RecordingAnalyzer();
        using var decoder = new ImageDecoder(
            [new SuccessfulBackend()],
            photoStyleAnalyzer: analyzer);
        var directory = Directory.CreateTempSubdirectory("Fovium.PhotoStyle.Tests.");
        try
        {
            var path = Path.Combine(directory.FullName, "photo.test");
            await File.WriteAllBytesAsync(path, [1]);

            var result = await decoder.LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            using var image = result.Image;

            Assert.True(result.IsSuccess);
            Assert.NotNull(image);
            Assert.Equal(1, analyzer.CallCount);
            Assert.Null(analyzer.AnalysisSynchronizationContext);
            Assert.Same(analyzer.Result, image!.GetPhotoStyleAnalysis());
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task StylingFailureKeepsDecodedPhotoUsableWithTruthfulFallbackState()
    {
        using var decoder = new ImageDecoder(
            [new SuccessfulBackend()],
            photoStyleAnalyzer: new ThrowingAnalyzer());
        var directory = Directory.CreateTempSubdirectory("Fovium.PhotoStyle.Tests.");
        try
        {
            var path = Path.Combine(directory.FullName, "photo.test");
            await File.WriteAllBytesAsync(path, [1]);

            var result = await decoder.LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                CancellationToken.None);
            using var image = result.Image;

            Assert.True(result.IsSuccess);
            Assert.NotNull(image);
            Assert.False(image!.HasPhotoStyleAnalysis);
            Assert.Null(image.GetPhotoStyleAnalysis());
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task CancellationDuringAnalysisDisposesDecodedCandidateAndSuppressesPublication()
    {
        var backend = new CapturingBackend();
        var analyzer = new BlockingAnalyzer();
        using var decoder = new ImageDecoder([backend], photoStyleAnalyzer: analyzer);
        var directory = Directory.CreateTempSubdirectory("Fovium.PhotoStyle.Tests.");
        try
        {
            var path = Path.Combine(directory.FullName, "photo.test");
            await File.WriteAllBytesAsync(path, [1]);
            using var cancellation = new CancellationTokenSource();

            var pending = decoder.LoadAsync(
                path,
                new ImageLoadAllowance(long.MaxValue, long.MaxValue, false),
                cancellation.Token);
            await analyzer.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
            Assert.NotNull(backend.Image);
            Assert.Throws<ObjectDisposedException>(() => backend.Image!.AcquireRenderLease());
        }
        finally
        {
            directory.Delete(true);
        }
    }

    private sealed class SuccessfulBackend : IImageDecodeBackend
    {
        public ImageDecodeBackendResult Decode(
            string path,
            ImageLoadAllowance allowance,
            CancellationToken cancellationToken) =>
            ImageDecodeBackendResult.Success(StageTestImages.CreateDecoded(path));
    }

    private sealed class CapturingBackend : IImageDecodeBackend
    {
        public DecodedImage? Image { get; private set; }

        public ImageDecodeBackendResult Decode(
            string path,
            ImageLoadAllowance allowance,
            CancellationToken cancellationToken)
        {
            Image = StageTestImages.CreateDecoded(path);
            return ImageDecodeBackendResult.Success(Image);
        }
    }

    private sealed class RecordingAnalyzer : IPhotoStyleAnalyzer
    {
        public PhotoStyleAnalysis Result { get; } = PhotoDerivedStylePolicyTests.CreateAnalysis(
            new StageColor(10, 20, 30),
            new StageColor(40, 50, 60),
            new StageColor(70, 80, 90));

        public int CallCount { get; private set; }

        public SynchronizationContext? AnalysisSynchronizationContext { get; private set; }

        public PhotoStyleAnalysis Analyze(DecodedImage image, CancellationToken cancellationToken)
        {
            CallCount++;
            AnalysisSynchronizationContext = SynchronizationContext.Current;
            return Result;
        }
    }

    private sealed class ThrowingAnalyzer : IPhotoStyleAnalyzer
    {
        public PhotoStyleAnalysis Analyze(DecodedImage image, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Controlled analysis failure.");
    }

    private sealed class BlockingAnalyzer : IPhotoStyleAnalyzer
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PhotoStyleAnalysis Analyze(DecodedImage image, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancellation was not observed.");
        }
    }
}

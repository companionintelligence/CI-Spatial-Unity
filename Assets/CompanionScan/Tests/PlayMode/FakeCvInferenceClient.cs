using System.Threading.Tasks;
using CompanionScan.Core;

namespace CompanionScan.Tests.PlayMode
{
    internal class FakeCvInferenceClient : ICvInferenceClient
    {
        public DetectionResult DetectionResponse { get; set; } = new DetectionResult();
        public SegmentationResult SegmentationResponse { get; set; } = new SegmentationResult();
        public FlorenceResult FlorenceResponse { get; set; } = new FlorenceResult
        {
            text = "fake caption",
            task = "<MORE_DETAILED_CAPTION>",        };

        public Task<DetectionResult> DetectAsync(byte[] jpegBytes, string prompt)
        {
            return Task.FromResult(DetectionResponse);
        }

        public Task<SegmentationResult> SegmentAsync(byte[] jpegBytes, string prompt, string groundingModel = "grounding_dino")
        {
            return Task.FromResult(SegmentationResponse);
        }

        public Task<FlorenceResult> FlorenceAsync(byte[] jpegBytes, string task, string textInput = "")
        {
            return Task.FromResult(FlorenceResponse);
        }
    }
}

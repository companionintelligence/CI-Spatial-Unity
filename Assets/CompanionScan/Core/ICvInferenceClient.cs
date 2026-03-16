using System.Threading.Tasks;

namespace CompanionScan.Core
{
    public interface ICvInferenceClient
    {
        Task<DetectionResult> DetectAsync(byte[] jpegBytes, string prompt);
        Task<SegmentationResult> SegmentAsync(byte[] jpegBytes, string prompt, string groundingModel = "grounding_dino");
        Task<FlorenceResult> FlorenceAsync(byte[] jpegBytes, string task, string textInput = "");
    }
}
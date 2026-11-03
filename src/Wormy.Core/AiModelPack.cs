namespace Wormy.Core;

public static class AiModelPack
{
    public static readonly IReadOnlyList<string> RequiredFiles = Array.AsReadOnly(new[]
    {
        "minilm/model.onnx", "minilm/vocab.txt", "clip/text_model_quantized.onnx",
        "clip/vision_model_quantized.onnx", "clip/vocab.json", "clip/merges.txt"
    });

    // Presence check only. Release tooling verifies hashes; model runtimes validate formats.
    public static bool IsAvailable(string modelDirectory)
    {
        try { return RequiredFiles.All(relative => new FileInfo(Path.Combine(modelDirectory, relative)).Length > 0); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return false; }
    }
}

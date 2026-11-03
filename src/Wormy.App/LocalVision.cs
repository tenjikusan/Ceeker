using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Wormy.App;
public sealed class LocalVision : IDisposable
{
    private readonly InferenceSession textSession;
    private readonly InferenceSession imageSession;
    private readonly Dictionary<string,int> vocabulary;
    private readonly Dictionary<string,int> ranks;
    private readonly Dictionary<byte,char> byteCharacters;
    public LocalVision()
    {
        Environment.SetEnvironmentVariable("ORT_DISABLE_TELEMETRY","1"); OrtEnv.Instance().DisableTelemetryEvents();
        var folder=Path.Combine(AppContext.BaseDirectory,"Models","clip");
        vocabulary=JsonSerializer.Deserialize<Dictionary<string,int>>(File.ReadAllText(Path.Combine(folder,"vocab.json")))!;
        ranks=File.ReadLines(Path.Combine(folder,"merges.txt")).Where(line=>!line.StartsWith('#')&&!string.IsNullOrWhiteSpace(line)).Select((pair,rank)=>(pair,rank)).ToDictionary(item=>item.pair,item=>item.rank);
        var bytes=Enumerable.Range(33,94).Concat(Enumerable.Range(161,12)).Concat(Enumerable.Range(174,82)).ToList(); var chars=bytes.ToList(); var extra=0;
        for(var b=0;b<256;b++) if(!bytes.Contains(b)) { bytes.Add(b); chars.Add(256+extra++); }
        byteCharacters=bytes.Zip(chars,(b,c)=>(b,c)).ToDictionary(item=>(byte)item.b,item=>(char)item.c);
        using var options=new SessionOptions { IntraOpNumThreads=2,InterOpNumThreads=1 };
        textSession=new InferenceSession(Path.Combine(folder,"text_model_quantized.onnx"),options);
        imageSession=new InferenceSession(Path.Combine(folder,"vision_model_quantized.onnx"),options);
    }
    private IEnumerable<long> Tokens(string text)
    {
        text=System.Net.WebUtility.HtmlDecode(System.Net.WebUtility.HtmlDecode(text)).Trim().ToLowerInvariant();
        foreach(Match match in Regex.Matches(text,@"'s|'t|'re|'ve|'m|'ll|'d|[\p{L}]+|[\p{N}]|[^\s\p{L}\p{N}]+"))
        {
            var encoded=Encoding.UTF8.GetBytes(match.Value).Select(b=>byteCharacters[b].ToString()).ToList(); encoded[^1]+="</w>";
            while(encoded.Count>1)
            {
                var best=-1; var rank=int.MaxValue;
                for(var i=0;i<encoded.Count-1;i++) if(ranks.TryGetValue(encoded[i]+" "+encoded[i+1],out var candidate)&&candidate<rank) { rank=candidate; best=i; }
                if(best<0) break;
                var left=encoded[best]; var right=encoded[best+1]; var merged=new List<string>();
                for(var i=0;i<encoded.Count;i++) if(i+1<encoded.Count&&encoded[i]==left&&encoded[i+1]==right) { merged.Add(left+right); i++; } else merged.Add(encoded[i]);
                encoded=merged;
            }
            foreach(var piece in encoded) yield return vocabulary.TryGetValue(piece,out var id) ? id : vocabulary["<|endoftext|>"];
        }
    }
    private static float[] Output(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,string name)
    {
        var tensor=(outputs.FirstOrDefault(item=>item.Name==name)??outputs.First()).AsTensor<float>();
        var vector=tensor.ToArray(); var norm=Math.Sqrt(vector.Sum(v=>(double)v*v)); if(norm>0) for(var i=0;i<vector.Length;i++) vector[i]/=(float)norm; return vector;
    }
    public float[] EncodeText(string text)
    {
        var tokens=new List<long> { vocabulary["<|startoftext|>"] }; tokens.AddRange(Tokens(text).Take(75)); tokens.Add(vocabulary["<|endoftext|>"]);
        var count=tokens.Count; while(tokens.Count<77) tokens.Add(vocabulary["<|endoftext|>"]);
        var inputs=new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input_ids",new DenseTensor<long>(tokens.ToArray(),new[] {1,77})) };
        if(textSession.InputMetadata.ContainsKey("attention_mask")) inputs.Add(NamedOnnxValue.CreateFromTensor("attention_mask",new DenseTensor<long>(Enumerable.Range(0,77).Select(i=>i<count ? 1L : 0L).ToArray(),new[] {1,77})));
        using var output=textSession.Run(inputs); return Output(output,"text_embeds");
    }
    public async Task<float[]> EncodeImageAsync(string path,CancellationToken token)
    {
        var info=new FileInfo(path); if(!info.Exists||info.Length>40_000_000) throw new IOException("Vision image limit is 40 MB.");
        var file=await StorageFile.GetFileFromPathAsync(path).AsTask(token); using var stream=await file.OpenReadAsync().AsTask(token);
        var decoder=await BitmapDecoder.CreateAsync(stream).AsTask(token); var w=decoder.OrientedPixelWidth; var h=decoder.OrientedPixelHeight;
        if(w==0||h==0||w>16384||h>16384||(long)w*h>100_000_000||Math.Max(w,h)/(double)Math.Min(w,h)>20) throw new IOException("Vision image exceeds dimensions/aspect limit.");
        var scale=224d/Math.Min(w,h); var sw=(uint)Math.Round(w*scale); var sh=(uint)Math.Round(h*scale);
        var transform=new BitmapTransform { ScaledWidth=decoder.PixelWidth==w ? sw : sh,ScaledHeight=decoder.PixelHeight==h ? sh : sw,InterpolationMode=BitmapInterpolationMode.Fant };
        var pixels=(await decoder.GetPixelDataAsync(BitmapPixelFormat.Rgba8,BitmapAlphaMode.Straight,transform,ExifOrientationMode.RespectExifOrientation,ColorManagementMode.ColorManageToSRgb).AsTask(token)).DetachPixelData();
        if(pixels.Length!=checked((int)(sw*sh*4))) throw new IOException("Unexpected vision sample size.");
        var values=new float[3*224*224]; var mean=new[] {.48145466f,.4578275f,.40821073f}; var std=new[] {.26862954f,.26130258f,.27577711f};
        for(var i=0;i<224*224;i++) for(var channel=0;channel<3;channel++) { var source=((i/224+(int)(sh-224)/2)*(int)sw+i%224+(int)(sw-224)/2)*4; var alpha=pixels[source+3]/255f; values[channel*224*224+i]=((pixels[source+channel]/255f*alpha+1-alpha)-mean[channel])/std[channel]; }
        token.ThrowIfCancellationRequested();
        using var output=imageSession.Run(new[] { NamedOnnxValue.CreateFromTensor("pixel_values",new DenseTensor<float>(values,new[] {1,3,224,224})) });
        token.ThrowIfCancellationRequested(); return Output(output,"image_embeds");
    }
    public void Dispose() { textSession.Dispose(); imageSession.Dispose(); }
}

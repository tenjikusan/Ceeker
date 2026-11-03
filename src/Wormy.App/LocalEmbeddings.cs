using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Wormy.App;
public sealed class LocalEmbeddings : IDisposable
{
    private readonly InferenceSession session;
    private readonly Dictionary<string,int> vocabulary;
    public LocalEmbeddings()
    {
        Environment.SetEnvironmentVariable("ORT_DISABLE_TELEMETRY","1"); OrtEnv.Instance().DisableTelemetryEvents();
        var folder=Path.Combine(AppContext.BaseDirectory,"Models","minilm");
        vocabulary=File.ReadLines(Path.Combine(folder,"vocab.txt")).Select((token,id)=>(token,id)).ToDictionary(item=>item.token,item=>item.id);
        using var options=new SessionOptions { IntraOpNumThreads=2,InterOpNumThreads=1 };
        session=new InferenceSession(Path.Combine(folder,"model.onnx"),options);
    }
    public float[] Encode(string text)
    {
        var clean=new StringBuilder();
        foreach(var character in text.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if(CharUnicodeInfo.GetUnicodeCategory(character)==UnicodeCategory.NonSpacingMark) continue;
            if(character is >= '\u4E00' and <= '\u9FFF') clean.Append(' ').Append(character).Append(' ');
            else clean.Append(char.IsControl(character) ? ' ' : character);
        }
        var ids=new List<long> { vocabulary["[CLS]"] };
        foreach(Match token in Regex.Matches(clean.ToString(),@"[\p{L}\p{N}]+|[^\s]"))
        {
            if(ids.Count>=127) break;
            var word=token.Value; var pieces=new List<int>(); var start=0;
            while(start<word.Length && word.Length<=100)
            {
                var end=word.Length; var found=false;
                while(end>start)
                {
                    var candidate=(start==0 ? "" : "##")+word[start..end];
                    if(vocabulary.TryGetValue(candidate,out var id)) { pieces.Add(id); found=true; break; }
                    end--;
                }
                if(!found) break; start=end;
            }
            if(start<word.Length) ids.Add(vocabulary["[UNK]"]); else foreach(var piece in pieces) { if(ids.Count==127) break; ids.Add(piece); }
        }
        ids.Add(vocabulary["[SEP]"]);
        var input=ids.ToArray(); var shape=new[] {1,input.Length};
        var values=new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input_ids",new DenseTensor<long>(input,shape)),NamedOnnxValue.CreateFromTensor("attention_mask",new DenseTensor<long>(Enumerable.Repeat(1L,input.Length).ToArray(),shape)),NamedOnnxValue.CreateFromTensor("token_type_ids",new DenseTensor<long>(new long[input.Length],shape)) };
        using var output=session.Run(values);
        var tensor=output.First().AsTensor<float>(); var dimension=tensor.Dimensions[^1]; var vector=new float[dimension];
        if(tensor.Rank==2) { for(var d=0;d<dimension;d++) vector[d]=tensor[0,d]; }
        else for(var t=0;t<input.Length;t++) for(var d=0;d<dimension;d++) vector[d]+=tensor[0,t,d]/input.Length;
        var norm=Math.Sqrt(vector.Sum(value=>(double)value*value));
        if(norm>0) for(var d=0;d<dimension;d++) vector[d]/=(float)norm;
        return vector;
    }
    public static double Similarity(float[] a,float[] b) => a.Length==b.Length ? a.Zip(b,(x,y)=>(double)x*y).Sum() : 0;
    public void Dispose()=>session.Dispose();
}

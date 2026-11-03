using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Wormy.Core;

namespace Wormy.App;
public sealed partial class MainWindow
{
    private readonly SemaphoreSlim semanticWorker=new(1);
    private readonly Dictionary<string,float[]> semanticVectors=new(StringComparer.Ordinal);
    private LocalEmbeddings? embeddingModel;
    private LocalVision? visionModel;
    private async Task<Dictionary<string,double>> SemanticMatchesAsync(SearchQuery query,IReadOnlyList<FileEntry> snapshot,CancellationToken token)
    {
        var result=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase);
        if(!preferences.SemanticSearch || query.Terms.Count==0 || snapshot.Count==0) return result;
        if(!AiModelPack.IsAvailable(Path.Combine(AppContext.BaseDirectory,"Models")))
        { Status.Text="Optional AI pack is not installed · Names, tags and indexed content search remain available."; return result; }
        await semanticWorker.WaitAsync(token);
        try
        {
            if(closed) return result;
            Status.Text="Offline AI search · preparing local models and representations…";
            var progress=new Progress<string>(message=> { if(!closed&&!token.IsCancellationRequested) Status.Text=message; });
            var texts=await index.SemanticTextsAsync(token);
            return await Task.Run(async()=>
            {
                embeddingModel ??= new LocalEmbeddings(); var needle=embeddingModel.Encode(string.Join(" ",query.Terms));
                var completed=0;
                foreach(var file in snapshot.Where(query.MatchesMetadata).Take(1000))
                {
                    token.ThrowIfCancellationRequested();
                    var text=file.Name+" "+(texts.TryGetValue(file.Path,out var content) ? content : "");
                    var key=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
                    if(!semanticVectors.TryGetValue(key,out var vector)) { vector=embeddingModel.Encode(text); if(semanticVectors.Count>=1000) semanticVectors.Clear(); semanticVectors[key]=vector; }
                    var score=LocalEmbeddings.Similarity(needle,vector);
                    if(score>=.4) result[file.Path]=score;
                    if(++completed%25==0) ((IProgress<string>)progress).Report($"Offline AI search · {completed:N0} text candidates processed");
                }
                visionModel ??=new LocalVision(); var visualQuery=visionModel.EncodeText("a photo of "+string.Join(" ",query.Terms));
                completed=0;
                foreach(var file in snapshot.Where(query.MatchesMetadata).Where(f=>f.Category=="Images").Take(300))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        ThumbnailCache.Validate(file);
                        var key="image:"+file.Path+":"+file.Length+":"+file.ModifiedUtc.Ticks;
                        if(!semanticVectors.TryGetValue(key,out var vector)) { vector=await visionModel.EncodeImageAsync(file.Path,token); ThumbnailCache.Validate(file); if(semanticVectors.Count>=2000) semanticVectors.Clear(); semanticVectors[key]=vector; }
                        var score=LocalEmbeddings.Similarity(visualQuery,vector);
                        if(score>=.25) result[file.Path]=Math.Max(result.GetValueOrDefault(file.Path),score);
                        if(++completed%5==0) ((IProgress<string>)progress).Report($"Offline AI search · {completed:N0} photos processed");
                    }
                    catch(OperationCanceledException) { throw; }
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException) { }
                }
                return result;
            },token);
        }
        finally { semanticWorker.Release(); }
    }
    private void InitializeSemanticSearch()
    {
        var choice=new CheckBox { Content="Offline AI text and photo search",IsChecked=preferences.SemanticSearch };
        ToolTipService.SetToolTip(choice,"Local MiniLM text and CLIP image similarity. First 1,000 text candidates and 300 images; approximate results. Quoted searches remain literal.");
        choice.Click+=(_,_)=>
        {
            if(choice.IsChecked==true&&!AiModelPack.IsAvailable(Path.Combine(AppContext.BaseDirectory,"Models")))
            { choice.IsChecked=false;Status.Text="Install the optional Ceeker AI pack into the Ceeker application folder, then restart. Standard search and OCR work without it."; }
            preferences.SemanticSearch=choice.IsChecked==true; SavePreferences(); UpdateFiles();
        };
        SettingsPanel.Children.Add(choice);
        SettingsPanel.Children.Add(new TextBlock { Text=AiModelPack.IsAvailable(Path.Combine(AppContext.BaseDirectory,"Models"))?"AI pack installed · AI searches run offline.":"Optional AI pack not installed. Standard search, indexed document text and OCR remain available. Install the separate Ceeker AI pack into this application folder, then restart. No automatic download is configured.",FontSize=12,TextWrapping=TextWrapping.Wrap,Foreground=AppTheme.Brush("MutedBrush") });
        Closed+=async(_,_)=> { await semanticWorker.WaitAsync(); try { embeddingModel?.Dispose(); visionModel?.Dispose(); } finally { semanticWorker.Release(); } };
    }
}



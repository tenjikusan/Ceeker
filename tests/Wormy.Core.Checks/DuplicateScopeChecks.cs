using Wormy.Core;
internal static class DuplicateScopeChecks
{
    public static async Task RunAsync()
    {
        var root=Path.Combine(Path.GetTempPath(),"ceeker-scope-"+Guid.NewGuid().ToString("N"));
        try
        {
            var a=Path.Combine(root,"Chosen");var b=Path.Combine(root,"Other");var outside=Path.Combine(root,"ChosenElsewhere");
            Directory.CreateDirectory(Path.Combine(a,"Nested"));Directory.CreateDirectory(b);Directory.CreateDirectory(outside);
            await File.WriteAllTextAsync(Path.Combine(a,"one.txt"),"same");await File.WriteAllTextAsync(Path.Combine(a,"Nested","two.txt"),"same");await File.WriteAllTextAsync(Path.Combine(b,"three.txt"),"same");await File.WriteAllTextAsync(Path.Combine(outside,"four.txt"),"same");
            var files=(await FolderScanner.ScanAsync(root,CancellationToken.None)).Files;
            var selected=DuplicateScanScope.Select(files,[a]);
            if(selected.Length!=2)throw new Exception("Nested or prefix boundary scope failed.");
            if(DuplicateScanScope.Select(files,[]).Length!=0||DuplicateScanScope.Select(files,null).Length!=4)throw new Exception("Empty/all scope failed.");
            if(DuplicateScanScope.Select(files,[a,Path.Combine(a,"Nested")]).Length!=2)throw new Exception("Overlapping scopes duplicated paths.");
            var combined=await DuplicateScanner.ScanAsync(DuplicateScanScope.Select(files,[a,b]),null,CancellationToken.None);
            if(combined.Groups.Count!=1||combined.Groups[0].Files.Count!=3)throw new Exception("Cross-folder duplicate scope failed.");
            Console.WriteLine("PASS: duplicate selected/all/empty folders, recursive scope, sibling prefix boundary, overlap and cross-folder exact matching.");
        }
        finally{Directory.Delete(root,true);}
    }
}

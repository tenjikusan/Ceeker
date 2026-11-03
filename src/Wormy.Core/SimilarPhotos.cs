using System.Numerics;

namespace Wormy.Core;

public sealed record PhotoVariant(ulong Hash,double Aspect);
public sealed record PhotoFeatures(byte[] Descriptors,float[] X,float[] Y);
public sealed record PhotoFingerprint(ulong Hash, double Aspect, double Red, double Green, double Blue, ulong? PerceptualHash = null, ulong? CenterHash = null, IReadOnlyList<PhotoVariant>? Variants = null,PhotoFeatures? Features=null);
public sealed record SimilarPhotoPair(FileEntry First, FileEntry Second, int Distance);
public sealed record SimilarPhotoResult(IReadOnlyList<SimilarPhotoPair> Pairs, int Compared, int Skipped, int NotProcessed, bool PairLimitReached);
public static class SimilarPhotos
{
    private static readonly double[,] Cosines = BuildCosines();
    private static double[,] BuildCosines() { var values=new double[8,32]; for(var u=0;u<8;u++) for(var x=0;x<32;x++) values[u,x]=Math.Cos((2*x+1)*u*Math.PI/64); return values; }
    public static IReadOnlyList<PhotoVariant> Variants(byte[] rgba,double aspect)
    {
        if(rgba.Length!=32*32*4 || !double.IsFinite(aspect)||aspect<=0) throw new IOException("Invalid variant sample.");
        var result=new List<PhotoVariant>();
        for(var turns=0;turns<4;turns++) foreach(var crop in new[] {32,24,16})
        {
            var sample=new byte[rgba.Length]; var margin=(32-crop)/2;
            for(var y=0;y<32;y++) for(var x=0;x<32;x++)
            {
                var sx=margin+x*crop/32; var sy=margin+y*crop/32;
                for(var t=0;t<turns;t++) (sx,sy)=(31-sy,sx);
                Array.Copy(rgba,(sy*32+sx)*4,sample,(y*32+x)*4,4);
            }
            var minimum=int.MaxValue; var maximum=int.MinValue;
            for(var i=0;i<sample.Length;i+=4) { var value=sample[i]*299+sample[i+1]*587+sample[i+2]*114; minimum=Math.Min(minimum,value); maximum=Math.Max(maximum,value); }
            if(maximum-minimum>=12000) result.Add(new(PerceptualHash(sample),turns%2==0 ? aspect : 1/aspect));
        }
        return result;
    }
    public static ulong PerceptualHash(byte[] rgba, bool center = false)
    {
        if (rgba.Length != 32 * 32 * 4) throw new IOException("Expected 32-pixel image sample.");
        var values = new double[32,32];
        for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++)
        {
            var sx = center ? 4 + x * 24 / 32 : x; var sy = center ? 4 + y * 24 / 32 : y; var i = (sy * 32 + sx) * 4; var alpha = rgba[i + 3] / 255d;
            values[y,x] = (rgba[i] * .299 + rgba[i+1] * .587 + rgba[i+2] * .114) * alpha + 255 * (1-alpha);
        }
        var coefficients = new double[64];
        for (var v = 0; v < 8; v++) for (var u = 0; u < 8; u++)
        { double sum = 0; for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++) sum += values[y,x] * Cosines[u,x] * Cosines[v,y]; coefficients[v*8+u] = sum; }
        var median = coefficients.Skip(1).Order().ElementAt(31); ulong hash = 0;
        for (var i = 1; i < 64; i++) if (coefficients[i] > median) hash |= 1UL << i;
        return hash;
    }
    public static PhotoFingerprint Fingerprint(byte[] rgba, double aspect)
    {
        if (rgba.Length != 9 * 8 * 4 || !double.IsFinite(aspect) || aspect <= 0) throw new IOException("Invalid image sample.");
        var luminance = new double[72]; double red = 0, green = 0, blue = 0;
        for (var i = 0; i < 72; i++)
        {
            var alpha = rgba[i * 4 + 3] / 255d;
            var r = rgba[i * 4] * alpha + 255 * (1 - alpha); var g = rgba[i * 4 + 1] * alpha + 255 * (1 - alpha); var b = rgba[i * 4 + 2] * alpha + 255 * (1 - alpha);
            red += r; green += g; blue += b; luminance[i] = r * .299 + g * .587 + b * .114;
        }
        if (luminance.Max() - luminance.Min() < 12) throw new IOException("Too little visual detail for a reliable comparison.");
        ulong hash = 0;
        for (var y = 0; y < 8; y++) for (var x = 0; x < 8; x++) if (luminance[y * 9 + x] > luminance[y * 9 + x + 1]) hash |= 1UL << (y * 8 + x);
        return new(hash, aspect, red / 72, green / 72, blue / 72);
    }
    public static int? Distance(PhotoFingerprint a, PhotoFingerprint b)
    {
        if(a.Variants is { } av && b.Variants is { } bv && Math.Abs(a.Red-b.Red)<=30 && Math.Abs(a.Green-b.Green)<=30 && Math.Abs(a.Blue-b.Blue)<=30)
        {
            foreach(var firstVariant in av) foreach(var secondVariant in bv)
                if(Math.Abs(firstVariant.Aspect-secondVariant.Aspect)/Math.Max(firstVariant.Aspect,secondVariant.Aspect)<=.15)
                { var bits=BitOperations.PopCount(firstVariant.Hash^secondVariant.Hash); if(bits<=6) return bits; }
        }
        if (a.PerceptualHash is { } first && b.PerceptualHash is { } second)
        {
            if (Math.Abs(a.Aspect-b.Aspect) / Math.Max(a.Aspect,b.Aspect) > .15) return null;
            var full = BitOperations.PopCount(first ^ second);
            var center = a.CenterHash is { } ac && b.CenterHash is { } bc ? BitOperations.PopCount(ac ^ bc) : 64;
            var detail = BitOperations.PopCount(a.Hash ^ b.Hash);
            if (full <= 10 && (detail <= 16 || center <= 10)) return full;
            // Border changes from small camera movement can disturb the full-image hash.
            // Require agreement in both the center and directional detail, plus similar color.
            return center <= 8 && detail <= 8 && Math.Abs(a.Red-b.Red) <= 30 && Math.Abs(a.Green-b.Green) <= 30 && Math.Abs(a.Blue-b.Blue) <= 30 ? center : null;
        }
        if (Math.Abs(a.Aspect - b.Aspect) / Math.Max(a.Aspect, b.Aspect) > .05 || Math.Abs(a.Red - b.Red) > 30 || Math.Abs(a.Green - b.Green) > 30 || Math.Abs(a.Blue - b.Blue) > 30) return null;
        var distance = BitOperations.PopCount(a.Hash ^ b.Hash); return distance <= 6 ? distance : null;
    }
    public static async Task<SimilarPhotoResult> FindAsync(IEnumerable<FileEntry> files, Func<FileEntry, CancellationToken, Task<PhotoFingerprint>> read, CancellationToken token, IProgress<int>? progress = null, int maximumImages = 2000, int maximumPairs = 300,Func<PhotoFingerprint,PhotoFingerprint,int?>? geometricCompare=null)
    {
        if (maximumImages < 1 || maximumPairs < 1) throw new ArgumentOutOfRangeException(nameof(maximumImages));
        var candidates = files.Where(f => f.Category == "Images").DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase).OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        var samples = new List<(FileEntry File, PhotoFingerprint Sample)>(); var skipped = 0;
        foreach (var file in candidates.Take(maximumImages))
        {
            token.ThrowIfCancellationRequested();
            try { ThumbnailCache.Validate(file); var sample = await read(file, token); ThumbnailCache.Validate(file); samples.Add((file, sample)); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch { skipped++; }
            progress?.Report(samples.Count + skipped);
        }
        var pairs = new List<SimilarPhotoPair>();
        for (var i = 0; i < samples.Count; i++) for (var j = i + 1; j < samples.Count; j++)
        {
            token.ThrowIfCancellationRequested(); var distance = Distance(samples[i].Sample, samples[j].Sample);
            distance ??= geometricCompare?.Invoke(samples[i].Sample,samples[j].Sample);
            if (distance is null) continue;
            if (pairs.Count == maximumPairs) return new(pairs, samples.Count, skipped, Math.Max(0, candidates.Length - maximumImages), true);
            pairs.Add(new(samples[i].File, samples[j].File, distance.Value));
        }
        return new(pairs, samples.Count, skipped, Math.Max(0, candidates.Length - maximumImages), false);
    }
}

using OpenCvSharp;
using Wormy.Core;
namespace Wormy.App;
public static class GeometricPhotos
{
    public static PhotoFeatures Extract(byte[] gray,int width,int height)
    {
        if(width is < 1 or > 512 || height is < 1 or > 512 || gray.Length!=width*height) throw new IOException("Invalid geometric sample dimensions.");
        using var image=Mat.FromPixelData(height,width,MatType.CV_8UC1,gray); using var orb=ORB.Create(256);
        using var descriptors=new Mat(); orb.DetectAndCompute(image,null,out var points,descriptors);
        if(descriptors.Empty()) return new([],[],[]);
        var bytes=new byte[descriptors.Rows*descriptors.Cols]; System.Runtime.InteropServices.Marshal.Copy(descriptors.Data,bytes,0,bytes.Length);
        return new(bytes,points.Select(p=>p.Pt.X).ToArray(),points.Select(p=>p.Pt.Y).ToArray());
    }
    public static int? Compare(PhotoFingerprint a,PhotoFingerprint b)
    {
        if(a.Features is not { } af || b.Features is not { } bf || af.X.Length<16 || bf.X.Length<16) return null;
        if(af.X.Length>256||bf.X.Length>256||af.Y.Length!=af.X.Length||bf.Y.Length!=bf.X.Length||af.Descriptors.Length!=af.X.Length*32||bf.Descriptors.Length!=bf.X.Length*32) return null;
        using var first=Mat.FromPixelData(af.X.Length,32,MatType.CV_8UC1,af.Descriptors); using var second=Mat.FromPixelData(bf.X.Length,32,MatType.CV_8UC1,bf.Descriptors);
        using var matcher=new BFMatcher(NormTypes.Hamming); var matches=matcher.KnnMatch(first,second,2).Where(pair=>pair.Length==2&&pair[0].Distance<.7*pair[1].Distance&&pair[0].Distance<50).Select(pair=>pair[0]).GroupBy(match=>match.TrainIdx).Select(group=>group.MinBy(match=>match.Distance)).ToArray();
        if(matches.Length<16) return null;
        using var mask=new Mat();
        using var homography=Cv2.FindHomography(matches.Select(m=>new Point2d(af.X[m.QueryIdx],af.Y[m.QueryIdx])),matches.Select(m=>new Point2d(bf.X[m.TrainIdx],bf.Y[m.TrainIdx])),HomographyMethods.Ransac,3,mask,500,.99);
        if(homography.Empty()||mask.Empty()) return null;
        var inliers=Cv2.CountNonZero(mask);
        if(inliers<16 || inliers<Math.Ceiling(matches.Length*.75)) return null;
        var accepted=matches.Where((_,i)=>mask.At<byte>(i)!=0).ToArray();
        if(accepted.Max(m=>af.X[m.QueryIdx])-accepted.Min(m=>af.X[m.QueryIdx])<30 || accepted.Max(m=>af.Y[m.QueryIdx])-accepted.Min(m=>af.Y[m.QueryIdx])<30) return null;
        return -inliers;
    }
}

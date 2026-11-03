using System.Buffers.Binary;
using System.Text;
namespace Wormy.Core;
public static class CaptureMetadata
{
    public static bool IsCapture(string path)
    {
        if(Path.GetExtension(path).Equals(".jpg",StringComparison.OrdinalIgnoreCase)||Path.GetExtension(path).Equals(".jpeg",StringComparison.OrdinalIgnoreCase)) return JpegCapture(path);
        if(!Path.GetExtension(path).Equals(".png",StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
            Span<byte> header=stackalloc byte[8]; stream.ReadExactly(header);
            Span<byte> checksum=stackalloc byte[4];
            if(!header.SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) return false;
            var read=8;
            while(read<131072)
            {
                stream.ReadExactly(header); read+=8;
                var length=BinaryPrimitives.ReadUInt32BigEndian(header[..4]); var type=Encoding.ASCII.GetString(header[4..]);
                if(type is "IDAT" or "IEND" || length>65536 || read+length+4>131072) return false;
                var data=new byte[(int)length]; stream.ReadExactly(data); stream.ReadExactly(checksum); read+=data.Length+4;
                if(type is not ("tEXt" or "zTXt" or "iTXt")) continue;
                var text=Encoding.Latin1.GetString(data); var split=text.IndexOf('\0');
                if(split<0 || !text[..split].Equals("Software",StringComparison.OrdinalIgnoreCase)) continue;
                var start=split+1; var compressed=false;
                if(type=="zTXt") { if(start>=data.Length||data[start++]!=0) continue; compressed=true; }
                if(type=="iTXt")
                {
                    if(start+2>data.Length) continue; var flag=data[start++]; if(flag>1||data[start++]!=0) continue; compressed=flag==1;
                    for(var field=0;field<2;field++) { while(start<data.Length&&data[start]!=0) start++; start++; }
                    if(start>data.Length) continue;
                }
                var content=data[start..];
                if(compressed)
                {
                    using var source=new MemoryStream(content); using var decoder=new System.IO.Compression.ZLibStream(source,System.IO.Compression.CompressionMode.Decompress); using var output=new MemoryStream();
                    var buffer=new byte[512]; int count; while((count=decoder.Read(buffer))>0) { if(output.Length+count>4096) break; output.Write(buffer,0,count); }
                    if(output.Length>=4096) continue; content=output.ToArray();
                }
                if(KnownSoftware((type=="iTXt" ? Encoding.UTF8 : Encoding.Latin1).GetString(content))) return true;
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
        return false;
    }
    private static bool KnownSoftware(string text)
    {
        text=text.TrimEnd('\0').Trim();
        return new[] {"Snipping Tool","Snip & Sketch","ShareX","Greenshot","gnome-screenshot","Spectacle","screencapture"}.Any(name=>text.Equals(name,StringComparison.OrdinalIgnoreCase)||text.StartsWith(name+" ",StringComparison.OrdinalIgnoreCase));
    }
    private static bool JpegCapture(string path)
    {
        try
        {
            using var stream=File.OpenRead(path); if(stream.ReadByte()!=255||stream.ReadByte()!=216) return false;
            var budget=0;
            while(budget<131072)
            {
                var prefix=stream.ReadByte(); var marker=stream.ReadByte(); if(prefix!=255||marker<0||marker is 218 or 217) return false;
                var high=stream.ReadByte(); var low=stream.ReadByte(); if(high<0||low<0) return false; var size=(high<<8|low)-2; if(size<0||budget+size>131072) return false;
                var data=new byte[size]; stream.ReadExactly(data); budget+=size+4;
                if(marker!=225||data.Length<14||!data.AsSpan(0,6).SequenceEqual("Exif\0\0"u8)) continue;
                var little=data[6]==73&&data[7]==73; if(!little&&!(data[6]==77&&data[7]==77)) continue;
                uint U32(int i)=>little ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(i,4)) : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i,4));
                ushort U16(int i)=>little ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(i,2)) : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(i,2));
                var offset=(long)U32(10)+6; if(offset<14||offset+2>data.Length) continue; var entries=U16((int)offset);
                for(var index=0;index<Math.Min((int)entries,512);index++)
                {
                    var entry=offset+2+12L*index; if(entry+12>data.Length) break; var i=(int)entry;
                    if(U16(i)!=0x0131||U16(i+2)!=2) continue; var count=U32(i+4); if(count==0||count>4096) continue;
                    var position=count<=4 ? i+8 : (long)U32(i+8)+6; if(position<6||position+count>data.Length) continue;
                    if(KnownSoftware(Encoding.ASCII.GetString(data,(int)position,(int)count))) return true;
                }
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
        return false;
    }
}

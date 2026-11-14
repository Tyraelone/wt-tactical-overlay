using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace WtTactical {
    public class RecognitionCatalog {
        public int Schema {get;set;}
        public string GeneratedAt {get;set;}
        public List<MapTemplate> Templates {get;set;}
        public RecognitionCatalog(){Schema=1;Templates=new List<MapTemplate>();}
    }
    public class MapTemplate {
        public string MapId {get;set;}
        public string GroupId {get;set;}
        public double Aspect {get;set;}
        public double[] Cells {get;set;}
        public Viewport ReferenceBounds {get;set;}
        public static MapTemplate Create(string id,string group,Bitmap image){return new MapTemplate{MapId=id,GroupId=string.IsNullOrWhiteSpace(group)?id:group,Aspect=(double)image.Height/image.Width,Cells=IntegralImage.FromBitmap(image,256).Cells(0,0,256,(int)Math.Round(256.0*image.Height/image.Width),16)};}
    }
    public class ImageMatch {
        public string MapId {get;set;}
        public string GroupId {get;set;}
        public double Score {get;set;}
        public double X {get;set;}
        public double Y {get;set;}
        public double Width {get;set;}
        public double Height {get;set;}
    }
    public class RecognitionResult {
        public bool Accepted {get;set;}
        public ImageMatch Match {get;set;}
        public double RunnerUp {get;set;}
        public string Reason {get;set;}
    }
    public static class RecognitionPolicy {
        static string Family(ImageMatch match){return string.IsNullOrWhiteSpace(match.GroupId)?match.MapId:match.GroupId;}
        public static RecognitionResult Choose(IEnumerable<ImageMatch> matches){
            var sorted=matches.Where(m=>m!=null&&Viewport.Finite(m.Score)).OrderByDescending(m=>m.Score).ToList();
            if(sorted.Count==0)return new RecognitionResult{Reason="没有可用匹配"};
            var best=sorted[0];var second=sorted.FirstOrDefault(m=>Family(m)!=Family(best));double runner=second==null?0:second.Score;
            bool accepted=best.Score>=.94&&best.Score-runner>=.055;
            return new RecognitionResult{Accepted=accepted,Match=best,RunnerUp=runner,Reason=accepted?"图像匹配":"相似度不足或不同地形之间存在歧义"};
        }
    }
    internal class IntegralImage {
        public int Width,Height;
        readonly double[][] sums;
        IntegralImage(int width,int height,byte[] pixels,int stride){Width=width;Height=height;sums=new[]{new double[(width+1)*(height+1)],new double[(width+1)*(height+1)],new double[(width+1)*(height+1)]};
            for(int y=1;y<=height;y++)for(int x=1;x<=width;x++)for(int c=0;c<3;c++){int i=y*(width+1)+x;sums[c][i]=pixels[(y-1)*stride+(x-1)*4+c]+sums[c][i-1]+sums[c][i-width-1]-sums[c][i-width-2];}}
        public static IntegralImage FromBitmap(Bitmap image,int width){int height=(int)Math.Round((double)width*image.Height/image.Width);height=Math.Max(16,height);using(var scaled=new Bitmap(width,height,PixelFormat.Format32bppArgb)){
                using(var graphics=Graphics.FromImage(scaled)){graphics.InterpolationMode=InterpolationMode.HighQualityBilinear;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;graphics.DrawImage(image,new Rectangle(0,0,width,height));}
                var locked=scaled.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{var pixels=new byte[locked.Stride*height];Marshal.Copy(locked.Scan0,pixels,0,pixels.Length);return new IntegralImage(width,height,pixels,locked.Stride);}finally{scaled.UnlockBits(locked);}}}
        double Average(int c,int left,int top,int right,int bottom){var a=sums[c];int stride=Width+1;return(a[bottom*stride+right]-a[top*stride+right]-a[bottom*stride+left]+a[top*stride+left])/((right-left)*(bottom-top));}
        public double[] Cells(int x,int y,int w,int h,int n){var cells=new double[n*n*3];Fill(x,y,w,h,n,cells);return cells;}
        public void Fill(int x,int y,int w,int h,int n,double[] output){int index=0;for(int row=0;row<n;row++){int top=y+row*h/n,bottom=y+(row+1)*h/n;for(int col=0;col<n;col++){int left=x+col*w/n,right=x+(col+1)*w/n;for(int c=0;c<3;c++)output[index++]=Average(c,left,top,right,bottom);}}}
    }
    public static class MapRecognizer {
        class TemplateCandidate {
            public MapTemplate Template;
            public ImageMatch Match;
        }

        static string Family(MapTemplate template){return string.IsNullOrWhiteSpace(template.GroupId)?template.MapId:template.GroupId;}

        static List<TemplateCandidate> RefinementCandidates(IEnumerable<TemplateCandidate> candidates){
            var ordered=candidates.OrderByDescending(candidate=>candidate.Match.Score).ToList();
            var bestFamilies=ordered.GroupBy(candidate=>Family(candidate.Template)).Select(group=>group.First()).Take(6);
            var selected=new List<TemplateCandidate>();
            var seenTemplates=new HashSet<MapTemplate>();
            foreach(var candidate in ordered.Take(6).Concat(bestFamilies))
                if(seenTemplates.Add(candidate.Template))selected.Add(candidate);
            return selected;
        }

        static ImageMatch ToCanonicalReference(ImageMatch match,MapTemplate template){
            var reference=template.ReferenceBounds;
            if(reference==null)return match;
            double referenceWidth=reference.X1-reference.X0,referenceHeight=reference.Y1-reference.Y0;
            return new ImageMatch{
                MapId=match.MapId,GroupId=match.GroupId,Score=match.Score,
                X=match.X-reference.X0/referenceWidth*match.Width,
                Y=match.Y-reference.Y0/referenceHeight*match.Height,
                Width=match.Width/referenceWidth,Height=match.Height/referenceHeight
            };
        }

        static double[] Vector(MapTemplate template,int n){if(template.Cells==null||template.Cells.Length!=16*16*3)throw new ArgumentException("识别模板格式无效。");var result=new double[n*n*3];int block=16/n;
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)for(int c=0;c<3;c++){double total=0;for(int yy=0;yy<block;yy++)for(int xx=0;xx<block;xx++)total+=template.Cells[((y*block+yy)*16+x*block+xx)*3+c];result[(y*n+x)*3+c]=total/(block*block);}
            var mean=new double[3];for(int i=0;i<result.Length;i++)mean[i%3]+=result[i]/(n*n);double norm=0;for(int i=0;i<result.Length;i++){result[i]-=mean[i%3];norm+=result[i]*result[i];}norm=Math.Sqrt(norm);if(norm<.001)return null;for(int i=0;i<result.Length;i++)result[i]/=norm;return result;
        }
        static double Score(double[] template,double[] cells){double a=0,b=0,c=0,squares=0,dot=0;for(int i=0;i<cells.Length;i+=3){a+=cells[i];b+=cells[i+1];c+=cells[i+2];squares+=cells[i]*cells[i]+cells[i+1]*cells[i+1]+cells[i+2]*cells[i+2];dot+=template[i]*cells[i]+template[i+1]*cells[i+1]+template[i+2]*cells[i+2];}
            double variance=squares-(a*a+b*b+c*c)/(cells.Length/3);return variance>1?dot/Math.Sqrt(variance):0;
        }
        static ImageMatch Search(IntegralImage image,MapTemplate template,int n,int minW,int maxW,int widthStep,int positionStep,int minX,int minY,int maxX,int maxY){
            var vector=Vector(template,n);var result=new ImageMatch{MapId=template.MapId,GroupId=template.GroupId,Score=-1};if(vector==null)return result;var cells=new double[n*n*3];
            for(int w=Math.Max(n,minW);w<=Math.Min(image.Width,maxW);w+=widthStep){int h=(int)Math.Round(w*template.Aspect);if(h<n||h>image.Height)continue;
                for(int y=Math.Max(0,minY);y<=Math.Min(image.Height-h,maxY);y+=positionStep)for(int x=Math.Max(0,minX);x<=Math.Min(image.Width-w,maxX);x+=positionStep){image.Fill(x,y,w,h,n,cells);double score=Score(vector,cells);if(score>result.Score){result.Score=score;result.X=(double)x/image.Width;result.Y=(double)y/image.Height;result.Width=(double)w/image.Width;result.Height=(double)h/image.Height;}}}
            return result;
        }
        public static RecognitionResult Recognize(byte[] bytes,RecognitionCatalog catalog){
            if(catalog==null||(catalog.Schema!=1&&catalog.Schema!=2)||catalog.Templates==null||catalog.Templates.Count==0)return new RecognitionResult{Reason="识别模板库缺失"};
            using(var stream=new MemoryStream(bytes))using(var bitmap=new Bitmap(stream)){
                if(bitmap.Width<64||bitmap.Height<64||bitmap.Width>8192||bitmap.Height>8192)return new RecognitionResult{Reason="底图尺寸不受支持"};
                var coarse=IntegralImage.FromBitmap(bitmap,128);var candidates=new List<TemplateCandidate>();
                foreach(var template in catalog.Templates){
                    if(template.ReferenceBounds!=null)template.ReferenceBounds.Validate();
                    if(template.Aspect<.4||template.Aspect>2.5)continue;
                    candidates.Add(new TemplateCandidate{Template=template,Match=Search(coarse,template,8,24,128,4,3,0,0,coarse.Width,coarse.Height)});
                }
                var fine=IntegralImage.FromBitmap(bitmap,320);var refined=new List<ImageMatch>();
                foreach(var candidate in RefinementCandidates(candidates)){
                    var best=candidate.Match;var template=candidate.Template;
                    int w=(int)Math.Round(best.Width*fine.Width),x=(int)Math.Round(best.X*fine.Width),y=(int)Math.Round(best.Y*fine.Height);
                    var match=Search(fine,template,16,w-11,w+11,1,1,x-10,y-10,x+10,y+10);
                    refined.Add(ToCanonicalReference(match,template));
                }
                return RecognitionPolicy.Choose(refined);
            }
        }
    }
}

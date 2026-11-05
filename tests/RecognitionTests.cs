using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using WtTactical;

class RecognitionTests {
    static int passed,failed;
    static void Check(string name,Action test){try{test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static void Assert(bool value){if(!value)throw new Exception("Assertion failed");}
    static Bitmap Board(int seed){var b=new Bitmap(256,256);using(var g=Graphics.FromImage(b)){var r=new Random(seed);for(int y=0;y<256;y+=8)for(int x=0;x<256;x+=8)using(var brush=new SolidBrush(Color.FromArgb(r.Next(30,220),r.Next(30,220),r.Next(30,220))))g.FillRectangle(brush,x,y,8,8);}return b;}
    static byte[] Bytes(Bitmap b){using(var s=new MemoryStream()){b.Save(s,ImageFormat.Png);return s.ToArray();}}
    static int Main(string[] args){
        Check("recognizes first-seen cropped map without a fingerprint binding",delegate{using(var full=Board(9))using(var cropped=full.Clone(new Rectangle(64,32,128,128),PixelFormat.Format32bppArgb))using(var wrong=Board(77)){var catalog=new RecognitionCatalog{Templates=new List<MapTemplate>{MapTemplate.Create("correct","terrain-a",cropped),MapTemplate.Create("wrong","terrain-b",wrong)}};var result=MapRecognizer.Recognize(Bytes(full),catalog);Assert(result.Accepted&&result.Match.MapId=="correct");Assert(Math.Abs(result.Match.X-.25)<.025&&Math.Abs(result.Match.Y-.125)<.025);Assert(Math.Abs(result.Match.Width-.5)<.025);}});
        Check("rejects an unknown map rather than selecting nearest",delegate{using(var a=Board(9))using(var b=Board(125)){var result=MapRecognizer.Recognize(Bytes(a),new RecognitionCatalog{Templates=new List<MapTemplate>{MapTemplate.Create("other","other",b)}});Assert(!result.Accepted);}});
        Check("similar different terrains remain ambiguous",delegate{var result=RecognitionPolicy.Choose(new[]{new ImageMatch{MapId="a",GroupId="a",Score=.97},new ImageMatch{MapId="b",GroupId="b",Score=.95}});Assert(!result.Accepted);});
        Check("missing terrain groups do not bypass ambiguity rejection",delegate{var result=RecognitionPolicy.Choose(new[]{new ImageMatch{MapId="suncity",GroupId=null,Score=.97},new ImageMatch{MapId="spaceport",GroupId=null,Score=.96}});Assert(!result.Accepted);});
        Check("same terrain variants do not compete as different maps",delegate{var result=RecognitionPolicy.Choose(new[]{new ImageMatch{MapId="a",GroupId="terrain",Score=.98},new ImageMatch{MapId="b",GroupId="terrain",Score=.97},new ImageMatch{MapId="c",GroupId="other",Score=.6}});Assert(result.Accepted&&result.Match.MapId=="a");});
        Check("weak match remains rejected",delegate{Assert(!RecognitionPolicy.Choose(new[]{new ImageMatch{MapId="a",GroupId="a",Score=.75}}).Accepted);});
        Check("observed natural-map false positive remains rejected",delegate{Assert(!RecognitionPolicy.Choose(new[]{new ImageMatch{MapId="secondbattleofelalmein",GroupId="desert",Score=.9006546943279684},new ImageMatch{MapId="other",GroupId="other",Score=.80}}).Accepted);});
        if(args.Length==3)Check("captured live map recognized against full reference catalog",delegate{var result=MapRecognizer.Recognize(File.ReadAllBytes(args[1]),JsonStore.Load<RecognitionCatalog>(args[0]));Console.WriteLine("LIVE_MATCH "+new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result));Assert(result.Accepted&&result.Match.MapId==args[2]);});
        Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;
    }
}

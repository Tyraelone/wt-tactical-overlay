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
    static void Close(double expected,double actual,double tolerance){if(Math.Abs(expected-actual)>tolerance)throw new Exception("Expected "+expected+", got "+actual);}
    static MapTemplate WithBounds(MapTemplate template,Viewport bounds){template.ReferenceBounds=bounds;return template;}
    static int Main(string[] args){
        Check("recognizes first-seen cropped map without a fingerprint binding",delegate{using(var full=Board(9))using(var cropped=full.Clone(new Rectangle(64,32,128,128),PixelFormat.Format32bppArgb))using(var wrong=Board(77)){var catalog=new RecognitionCatalog{Templates=new List<MapTemplate>{MapTemplate.Create("correct","terrain-a",cropped),MapTemplate.Create("wrong","terrain-b",wrong)}};var result=MapRecognizer.Recognize(Bytes(full),catalog);Assert(result.Accepted&&result.Match.MapId=="correct");Assert(Math.Abs(result.Match.X-.25)<.025&&Math.Abs(result.Match.Y-.125)<.025);Assert(Math.Abs(result.Match.Width-.5)<.025);}});
        Check("rejects an unknown map rather than selecting nearest",delegate{using(var a=Board(9))using(var b=Board(125)){var result=MapRecognizer.Recognize(Bytes(a),new RecognitionCatalog{Templates=new List<MapTemplate>{MapTemplate.Create("other","other",b)}});Assert(!result.Accepted);}});
        Check("similar different terrains remain ambiguous",delegate{var result=RecognitionPolicy.Choose(new[]{new ImageMatch{MapId="a",GroupId="a",Score=.97},new ImageMatch{MapId="b",GroupId="b",Score=.95}});Assert(!result.Accepted);});
        Check("missing terrain groups do not bypass ambiguity rejection",delegate{var result=RecognitionPolicy.Choose(new[]{new ImageMatch{MapId="suncity",GroupId=null,Score=.97},new ImageMatch{MapId="spaceport",GroupId=null,Score=.96}});Assert(!result.Accepted);});
        Check("same terrain variants do not compete as different maps",delegate{var result=RecognitionPolicy.Choose(new[]{new ImageMatch{MapId="a",GroupId="terrain",Score=.98},new ImageMatch{MapId="b",GroupId="terrain",Score=.97},new ImageMatch{MapId="c",GroupId="other",Score=.6}});Assert(result.Accepted&&result.Match.MapId=="a");});
        Check("weak match remains rejected",delegate{Assert(!RecognitionPolicy.Choose(new[]{new ImageMatch{MapId="a",GroupId="a",Score=.75}}).Accepted);});
        Check("observed natural-map false positive remains rejected",delegate{Assert(!RecognitionPolicy.Choose(new[]{new ImageMatch{MapId="secondbattleofelalmein",GroupId="desert",Score=.9006546943279684},new ImageMatch{MapId="other",GroupId="other",Score=.80}}).Accepted);});
        Check("schema one full-map template preserves its original reference frame",delegate{
            using(var image=Board(9)){
                var result=MapRecognizer.Recognize(Bytes(image),new RecognitionCatalog{Schema=1,Templates=new List<MapTemplate>{MapTemplate.Create("legacy","terrain",image)}});
                Assert(result.Accepted&&result.Match.MapId=="legacy");Close(0,result.Match.X,.01);Close(0,result.Match.Y,.01);Close(1,result.Match.Width,.01);Close(1,result.Match.Height,.01);
            }
        });
        Check("schema two templates without bounds use the full reference frame",delegate{
            using(var image=Board(9)){
                var result=MapRecognizer.Recognize(Bytes(image),new RecognitionCatalog{Schema=2,Templates=new List<MapTemplate>{MapTemplate.Create("current","terrain",image)}});
                Assert(result.Accepted&&result.Match.MapId=="current");Close(0,result.Match.X,.01);Close(1,result.Match.Width,.01);
            }
        });
        Check("local template match expands to its canonical reference outside the query",delegate{
            using(var full=Board(9))using(var crop=full.Clone(new Rectangle(64,32,128,128),PixelFormat.Format32bppArgb)){
                var template=WithBounds(MapTemplate.Create("canonical","terrain",crop),new Viewport{X0=.25,Y0=.125,X1=.75,Y1=.625});
                var result=MapRecognizer.Recognize(Bytes(crop),new RecognitionCatalog{Schema=1,Templates=new List<MapTemplate>{template}});
                Assert(result.Accepted);Close(-.5,result.Match.X,.02);Close(-.25,result.Match.Y,.02);Close(2,result.Match.Width,.02);Close(2,result.Match.Height,.02);
            }
        });
        Check("serialized schema two bounds retain independent x and y reference spans",delegate{
            using(var full=Board(9))using(var crop=full.Clone(new Rectangle(64,32,128,128),PixelFormat.Format32bppArgb)){
                var template=WithBounds(MapTemplate.Create("canonical","terrain",crop),new Viewport{X0=.2,Y0=.1,X1=.6,Y1=.7});
                var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();
                var catalog=serializer.Deserialize<RecognitionCatalog>(serializer.Serialize(new RecognitionCatalog{Schema=2,Templates=new List<MapTemplate>{template}}));
                var result=MapRecognizer.Recognize(Bytes(full),catalog);
                Assert(result.Accepted);Close(0,result.Match.X,.04);Close(1.0/24,result.Match.Y,.04);Close(1.25,result.Match.Width,.04);Close(5.0/6,result.Match.Height,.04);
            }
        });
        Check("duplicate map IDs refine the matching image rather than the first template",delegate{
            using(var image=Board(9))using(var wrong=Board(77)){
                var result=MapRecognizer.Recognize(Bytes(image),new RecognitionCatalog{Templates=new List<MapTemplate>{MapTemplate.Create("same-map","terrain",wrong),MapTemplate.Create("same-map","terrain",image)}});
                Assert(result.Accepted&&result.Match.MapId=="same-map");Close(1,result.Match.Score,.001);
            }
        });
        Check("many templates from one family cannot hide an ambiguous competing family",delegate{
            using(var image=Board(9)){
                var templates=new List<MapTemplate>();
                for(int i=0;i<7;i++)templates.Add(MapTemplate.Create("variant-"+i,"family-a",image));
                templates.Add(MapTemplate.Create("competing-map","family-b",image));
                var result=MapRecognizer.Recognize(Bytes(image),new RecognitionCatalog{Templates=templates});
                Assert(!result.Accepted&&result.RunnerUp>.99);
            }
        });
        Check("invalid reference bounds cannot produce an accepted location",delegate{
            using(var image=Board(9))foreach(var bounds in new[]{
                new Viewport{X0=double.NaN},new Viewport{Y1=double.PositiveInfinity},
                new Viewport{X0=.5,X1=.5},new Viewport{Y0=.5,Y1=.5001},
                new Viewport{X0=-17},new Viewport{X1=18}}){
                bool rejected=false;
                try{MapRecognizer.Recognize(Bytes(image),new RecognitionCatalog{Templates=new List<MapTemplate>{WithBounds(MapTemplate.Create("invalid","terrain",image),bounds)}});}catch(ArgumentException){rejected=true;}
                Assert(rejected);
            }
        });
        if(args.Length==3)Check("captured live map recognized against full reference catalog",delegate{var result=MapRecognizer.Recognize(File.ReadAllBytes(args[1]),JsonStore.Load<RecognitionCatalog>(args[0]));Console.WriteLine("LIVE_MATCH "+new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result));Assert(result.Accepted&&result.Match.MapId==args[2]);});
        Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;
    }
}

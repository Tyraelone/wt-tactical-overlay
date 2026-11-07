using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Controls;
using WtTactical;
class UiCalibrationTests {
    static int passed,failed;
    static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static object Call(object o,string name,params object[] values){return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,values);}
    static void Assert(bool value){if(!value)throw new Exception("Assertion failed");}
    static void Check(string name,Action<ControlPanel,Profile,Profile> test){
        string root=Path.Combine(Path.GetTempPath(),"wt-auto-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);ControlPanel panel=null;
        try{var library=new MapLibrary{Schema=1,Maps=new List<MapDefinition>{new MapDefinition{Id="abandonedfactory",Name="Test",English="Test",Pins=new List<Pin>(),Routes=new List<Route>()}}};
            var a=new Profile{MapId="abandonedfactory",PositionVerified=true,MapLayoutStamp="layout-a",Left=10,Top=20,Width=300,Height=300};
            var b=new Profile{MapId="abandonedfactory",PositionVerified=true,MapLayoutStamp="layout-b",Left=500,Top=600,Width=360,Height=300,View=new Viewport{X0=.25,Y0=.1,X1=.75,Y1=.9}};
            var data=new UserData();data.Profiles.Add(b);data.Profiles.Add(a);data.Bindings.Add(new Binding{Fingerprint="layout-a",MapId=a.MapId,View=a.View.Copy(),SourceBounds=new Viewport()});data.Bindings.Add(new Binding{Fingerprint="layout-b",MapId=b.MapId,View=b.View.Copy(),SourceBounds=new Viewport()});JsonStore.Save(Path.Combine(root,"user-data","annotations.json"),data);
            panel=new ControlPanel(library,root,true);var loaded=(UserData)Field(panel,"user");a=loaded.Profiles[1];b=loaded.Profiles[0];string display=(string)Call(panel,"DisplayStamp");a.DisplayStamp=b.DisplayStamp=display;
            typeof(ControlPanel).GetField("snapshot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(panel,new MapSnapshot{Fingerprint="layout-b",Session="1",ImageFingerprint="image-b",GridView=b.View.Copy()});
            test(panel,a,b);passed++;Console.WriteLine("PASS "+name);
        }catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+(e.InnerException??e).Message);}
        finally{if(panel!=null)panel.Close();string folder=Path.Combine(root,"user-data");foreach(string file in new[]{"annotations.json","annotations.json.bak"}){string path=Path.Combine(folder,file);if(File.Exists(path))File.Delete(path);}if(Directory.Exists(folder))Directory.Delete(folder);Directory.Delete(root);}
    }
    static void Arm(ControlPanel p,Profile profile){Call(p,"Arm",new MapSnapshot{Fingerprint=profile.MapLayoutStamp,Session="1",GridView=profile.View.Copy()},profile);}
    static void Set(object o,string name,object value){o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);}
    static byte[] TestMapImage(int width,int height){
        var frame=System.Windows.Media.Imaging.BitmapSource.Create(width,height,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[width*height*4],width*4);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frame));
        using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}
    }
    static TacticalCanvas PrepareExternalCanvas(ControlPanel panel){
        var type=typeof(ControlPanel).Assembly.GetType("WtTactical.ExternalMapWindow");
        if(type==null)throw new Exception("The second-screen map window is missing");
        var window=(System.Windows.Window)Activator.CreateInstance(type);
        Set(panel,"externalWindow",window);
        return (TacticalCanvas)type.GetField("Canvas").GetValue(window);
    }
    [STAThread] static int Main(){
        Check("external map remains available when native calibration cannot be saved",delegate(ControlPanel p,Profile a,Profile b){
            var current=(MapSnapshot)Field(p,"snapshot");
            current.Image=TestMapImage(200,100);
            var canvas=PrepareExternalCanvas(p);
            Set(p,"saveAllowed",false);
            string notice=(string)Call(p,"UpdateNativeMap",current,b.MapId);
            Assert(notice.Contains("主屏")&&Field(p,"armedSession")==null);
            Call(p,"RefreshExternalMap");
            Assert(canvas.Map!=null&&canvas.BackgroundImage!=null&&!p.Overlay.IsVisible);
        });
        Check("external map projects the clipped live grid without screen calibration",delegate(ControlPanel p,Profile a,Profile b){
            var data=(UserData)Field(p,"user");a.PositionVerified=b.PositionVerified=false;
            data.Bindings[1].SourceBounds=new Viewport{X0=.2,Y0=.2,X1=.6,Y1=.6};
            Set(p,"snapshot",new MapSnapshot{Fingerprint="layout-b",ImageFingerprint="image-b",Session="1",Image=TestMapImage(200,100),GridView=new Viewport{X0=-.2,Y0=.1,X1=.8,Y1=.9}});
            var canvas=PrepareExternalCanvas(p);
            Call(p,"RefreshExternalMap");
            var image=(System.Windows.Media.Imaging.BitmapSource)canvas.BackgroundImage;
            Assert(canvas.Map!=null&&canvas.Map.Id==b.MapId);
            Assert(image.PixelWidth==160&&image.PixelHeight==80);
            Assert(Math.Abs(canvas.Width/canvas.Height-2)<.000001);
            Assert(Math.Abs(canvas.View.X0+.5)<.000001&&Math.Abs(canvas.View.X1-1.5)<.000001);
            Assert(Math.Abs(canvas.View.Y0+.25)<.000001&&Math.Abs(canvas.View.Y1-1.75)<.000001);
            Assert(!p.Overlay.IsVisible&&data.ScreenCalibration==null);
        });
        Check("external map clears stale layers during a map change and disconnect",delegate(ControlPanel p,Profile a,Profile b){
            var data=(UserData)Field(p,"user");
            data.Markers.Add(new PinRecord{MapId=b.MapId,Marker=new Pin{Id="personal",X=.5,Y=.5,Kind="sniper"}});
            var canvas=PrepareExternalCanvas(p);
            var current=(MapSnapshot)Field(p,"snapshot");current.Image=TestMapImage(200,100);
            ((CheckBox)Field(p,"danger")).IsChecked=false;
            Call(p,"RefreshCanvases");
            Assert(canvas.Map!=null&&canvas.Custom.Count==1&&!canvas.ShowDanger);
            Set(p,"snapshot",new MapSnapshot{Fingerprint="unknown",ImageFingerprint="other-image",Session="2",Image=TestMapImage(100,100),GridView=new Viewport()});
            Call(p,"RefreshExternalMap");
            Assert(canvas.Map==null&&canvas.Custom.Count==0&&canvas.BackgroundImage!=null);
            Set(p,"snapshot",null);
            Call(p,"RefreshExternalMap");
            Assert(canvas.Map==null&&canvas.BackgroundImage==null);
        });
        Check("saving screen position while awaiting recognition preserves enable intent",delegate(ControlPanel p,Profile a,Profile b){Set(p,"snapshot",null);Call(p,"Toggle");Call(p,"BindCurrent");Assert((bool)Field(p,"requested")&&!p.Overlay.IsVisible);Assert(((UserData)Field(p,"user")).ScreenCalibration.PositionVerified);Call(p,"Toggle");Assert(!(bool)Field(p,"requested"));});
        Check("lost live state has a readiness message instead of a low level projection exception",delegate(ControlPanel p,Profile a,Profile b){Set(p,"snapshot",null);bool guarded=false;try{Call(p,"ReadProfile");}catch(TargetInvocationException e){guarded=e.InnerException is InvalidOperationException&&!e.InnerException.Message.Contains("缺少原生网格");}Assert(guarded);Assert(((UserData)Field(p,"user")).Bindings.Count==2);});
        Check("screen position saves before any live map exists",delegate(ControlPanel p,Profile a,Profile b){Set(p,"snapshot",null);Call(p,"ApplyProfile");var data=(UserData)Field(p,"user");Assert(data.ScreenCalibration!=null&&data.ScreenCalibration.PositionVerified&&data.ScreenCalibration.Left==10);Assert(data.Profiles.Count==2&&!p.Overlay.IsVisible);});
        Check("enable waits without projecting an unavailable map and can be cancelled",delegate(ControlPanel p,Profile a,Profile b){Set(p,"snapshot",null);Call(p,"Toggle");Assert((bool)Field(p,"requested")&&!p.Overlay.IsVisible);Assert(((TextBlock)Field(p,"status")).Text.Contains("等待"));Call(p,"Toggle");Assert(!(bool)Field(p,"requested")&&!p.Overlay.IsVisible);});
        Check("missing source location preserves saved map profiles while saving screen position",delegate(ControlPanel p,Profile a,Profile b){var data=(UserData)Field(p,"user");data.Bindings[1].SourceBounds=null;Call(p,"ApplyProfile");Assert(data.ScreenCalibration!=null&&data.ScreenCalibration.Left==10);Assert(data.Profiles.Count==2&&b.Left==500&&b.View.X0==.25);Assert(!p.Overlay.IsVisible);});
        Check("a new map adopts the verified global screen rectangle before recognition",delegate(ControlPanel p,Profile a,Profile b){var library=(MapLibrary)Field(p,"library");var map=new MapDefinition{Id="new-map",Name="New",English="New",Pins=new List<Pin>(),Routes=new List<Route>()};library.Maps.Add(map);var data=(UserData)Field(p,"user");data.ScreenCalibration=CalibrationPolicy.ScreenCopy(b);var select=(ComboBox)Field(p,"mapSelect");select.ItemsSource=library.Maps;select.SelectedItem=map;Assert(((TextBox)Field(p,"left")).Text=="500");Assert(((Profile)Call(p,"ReadScreenProfile")).PositionVerified);Assert(!p.Overlay.IsVisible);});
        Check("native grid preview aligns extended reference coordinates",delegate(ControlPanel p,Profile a,Profile b){var user=(UserData)Field(p,"user");user.Bindings[1].SourceBounds=new Viewport{X0=.2,Y0=.2,X1=.6,Y1=.6};var frame=System.Windows.Media.Imaging.BitmapSource.Create(200,200,96,96,System.Windows.Media.PixelFormats.Bgra32,null,new byte[200*200*4],800);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(frame));byte[] bytes;using(var stream=new MemoryStream()){encoder.Save(stream);bytes=stream.ToArray();}var current=new MapSnapshot{Fingerprint="layout-b",ImageFingerprint="img",Image=bytes,GridView=b.View.Copy()};var canvas=(TacticalCanvas)Field(p,"preview");canvas.View=new Viewport{X0=-2,Y0=-2,X1=3,Y1=3};Call(p,"SetMatchedPreview",current,b.MapId);Assert(Math.Abs(canvas.View.X0-.125)<.000001);Assert(Math.Abs(canvas.View.X1-1.375)<.000001);var image=(System.Windows.Media.Imaging.BitmapSource)canvas.BackgroundImage;Assert(image.PixelWidth==100&&image.PixelHeight==160);Assert(canvas.ShowDanger&&canvas.ShowConditional);});
        Check("legacy binding does not block automatic sourced map recognition",delegate(ControlPanel p,Profile a,Profile b){var library=(MapLibrary)Field(p,"library");library.Maps[0].Status="compatibility_alias";Assert(Call(p,"KnownBinding",new MapSnapshot{Fingerprint="layout-b"})==null);WtTactical.Validation.User((UserData)Field(p,"user"),library);});
        Check("default selected map adopts exact live calibration",delegate(ControlPanel p,Profile a,Profile b){Assert(((TextBox)Field(p,"left")).Text=="10");Arm(p,b);Assert(((TextBox)Field(p,"left")).Text=="500");Assert(((TextBox)Field(p,"x0")).Text=="0.25");});
        Check("same map layout transition adopts new fields and preserves ongoing edits",delegate(ControlPanel p,Profile a,Profile b){Arm(p,a);Arm(p,b);Assert(((TextBox)Field(p,"width")).Text=="360");((TextBox)Field(p,"left")).Text="777";Arm(p,b);Assert(((TextBox)Field(p,"left")).Text=="777");bool rejected=false;try{Call(p,"Toggle");}catch(TargetInvocationException e){rejected=e.InnerException is InvalidOperationException;}Assert(rejected);Assert(!p.Overlay.IsVisible);});
        Check("returning to a prior layout uses latest verified screen rectangle",delegate(ControlPanel p,Profile a,Profile b){var data=(UserData)Field(p,"user");data.Profiles.Clear();data.Profiles.Add(a);data.Profiles.Add(b);var result=(Profile)Call(p,"AutomaticProfile",new MapSnapshot{Fingerprint="layout-a",Session="2",GridView=a.View.Copy()},a.MapId);Assert(result.Left==500&&result.Width==360);Assert(result.View.X0==0&&result.View.X1==1);});
        Check("captured rectangle proof cannot authorize later edited fields",delegate(ControlPanel p,Profile a,Profile b){a.PositionVerified=false;var capture=(Profile)Call(p,"ReadScreenProfile");typeof(ControlPanel).GetField("capturedScreen",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(p,capture);Assert(((Profile)Call(p,"ReadScreenProfile")).PositionVerified);((TextBox)Field(p,"left")).Text="999";Assert(!((Profile)Call(p,"ReadScreenProfile")).PositionVerified);});
        Check("screen only saving consumes capture proof and rejects later rectangle edits",delegate(ControlPanel p,Profile a,Profile b){a.PositionVerified=false;var capture=(Profile)Call(p,"ReadScreenProfile");Set(p,"capturedScreen",capture);((MapSnapshot)Field(p,"snapshot")).GridView=null;Call(p,"ApplyProfile");Assert(((UserData)Field(p,"user")).ScreenCalibration.PositionVerified);Assert(Field(p,"capturedScreen")==null);((TextBox)Field(p,"left")).Text="123";Assert(!((Profile)Call(p,"ReadScreenProfile")).PositionVerified);});
        Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed");return failed==0?0:1;
    }
}

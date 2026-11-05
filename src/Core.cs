using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Linq;
using System.Text;

namespace WtTactical {
    public class Pin {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Title { get; set; }
        public string Note { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Radius { get; set; }
        public string Source { get; set; }
        public string Conditions {get;set;}
        public string Evidence {get;set;}
        public string Context {get;set;}
    }
    public class Route {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Title { get; set; }
        public string Note { get; set; }
        public double[][] Points { get; set; }
        public string Source { get; set; }
        public string Conditions {get;set;}
        public string Context {get;set;}
    }
    public class TacticalZone {
        public string Id{get;set;}
        public string Geometry{get;set;}
        public string Title{get;set;}
        public string Note{get;set;}
        public string Reason{get;set;}
        public string Scope{get;set;}
        public string Side{get;set;}
        public string Conditions{get;set;}
        public string Evidence{get;set;}
        public string Source{get;set;}
        public string Context{get;set;}
        public string Kind{get;set;}
        public double[][] Points{get;set;}
        public double[][][] Polygons{get;set;}
    }
    public class MapDefinition {
        public string Id { get; set; }
        public string Name { get; set; }
        public string English { get; set; }
        public string[] LevelIds { get; set; }
        public string GuideUrl { get; set; }
        public string ReferenceImageUrl { get; set; }
        public string Status { get; set; }
        public List<Pin> Pins { get; set; }
        public List<Route> Routes { get; set; }
        public List<TacticalZone> Zones{get;set;}
        public List<string> Tactics{get;set;}
        public string[] Sources{get;set;}
        public string Revision{get;set;}
        public string[] Contexts{get;set;}
        public string DefaultContext{get;set;}
        public Viewport CoordinateBounds{get;set;}
        public MapDefinition(){Zones=new List<TacticalZone>();Tactics=new List<string>();}
        public override string ToString() { return Name + " · " + English; }
    }
    public class MapLibrary {
        public int Schema { get; set; }
        public string RetrievedAt { get; set; }
        public List<MapDefinition> Maps { get; set; }
    }
    public class Viewport {
        public double X0 { get; set; }
        public double Y0 { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public Viewport() { X1 = Y1 = 1; }
        public void Validate() {
            if(!Finite(X0)||!Finite(Y0)||!Finite(X1)||!Finite(Y1)||X0< -16||Y0< -16||X1>17||Y1>17||X1-X0<.001||Y1-Y0<.001)
                throw new ArgumentException("视野范围无效，右下角必须大于左上角。");
        }
        public static bool Finite(double n) { return !double.IsNaN(n)&&!double.IsInfinity(n); }
        void Dimensions(double x,double y,double width,double height) {
            Validate();
            if(!Finite(x)||!Finite(y)||!Finite(width)||!Finite(height)||width<=0||height<=0) throw new ArgumentException("无效坐标或尺寸。");
        }
        public double[] Project(double x, double y, double width, double height) {
            Dimensions(x,y,width,height);return new[]{(x-X0)/(X1-X0)*width,(y-Y0)/(Y1-Y0)*height};
        }
        public double[] Unproject(double x, double y, double width, double height) {
            Dimensions(x,y,width,height);return new[]{X0+x/width*(X1-X0),Y0+y/height*(Y1-Y0)};
        }
        public Viewport Copy() {return new Viewport{X0=X0,Y0=Y0,X1=X1,Y1=Y1};}
    }
    public class Binding {
        public string Fingerprint { get; set; }
        public string MapId { get; set; }
        public Viewport View { get; set; }
        public Viewport SourceBounds {get;set;}
    }
    public class BindingGate {
        public static bool CanConfirm(string expectedKey,string currentKey,Profile expected,Profile current,bool activationValid){
            if(!activationValid||string.IsNullOrWhiteSpace(expectedKey)||expectedKey!=currentKey||expected==null||current==null||expected.MapId!=current.MapId||expected.View==null||current.View==null)return false;
            return expected.DisplayStamp==current.DisplayStamp&&expected.MapLayoutStamp==current.MapLayoutStamp&&expected.Left==current.Left&&expected.Top==current.Top&&expected.Width==current.Width&&expected.Height==current.Height&&expected.View.X0==current.View.X0&&expected.View.Y0==current.View.Y0&&expected.View.X1==current.View.X1&&expected.View.Y1==current.View.Y1;
        }
        public static bool CanDisplay(string fingerprint,string session,string armedSession,string mapId,Viewport view,IEnumerable<Binding> bindings) {
            var binding=Match(fingerprint,bindings);if(binding==null||view==null||string.IsNullOrWhiteSpace(session)||armedSession!=fingerprint+":"+session||binding.MapId!=mapId)return false;
            try{view.Validate();}catch(ArgumentException){return false;}
            return Math.Abs(view.X0-binding.View.X0)<.000001&&Math.Abs(view.Y0-binding.View.Y0)<.000001&&Math.Abs(view.X1-binding.View.X1)<.000001&&Math.Abs(view.Y1-binding.View.Y1)<.000001;
        }
        public static Binding Match(string fingerprint, IEnumerable<Binding> bindings) {
            if(string.IsNullOrWhiteSpace(fingerprint)||bindings==null)return null;
            var found=bindings.Where(b=>b!=null&&b.Fingerprint==fingerprint).ToList();
            if(found.Count!=1||string.IsNullOrEmpty(found[0].MapId)||found[0].View==null)return null;
            try {found[0].View.Validate();}catch(ArgumentException){return null;}return found[0];
        }
    }
    public class ActivationGate {
        public int Generation {get;private set;}
        bool enabled,closed;
        public void SetEnabled(bool value){Generation++;enabled=value;}
        public void Close(){Generation++;closed=true;enabled=false;}
        public bool Accept(int capturedGeneration){return !closed&&enabled&&capturedGeneration==Generation;}
    }
    public class NumberedPin {public Pin Pin;public int Number;}
    public static class AnnotationLayout {
        public static List<NumberedPin> Visible(IEnumerable<Pin> pins,bool points,bool danger){return pins.Select((p,i)=>new NumberedPin{Pin=p,Number=i+1}).Where(p=>p.Pin.Kind=="danger"?danger:points).ToList();}
    }
    public static class TacticalPresentation {
        public static List<TacticalZone> VisibleZones(IEnumerable<TacticalZone> zones,bool danger,bool inference){return danger?zones.Where(z=>inference||z.Evidence!="source_inference").ToList():new List<TacticalZone>();}
        public static bool InContext(string context,string selected,string perspective){if(string.IsNullOrWhiteSpace(context))return true;var parts=context.Split('|');return (string.IsNullOrWhiteSpace(selected)||parts[0]==selected)&&(parts.Length<2||string.IsNullOrWhiteSpace(perspective)||parts[1]==perspective);}
        public static string Description(string title,string note,string conditions,string evidence,string source){return title+"\n"+note+(string.IsNullOrWhiteSpace(conditions)?"":"\n适用条件："+conditions)+(evidence=="source_inference"?"\n条件性风险参考，非实测火力范围。":"\n固定地图资料，非实时敌情。")+(string.IsNullOrWhiteSpace(source)?"":"\n来源："+source);}
    }
    public static class ZoneGeometry {
        static double Segment(double x,double y,double ax,double ay,double bx,double by){double dx=bx-ax,dy=by-ay,t=dx*dx+dy*dy>0?((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy):0;t=Math.Max(0,Math.Min(1,t));return Math.Sqrt(Math.Pow(x-ax-t*dx,2)+Math.Pow(y-ay-t*dy,2));}
        public static bool Hit(TacticalZone z,Viewport view,double x,double y,double w,double h,double tolerance){
            if(z.Geometry=="hotspot"){var p=view.Project(z.Points[0][0],z.Points[0][1],w,h);return Segment(x,y,p[0],p[1],p[0],p[1])<=Math.Max(10,tolerance)||Segment(x,y,p[0]-10,p[1]-10,p[0]-10,p[1]-10)<=8;}
            if(z.Geometry=="corridor"){for(int i=1;i<z.Points.Length;i++){var a=view.Project(z.Points[i-1][0],z.Points[i-1][1],w,h);var b=view.Project(z.Points[i][0],z.Points[i][1],w,h);if(Segment(x,y,a[0],a[1],b[0],b[1])<=tolerance)return true;}return false;}
            bool inside=false;foreach(var ring in z.Polygons){for(int i=0,j=ring.Length-1;i<ring.Length;j=i++){var a=view.Project(ring[i][0],ring[i][1],w,h);var b=view.Project(ring[j][0],ring[j][1],w,h);if(tolerance>0&&Segment(x,y,a[0],a[1],b[0],b[1])<=tolerance)return true;if((a[1]>y)!=(b[1]>y)&&x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0])inside=!inside;}}return inside;
        }
    }
    public class Profile {
        public string MapId { get; set; }
        public string DisplayStamp {get;set;}
        public string MapLayoutStamp {get;set;}
        public bool PositionVerified {get;set;}
        public Viewport View { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public Profile() { View = new Viewport(); Width = Height = 320; }
    }
    public static class CalibrationPolicy {
        public static Profile ScreenCopy(Profile p){return new Profile{MapId=p.MapId,DisplayStamp=p.DisplayStamp,PositionVerified=p.PositionVerified,Left=p.Left,Top=p.Top,Width=p.Width,Height=p.Height,View=new Viewport()};}
        public static bool SameScreen(Profile a,Profile b){return a!=null&&b!=null&&a.DisplayStamp==b.DisplayStamp&&Math.Abs(a.Left-b.Left)<.000001&&Math.Abs(a.Top-b.Top)<.000001&&Math.Abs(a.Width-b.Width)<.000001&&Math.Abs(a.Height-b.Height)<.000001;}
        public static bool SameGeometry(Profile a,Profile b){if(a==null||b==null||a.View==null||b.View==null)return false;return Math.Abs(a.Left-b.Left)<.000001&&Math.Abs(a.Top-b.Top)<.000001&&Math.Abs(a.Width-b.Width)<.000001&&Math.Abs(a.Height-b.Height)<.000001&&Math.Abs(a.View.X0-b.View.X0)<.000001&&Math.Abs(a.View.Y0-b.View.Y0)<.000001&&Math.Abs(a.View.X1-b.View.X1)<.000001&&Math.Abs(a.View.Y1-b.View.Y1)<.000001;}
        public static bool CanReuse(Profile profile,string displayStamp,string mapLayoutStamp=null){if(profile==null||!profile.PositionVerified||string.IsNullOrEmpty(displayStamp)||profile.DisplayStamp!=displayStamp||profile.View==null||(mapLayoutStamp!=null&&profile.MapLayoutStamp!=mapLayoutStamp))return false;try{profile.View.Validate();}catch(ArgumentException){return false;}return Viewport.Finite(profile.Left)&&Viewport.Finite(profile.Top)&&Viewport.Finite(profile.Width)&&Viewport.Finite(profile.Height)&&profile.Width>=80&&profile.Height>=80&&profile.Width<=3000&&profile.Height<=3000;}
    }
    public static class NativeGridMapping {
        static void Pair(double[] p){if(p==null||p.Length!=2||!Viewport.Finite(p[0])||!Viewport.Finite(p[1]))throw new ArgumentException("原生地图网格字段无效。");}
        public static Viewport FromWorld(double[] min,double[] max,double[] zero,double[] size){Pair(min);Pair(max);Pair(zero);Pair(size);double sx=max[0]-min[0],sy=max[1]-min[1];if(sx<=0||sy<=0||size[0]<=0||size[1]<=0)throw new ArgumentException("原生地图网格跨度无效。");
            var v=new Viewport{X0=(zero[0]-min[0])/sx,Y0=(max[1]-zero[1])/sy,X1=(zero[0]+size[0]-min[0])/sx,Y1=(max[1]-zero[1]+size[1])/sy};v.Validate();return v;}
        public static Viewport ToReference(Viewport native,Viewport source){if(native==null||source==null)throw new ArgumentException("缺少原生网格或资料图定位。");native.Validate();source.Validate();if(source.X0<0||source.Y0<0||source.X1>1||source.Y1>1)throw new ArgumentException("资料图定位超出底图。");double w=source.X1-source.X0,h=source.Y1-source.Y0;
            var v=new Viewport{X0=(native.X0-source.X0)/w,Y0=(native.Y0-source.Y0)/h,X1=(native.X1-source.X0)/w,Y1=(native.Y1-source.Y0)/h};v.Validate();return v;}
    }
    public class UserData {
        public Profile ScreenCalibration {get;set;}
        public List<PinRecord> Markers { get; set; }
        public List<RouteRecord> Routes {get;set;}
        public List<Binding> Bindings { get; set; }
        public List<Profile> Profiles { get; set; }
        public UserData() { Markers = new List<PinRecord>(); Routes=new List<RouteRecord>(); Bindings = new List<Binding>(); Profiles = new List<Profile>(); }
    }
    public class RouteRecord {public string MapId{get;set;} public Route Path{get;set;}}
    public class PinRecord {
        public string MapId { get; set; }
        public Pin Marker { get; set; }
    }
    public static class JsonStore {
        static JavaScriptSerializer Serializer() {return new JavaScriptSerializer{MaxJsonLength=16*1024*1024,RecursionLimit=80};}
        public static T Load<T>(string file) {
            try {var result=Serializer().Deserialize<T>(File.ReadAllText(file,Encoding.UTF8));if(result==null)throw new ArgumentException("JSON 为 null");return result;}
            catch(Exception e) {if(e is IOException||e is UnauthorizedAccessException)throw;throw new InvalidDataException("JSON 损坏或格式不兼容："+file,e);}
        }
        public static void Save<T>(string file, T value) {
            string directory=Path.GetDirectoryName(Path.GetFullPath(file));Directory.CreateDirectory(directory);
            string temp=Path.Combine(directory,Path.GetFileName(file)+"."+Guid.NewGuid().ToString("N")+".tmp");
            try {File.WriteAllText(temp,Serializer().Serialize(value),new UTF8Encoding(false));
                if(File.Exists(file)) File.Replace(temp,file,file+".bak",true);else File.Move(temp,file);
            } finally {if(File.Exists(temp))File.Delete(temp);}
        }
    }
    public static class Validation {
        static void Unit(double n) {if(!Viewport.Finite(n)||n<0||n>1)throw new ArgumentException("地图坐标必须在 0–1 内。");}
        static void Coordinate(double x,double y,Viewport bounds){if(bounds==null){Unit(x);Unit(y);return;}bounds.Validate();if(!Viewport.Finite(x)||!Viewport.Finite(y)||x<bounds.X0-.000001||x>bounds.X1+.000001||y<bounds.Y0-.000001||y>bounds.Y1+.000001)throw new ArgumentException("坐标超出已注册参考图范围。");}
        public static void Marker(Pin pin) {
            if(pin==null||string.IsNullOrWhiteSpace(pin.Id))throw new ArgumentException("无效标记。");
            Unit(pin.X);Unit(pin.Y);if(!Viewport.Finite(pin.Radius)||pin.Radius<0||pin.Radius>.5)throw new ArgumentException("无效区域半径。");
        }
        public static void User(UserData data,MapLibrary library){
            if(data==null||data.Markers==null||data.Routes==null||data.Profiles==null||data.Bindings==null)throw new ArgumentException("用户配置字段缺失。");var ids=new HashSet<string>(library.Maps.Select(m=>m.Id));
            if(data.ScreenCalibration!=null&&!CalibrationPolicy.CanReuse(data.ScreenCalibration,data.ScreenCalibration.DisplayStamp))throw new ArgumentException("当前屏幕校准无效。");
            var markerIds=new HashSet<string>();foreach(var record in data.Markers){if(record==null||!ids.Contains(record.MapId))throw new ArgumentException("用户标注地图无效。");Marker(record.Marker);if(!markerIds.Add(record.MapId+":"+record.Marker.Id))throw new ArgumentException("用户标注 ID 重复。");}
            foreach(var record in data.Routes){if(record==null||!ids.Contains(record.MapId))throw new ArgumentException("用户路线地图无效。");ValidateRoute(record.Path);if(!markerIds.Add(record.MapId+":"+record.Path.Id))throw new ArgumentException("用户路线 ID 重复。");}
            var profiles=new HashSet<string>();foreach(var profile in data.Profiles){if(profile==null||!ids.Contains(profile.MapId)||!profiles.Add(profile.MapId+":"+profile.DisplayStamp+":"+profile.MapLayoutStamp)||profile.View==null)throw new ArgumentException("用户对齐配置无效。");profile.View.Validate();
                if(!Viewport.Finite(profile.Left)||!Viewport.Finite(profile.Top)||!Viewport.Finite(profile.Width)||!Viewport.Finite(profile.Height)||profile.Width<80||profile.Height<80||profile.Width>3000||profile.Height>3000||Math.Abs(profile.Left)>100000||Math.Abs(profile.Top)>100000)throw new ArgumentException("用户屏幕矩形无效。");}
            var bindings=new HashSet<string>();foreach(var binding in data.Bindings){if(binding==null||string.IsNullOrWhiteSpace(binding.Fingerprint)||!bindings.Add(binding.Fingerprint)||!ids.Contains(binding.MapId)||binding.View==null)throw new ArgumentException("用户地图绑定无效。");binding.View.Validate();if(binding.SourceBounds!=null)NativeGridMapping.ToReference(new Viewport(),binding.SourceBounds);}
        }
        public static void ValidateRoute(Route route){if(route==null||string.IsNullOrEmpty(route.Id)||route.Points==null||route.Points.Length<2)throw new ArgumentException("路线至少需要两个点。");foreach(var p in route.Points){if(p==null||p.Length!=2)throw new ArgumentException("无效路线坐标。");Unit(p[0]);Unit(p[1]);}}
        static void Points(double[][] points,int count,Viewport bounds){if(points==null||points.Length<count)throw new ArgumentException("区域几何点数不足。");foreach(var p in points){if(p==null||p.Length!=2)throw new ArgumentException("区域坐标无效。");Coordinate(p[0],p[1],bounds);}}
        public static void Zone(TacticalZone zone,Viewport bounds){if(zone==null||string.IsNullOrWhiteSpace(zone.Id))throw new ArgumentException("区域 ID 无效。");if(zone.Geometry=="polygon"){if(zone.Polygons==null||zone.Polygons.Length==0)throw new ArgumentException("区域边界缺失。");foreach(var ring in zone.Polygons)Points(ring,3,bounds);}else if(zone.Geometry=="corridor")Points(zone.Points,2,bounds);else if(zone.Geometry=="hotspot")Points(zone.Points,1,bounds);else throw new ArgumentException("不支持的区域几何。");}
        public static void Library(MapLibrary library) {
            if(library==null||library.Schema!=1||library.Maps==null)throw new ArgumentException("不支持的地图库。");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var map in library.Maps){
                if(map==null||string.IsNullOrWhiteSpace(map.Id)||!ids.Add(map.Id)||map.Pins==null||map.Routes==null||map.Zones==null||map.Tactics==null)throw new ArgumentException("地图 ID 重复或字段缺失。");
                if(map.CoordinateBounds!=null)map.CoordinateBounds.Validate();
                var markers=new HashSet<string>();foreach(var pin in map.Pins){if(map.CoordinateBounds==null)Marker(pin);else{if(pin==null||string.IsNullOrEmpty(pin.Id))throw new ArgumentException("标记无效。");Coordinate(pin.X,pin.Y,map.CoordinateBounds);if(!Viewport.Finite(pin.Radius)||pin.Radius<0||pin.Radius>.5)throw new ArgumentException("半径无效。");}if(!markers.Add(pin.Id))throw new ArgumentException("标记 ID 重复。");}
                foreach(var route in map.Routes){if(map.CoordinateBounds==null)ValidateRoute(route);else{if(route==null||string.IsNullOrEmpty(route.Id))throw new ArgumentException("路线无效。");Points(route.Points,2,map.CoordinateBounds);}if(!markers.Add(route.Id))throw new ArgumentException("路线 ID 重复。");}
                foreach(var zone in map.Zones){Zone(zone,map.CoordinateBounds);if(!markers.Add(zone.Id))throw new ArgumentException("区域 ID 重复。");}
            }
        }
    }
}

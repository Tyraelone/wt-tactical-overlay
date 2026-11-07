using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace WtTactical {
    public class TacticalCanvas : FrameworkElement {
        public MapDefinition Map;
        public List<Pin> Custom=new List<Pin>();
        public List<Route> CustomRoutes=new List<Route>();
        public List<double[]> DraftRoute=new List<double[]>();
        public Viewport View=new Viewport();
        public bool ShowPins=true,ShowRoutes=true,ShowDanger=true,ShowConditional=true,ShowStrategic=false,Labels=true,Grid=true,GridLines=true;
        public string ActiveContext,Perspective;
        public ImageSource BackgroundImage;
        public bool OutlineOnly;
        public event Action<double,double> Pick;
        readonly ToolTip tip=new ToolTip{MaxWidth=360,Placement=System.Windows.Controls.Primitives.PlacementMode.Mouse};
        object hover;
        internal void ClearHover(){tip.IsOpen=false;hover=null;}
        public TacticalCanvas() {
            ClipToBounds=true;MinWidth=50;MinHeight=50;ToolTip=tip;
            MouseMove+=delegate(object s,MouseEventArgs e){var pos=e.GetPosition(this);object found=null;string description=null;var nearest=VisiblePins().OrderBy(p=>Distance(p,pos)).FirstOrDefault();
                if(nearest!=null&&Distance(nearest,pos)<12){found=nearest;description=TacticalPresentation.Description(nearest.Title,nearest.Note,nearest.Conditions,nearest.Evidence,nearest.Source);}
                if(found==null){var z=VisibleZones().Reverse().FirstOrDefault(v=>ZoneGeometry.Hit(v,View,pos.X,pos.Y,ActualWidth,ActualHeight,6));if(z!=null){found=z;description=TacticalPresentation.Description(z.Title,z.Note,z.Conditions,z.Evidence,z.Source);}}
                if(found==null&&ShowRoutes&&Map!=null){var r=Map.Routes.Concat(CustomRoutes).FirstOrDefault(v=>InContext(v.Context)&&ZoneGeometry.Hit(new TacticalZone{Geometry="corridor",Points=v.Points},View,pos.X,pos.Y,ActualWidth,ActualHeight,5));if(r!=null){found=r;description=TacticalPresentation.Description(r.Title,r.Note,r.Conditions,null,r.Source);}}
                if(found!=null){if(hover!=found){hover=found;tip.Content=description;}tip.IsOpen=true;}else{hover=null;tip.IsOpen=false;}};
            MouseLeave+=delegate{tip.IsOpen=false;hover=null;};
            MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(Pick==null)return;var p=e.GetPosition(this);var q=View.Unproject(p.X,p.Y,ActualWidth,ActualHeight);if(q[0]>=0&&q[0]<=1&&q[1]>=0&&q[1]<=1)Pick(q[0],q[1]);};
        }
        bool InContext(string context){return TacticalPresentation.InContext(context,ActiveContext,Perspective);}
        IEnumerable<Pin> VisiblePins(){return (Map==null?Enumerable.Empty<Pin>():Map.Pins).Concat(Custom).Where(p=>InContext(p.Context)&&(p.Kind=="danger"?ShowDanger:ShowPins));}
        IEnumerable<TacticalZone> VisibleZones(){return Map==null?Enumerable.Empty<TacticalZone>():Map.Zones.Where(z=>InContext(z.Context)&&(ShowConditional||z.Evidence!="source_inference")&&(z.Kind=="strategic"?ShowStrategic:ShowDanger));}
        double Distance(Pin p,Point q){var v=View.Project(p.X,p.Y,Math.Max(ActualWidth,1),Math.Max(ActualHeight,1));return Math.Sqrt((v[0]-q.X)*(v[0]-q.X)+(v[1]-q.Y)*(v[1]-q.Y));}
        public static Color ColorFor(string kind){switch(kind){case "danger":return Color.FromRgb(255,88,93);case "sniper":return Color.FromRgb(255,208,91);case "hulldown":return Color.FromRgb(122,226,174);case "overwatch":return Color.FromRgb(126,206,255);case "protected":return Color.FromRgb(89,220,151);case "flank":return Color.FromRgb(255,197,90);case "assault":return Color.FromRgb(255,158,66);default:return Color.FromRgb(192,163,255);}}
        StreamGeometry Geometry(double[][] points,double w,double h,bool closed){var geometry=new StreamGeometry();using(var g=geometry.Open()){var first=View.Project(points[0][0],points[0][1],w,h);g.BeginFigure(new Point(first[0],first[1]),closed,closed);for(int i=1;i<points.Length;i++){var p=View.Project(points[i][0],points[i][1],w,h);g.LineTo(new Point(p[0],p[1]),true,false);}}geometry.Freeze();return geometry;}
        void DrawZone(DrawingContext dc,TacticalZone z,double w,double h){Color color=z.Kind=="strategic"?Color.FromRgb(255,205,94):ColorFor("danger");var edge=new SolidColorBrush(Color.FromArgb(190,color.R,color.G,color.B));var fill=new SolidColorBrush(Color.FromArgb(44,color.R,color.G,color.B));var pen=new Pen(edge,1.1);if(z.Evidence=="source_inference")pen.DashStyle=DashStyles.Dash;
            if(z.Geometry=="polygon"){var shape=new StreamGeometry{FillRule=FillRule.EvenOdd};using(var g=shape.Open()){foreach(var ring in z.Polygons){var first=View.Project(ring[0][0],ring[0][1],w,h);g.BeginFigure(new Point(first[0],first[1]),true,true);for(int i=1;i<ring.Length;i++){var p=View.Project(ring[i][0],ring[i][1],w,h);g.LineTo(new Point(p[0],p[1]),true,false);}}}shape.Freeze();dc.DrawGeometry(fill,pen,shape);}
            else if(z.Geometry=="corridor"){var shape=Geometry(z.Points,w,h,false);dc.DrawGeometry(null,new Pen(fill,10){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},shape);dc.DrawGeometry(null,new Pen(edge,1.5){DashStyle=DashStyles.Dash},shape);}
            else{var p=View.Project(z.Points[0][0],z.Points[0][1],w,h);dc.DrawLine(new Pen(edge,.9),new Point(p[0],p[1]),new Point(p[0]-10,p[1]-10));p[0]-=10;p[1]-=10;var triangle=new StreamGeometry();using(var g=triangle.Open()){g.BeginFigure(new Point(p[0],p[1]-8),true,true);g.LineTo(new Point(p[0]-7,p[1]+6),true,false);g.LineTo(new Point(p[0]+7,p[1]+6),true,false);}triangle.Freeze();dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(220,36,12,16)),pen,triangle);dc.DrawLine(new Pen(edge,1.7),new Point(p[0],p[1]-3),new Point(p[0],p[1]+1));dc.DrawEllipse(edge,null,new Point(p[0],p[1]+3.5),.8,.8);}}
        protected override void OnRender(DrawingContext dc){
            base.OnRender(dc);double w=ActualWidth,h=ActualHeight;if(w<1||h<1)return;
            if(OutlineOnly){dc.DrawRectangle(null,new Pen(Brushes.Cyan,2),new Rect(1,1,w-2,h-2));dc.DrawText(new FormattedText("校准边框：应与小地图内容边缘重合",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),12,Brushes.Cyan,VisualTreeHelper.GetDpi(this).PixelsPerDip),new Point(8,8));return;}
            if(Grid){dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(18,28,38)),null,new Rect(0,0,w,h));
                if(BackgroundImage!=null)dc.DrawImage(BackgroundImage,new Rect(0,0,w,h));
                if(GridLines){var grid=new Pen(new SolidColorBrush(Color.FromArgb(45,183,212,227)),1);for(int i=1;i<10;i++){dc.DrawLine(grid,new Point(w*i/10,0),new Point(w*i/10,h));dc.DrawLine(grid,new Point(0,h*i/10),new Point(w,h*i/10));}}}
            if(Map==null)return;
            dc.PushClip(new RectangleGeometry(new Rect(0,0,w,h)));
            foreach(var zone in VisibleZones().Where(z=>z.Geometry!="hotspot"))DrawZone(dc,zone,w,h);
            if(ShowRoutes)foreach(var route in Map.Routes.Concat(CustomRoutes).Where(r=>InContext(r.Context))){var geometry=Geometry(route.Points,w,h,false);
                var pen=new Pen(new SolidColorBrush(ColorFor(route.Kind)),2.2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb(190,8,13,20)),5),geometry);dc.DrawGeometry(null,pen,geometry);}
            foreach(var numbered in AnnotationLayout.Visible(Map.Pins.Concat(Custom),ShowPins,ShowDanger).Where(p=>InContext(p.Pin.Context))){var pin=numbered.Pin;int index=numbered.Number;var xy=View.Project(pin.X,pin.Y,w,h);var p=new Point(xy[0],xy[1]);Color color=ColorFor(pin.Kind);var brush=new SolidColorBrush(color);
                if(pin.Kind=="danger"){dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(45,color.R,color.G,color.B)),new Pen(brush,1.5),p,pin.Radius/(View.X1-View.X0)*w,pin.Radius/(View.Y1-View.Y0)*h);}
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(230,8,15,23)),new Pen(brush,2),p,6,6);dc.DrawEllipse(brush,null,p,2,2);
                if(Labels&&p.X>=0&&p.X<=w&&p.Y>=0&&p.Y<=h){var text=new FormattedText(index.ToString(),System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),11,brush,VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(215,8,15,23)),null,new Rect(p.X+8,p.Y-9,text.Width+6,17),3,3);dc.DrawText(text,new Point(p.X+11,p.Y-8));}}
            foreach(var zone in VisibleZones().Where(z=>z.Geometry=="hotspot"))DrawZone(dc,zone,w,h);
            if(DraftRoute.Count>0){Point? last=null;foreach(var xy in DraftRoute){var q=View.Project(xy[0],xy[1],w,h);var p=new Point(q[0],q[1]);if(last.HasValue)dc.DrawLine(new Pen(Brushes.White,2){DashStyle=DashStyles.Dash},last.Value,p);dc.DrawEllipse(Brushes.White,null,p,3,3);last=p;}}
            dc.Pop();
        }
    }
    public class OverlayWindow : Window {
        public readonly TacticalCanvas Canvas=new TacticalCanvas{Grid=false};
        public bool Passthrough=true;
        IntPtr handle;
        public OverlayWindow(){Title="WT 战术浮层";WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ResizeMode=ResizeMode.NoResize;Topmost=true;ShowActivated=false;ShowInTaskbar=false;Width=Height=320;Content=Canvas;
            SourceInitialized+=delegate{handle=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(handle).AddHook(Hook);SetPassthrough(true);};}
        IntPtr Hook(IntPtr h,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==0x84&&Passthrough){handled=true;return new IntPtr(-1);}return IntPtr.Zero;}
        public void SetPassthrough(bool pass){Passthrough=pass;Canvas.IsHitTestVisible=!pass;if(handle==IntPtr.Zero)return;long style=Native.GetWindowLongPtr(handle,Native.GWL_EXSTYLE).ToInt64()|Native.WS_EX_TOOLWINDOW;
            style=pass?(style|Native.WS_EX_TRANSPARENT|Native.WS_EX_NOACTIVATE):(style&~(Native.WS_EX_TRANSPARENT|Native.WS_EX_NOACTIVATE));Native.SetWindowLongPtr(handle,Native.GWL_EXSTYLE,new IntPtr(style));}
        public void Apply(Profile profile){Left=profile.Left;Top=profile.Top;Width=profile.Width;Height=profile.Height;Canvas.View=profile.View.Copy();Canvas.InvalidateVisual();}
    }
}

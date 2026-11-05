using System;
using WtTactical;
class GridMappingTests{
 static int pass,fail;static void Check(string n,Action a){try{a();pass++;Console.WriteLine("PASS "+n);}catch(Exception e){fail++;Console.WriteLine("FAIL "+n+": "+e.Message);}}
 static void Equal(double a,double b){if(Math.Abs(a-b)>.000001)throw new Exception("Expected "+a+", got "+b);}static void Assert(bool b){if(!b)throw new Exception("Assertion failed");}
 static int Main(){
 Check("Tunisia grid view uses world bounds and north origin",delegate{var v=NativeGridMapping.FromWorld(new[]{0.0,0},new[]{4096.0,4096},new[]{350.0,3698},new[]{3300.0,3300});Equal(.08544921875,v.X0);Equal(.09716796875,v.Y0);Equal(.89111328125,v.X1);Equal(.90283203125,v.Y1);});
 Check("reference subimage is inverted before native projection",delegate{var native=new Viewport{X0=.25,Y0=.25,X1=.75,Y1=.75};var reference=new Viewport{X0=.4,Y0=.1,X1=.6,Y1=.3};var v=NativeGridMapping.ToReference(native,reference);var p=v.Project(.5,.75,400,400);Equal(200,p[0]);Equal(0,p[1]);Assert(v.X0<0&&v.Y1>1);});
 Check("source point maps to hand-derived Tunisia screenshot position",delegate{var v=NativeGridMapping.FromWorld(new[]{0.0,0},new[]{4096.0,4096},new[]{350.0,3698},new[]{3300.0,3300});var p=v.Project(.4809,.4306,650,650);Equal(319.044896969697,p[0]);Equal(269.008921212121,p[1]);});
 Check("invalid grid does not silently become full-map view",delegate{bool rejected=false;try{NativeGridMapping.FromWorld(new[]{0.0,0},new[]{0.0,4096},new[]{0.0,0},new[]{3300.0,3300});}catch(ArgumentException){rejected=true;}Assert(rejected);});
 Check("default rectangle is not a verified calibration",delegate{Assert(!CalibrationPolicy.CanReuse(new Profile{MapId="test",DisplayStamp="d",MapLayoutStamp="m"},"d","m"));});
 Console.WriteLine("RESULT "+pass+" passed, "+fail+" failed");return fail==0?0:1;
 }
}

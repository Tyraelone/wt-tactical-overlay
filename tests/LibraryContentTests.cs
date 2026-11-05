using System;using System.Collections.Generic;using System.Linq;using WtTactical;
class LibraryContentTests{
 static int pass,fail;static void Check(string n,Action a){try{a();pass++;Console.WriteLine("PASS "+n);}catch(Exception e){fail++;Console.WriteLine("FAIL "+n+": "+e.Message);}}static void Assert(bool x){if(!x)throw new Exception("Assertion failed");}
 static int Main(string[] args){var library=JsonStore.Load<MapLibrary>(args[0]);
 Check("every sourced layout supplies sniper positions and risk information",delegate{foreach(var m in library.Maps.Where(m=>m.Pins.Count>0||m.Zones.Count>0)){Assert(m.Pins.Any(p=>p.Kind=="sniper"));Assert(m.Zones.Any(z=>z.Kind=="danger"));}});
 Check("built in risk geometry retains provenance and source limits",delegate{foreach(var m in library.Maps)foreach(var z in m.Zones){Assert(!string.IsNullOrWhiteSpace(z.Source)&&z.Source.StartsWith("https://")&&!string.IsNullOrWhiteSpace(z.Conditions)&&!string.IsNullOrWhiteSpace(z.Note));Assert(z.Evidence=="source_explicit"||z.Evidence=="source_inference");}});
 Check("each contextual annotation belongs to an available source plan",delegate{foreach(var m in library.Maps){var contexts=m.Pins.Select(p=>p.Context).Concat(m.Routes.Select(r=>r.Context)).Concat(m.Zones.Select(z=>z.Context));foreach(var context in contexts.Where(c=>!string.IsNullOrWhiteSpace(c)))Assert(m.Contexts.Contains(context.Split('|')[0]));Assert(m.Contexts.Contains(m.DefaultContext));}});
 Check("legacy map IDs preserve old personal notes without coordinate migration",delegate{foreach(var id in new[]{"avg_arctic","avg_ardennes_snow","avg_football_field","avg_training_ground","avg_israel","avg_soviet_range"}){var map=library.Maps.Single(m=>m.Id==id);Assert(map.Status=="compatibility_alias");var user=new UserData();user.Markers.Add(new PinRecord{MapId=id,Marker=new Pin{Id="old",X=.2,Y=.7,Kind="position"}});Validation.User(user,library);}});
 Check("ground zero does not inherit routes across destroyed central terrain",delegate{var map=library.Maps.Single(m=>m.Id=="ground-zero");Assert(map.Routes.Count==0&&map.Zones.All(z=>z.Geometry=="hotspot"));foreach(var pin in map.Pins)Assert(Math.Sqrt(Math.Pow(pin.X-.365,2)+Math.Pow(pin.Y-.545,2))>.22);});
 Console.WriteLine("RESULT "+pass+" passed, "+fail+" failed");return fail==0?0:1;
 }
}

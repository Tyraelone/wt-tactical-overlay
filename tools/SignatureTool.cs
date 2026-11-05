using System;
using System.Drawing;
using System.IO;
using System.Linq;
using WtTactical;
class SignatureTool {
    static int Main(string[] args){try{var library=JsonStore.Load<MapLibrary>(args[0]);var catalog=new RecognitionCatalog{GeneratedAt=DateTimeOffset.Now.ToString("o")};foreach(var map in library.Maps.Where(m=>!string.IsNullOrEmpty(m.ReferenceImageUrl))){using(var image=new Bitmap(Path.Combine(args[1],map.Id+".img"))){string group=map.LevelIds==null?null:map.LevelIds.FirstOrDefault(id=>!string.IsNullOrWhiteSpace(id));catalog.Templates.Add(MapTemplate.Create(map.Id,group??map.Id,image));}}JsonStore.Save(args[2],catalog);Console.WriteLine("TEMPLATES="+catalog.Templates.Count);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

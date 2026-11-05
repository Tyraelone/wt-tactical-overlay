using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace WtTactical {
    public class MapSnapshot {
        public string Fingerprint;
        public string ImageFingerprint;
        public string Session;
        public byte[] Image;
        public Viewport GridView;
    }
    public static class LiveMap {
        static async Task<byte[]> Read(string path,int max){
            var request=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:8111/"+path);request.Proxy=null;request.AllowAutoRedirect=false;request.Timeout=1600;request.ReadWriteTimeout=1600;
            using(var timeout=new System.Threading.CancellationTokenSource(1800))using(timeout.Token.Register(request.Abort))
            using(var response=(HttpWebResponse)await request.GetResponseAsync()){
                if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>max)throw new InvalidDataException("地图响应无效。");
                using(var input=response.GetResponseStream())using(var output=new MemoryStream()){
                    var buffer=new byte[16384];int n;while((n=await input.ReadAsync(buffer,0,buffer.Length))>0){if(output.Length+n>max)throw new InvalidDataException("地图响应过大。");output.Write(buffer,0,n);}return output.ToArray();}}
        }
        public static async Task<MapSnapshot> Poll(){
            var serializer=new JavaScriptSerializer();
            string before=Encoding.UTF8.GetString(await Read("map_info.json",128*1024));
            var info=serializer.Deserialize<Dictionary<string,object>>(before);
            if(info==null||!info.ContainsKey("valid")||!(info["valid"] is bool)||!(bool)info["valid"])throw new InvalidDataException("当前没有有效战斗地图。");
            if(!info.ContainsKey("map_generation"))throw new InvalidDataException("缺少 map_generation；不能安全确认当前局。");
            Viewport grid=null;try{grid=NativeGridMapping.FromWorld(serializer.ConvertToType<double[]>(info["map_min"]),serializer.ConvertToType<double[]>(info["map_max"]),serializer.ConvertToType<double[]>(info["grid_zero"]),serializer.ConvertToType<double[]>(info["grid_size"]));}catch(Exception e){throw new InvalidDataException("不能确定原生地图网格范围，已隐藏浮层。",e);}
            byte[] image=await Read("map.img",12*1024*1024);
            string after=Encoding.UTF8.GetString(await Read("map_info.json",128*1024));
            if(before!=after)throw new InvalidDataException("地图正在变化，等待稳定。");
            if(image.Length<100)throw new InvalidDataException("尚无有效底图。");
            var identity=new StringBuilder();foreach(var key in new[]{"map_min","map_max","grid_steps","grid_zero","grid_size"}){object value;if(info.TryGetValue(key,out value))identity.Append(key).Append(serializer.Serialize(value));}
            byte[] extra=Encoding.UTF8.GetBytes(identity.ToString());byte[] bytes=new byte[image.Length+extra.Length];Buffer.BlockCopy(image,0,bytes,0,image.Length);Buffer.BlockCopy(extra,0,bytes,image.Length,extra.Length);
            using(var sha=SHA256.Create())return new MapSnapshot{Fingerprint=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""),ImageFingerprint=BitConverter.ToString(sha.ComputeHash(image)).Replace("-",""),Session=serializer.Serialize(info["map_generation"]),Image=image,GridView=grid};
        }
    }
}

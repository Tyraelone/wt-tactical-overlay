using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WtTactical;
class AuditRecognition {
    static int Main(string[] args){var catalog=JsonStore.Load<RecognitionCatalog>(args[0]);var rows=new List<object>();int exact=0,family=0,unknown=0,falsePositive=0;
        foreach(var template in catalog.Templates){var bytes=File.ReadAllBytes(Path.Combine(args[1],template.MapId+".img"));var result=MapRecognizer.Recognize(bytes,catalog);bool same=result.Accepted&&result.Match.GroupId==template.GroupId;if(same){family++;if(result.Match.MapId==template.MapId)exact++;}else unknown++;
            var without=new RecognitionCatalog{Templates=catalog.Templates.Where(t=>t.GroupId!=template.GroupId).ToList()};var negative=MapRecognizer.Recognize(bytes,without);if(negative.Accepted)falsePositive++;
            rows.Add(new{Map=template.MapId,Accepted=result.Accepted,Selected=result.Match==null?null:result.Match.MapId,Score=result.Match==null?0:result.Match.Score,SameTerrain=same,LeaveFamilyOutAccepted=negative.Accepted,LeaveFamilyOutCandidate=negative.Match==null?null:negative.Match.MapId,LeaveFamilyOutScore=negative.Match==null?0:negative.Match.Score});
            Console.WriteLine(template.MapId+" self="+(same?"PASS":"REJECT")+" unseen="+(negative.Accepted?"FALSE-POSITIVE":"REJECT"));}
        JsonStore.Save(args[2],new{ReferenceCount=catalog.Templates.Count,ExactMatches=exact,TerrainMatches=family,SelfRejected=unknown,LeaveFamilyOutFalsePositives=falsePositive,Rows=rows});Console.WriteLine("TOTAL exact="+exact+" family="+family+" rejected="+unknown+" falsePositive="+falsePositive);return falsePositive==0?0:1;
    }
}

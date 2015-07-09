using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class LogTests
    {
        private static string MakeRecord(string name, string value, string old = null)
        {
            return LogBase.MakeRecord(name, value, old);
        }

        [TestMethod]
        public void TestUndo()
        {
            var service = new DanceMusicTester(
                new List<string>()
                {
                    @"SongId={89077001-49c3-47b1-8f0e-e9738ea4c89d}	.Create=	User=dwgray	Time=05/07/2015 15:35:11	Title=Against All Odds	Artist=Phil Collins	Tempo=64.0	Tag+=Castle Foxtrot:Dance|First Dance:Other|Wedding:Other	DanceRating=CFT+4	DanceRating=FXT+1	.Edit=	User=batch	Time=5/7/2015 4:04:41 PM	Title=Against All Odds (Take A Look At Me Now)	Length=204	Tag+=Rock:Music|Soundtrack:Music|soundtracks:Music|Soundtracks:Music	.Create=	user=bdlist	Time=04/16/2015 08:37:15	Title=Against All Odds	Artist=Phil Collins	Tempo=118.0	Length=204	Tag+=Rumba:Dance	DanceRating=RMB+3	DanceRating=LTN+1	.Edit=	User=batch-a	Time=4/16/2015 9:03:48 AM	Title=Against All Odds (Take A Look At Me Now)	Tag+=soundtracks:Music	.Edit=	User=batch-i	Time=4/16/2015 9:03:48 AM	Title=Against All Odds (Take a Look At Me Now) [Live]	Length=209	Tag+=Pop:Music|Rock:Music|Soundtrack:Music	.Edit=	User=batch-x	Time=4/16/2015 9:03:49 AM	Title=Against All Odds (Take a Look at Me Now)	Length=204	Tag+=Pop:Music|Soundtracks:Music	.Merge=f7466922-7863-4127-a2be-0dd958ff5cfc;36fc4289-7fa5-486c-b458-a10085cb95c3	User=batch	Time=06/10/2015 15:06:26	.Edit=	User=batch	Time=6/10/2015 3:06:27 PM	Title=Against All Odds (Take a Look at Me Now)	Tempo=118.0	Album:00=...Hits	Track:00=7	Purchase:00:AS=D:B001RHPJNS	Purchase:00:AA=D:B001RHPJBU	Purchase:00:XS=music.C7410000-0100-11DB-89CA-0019B92A3933	Album:01=Against All Odds (Original Motion Picture Soundtrack)	Track:01=1	Purchase:01:IS=320264177	Purchase:01:IA=320264133	Album:02=Now That's What I Call Movies	Track:02=6	Purchase:02:IS=956877195	Purchase:02:IA=956877184	Purchase:02:XS=music.605FBE08-0100-11DB-89CA-0019B92A3933	Album:03=NOW That's What I Call 80s Hits	Track:03=16	Purchase:03:IS=716299575	Purchase:03:IA=716298811	Purchase:03:XS=music.E307C506-0100-11DB-89CA-0019B92A3933	Album:04=Ultimate GRAMMY Collection: Classic Pop	Track:04=14	Purchase:04:IS=325080106	Purchase:04:IA=325080047	Purchase:04:XS=music.88EAE606-0100-11DB-89CA-0019B92A3933	Album:05=NOW That's What I Call 80s Hits (Deluxe Edition)	Track:05=12	Purchase:05:IS=905465095	Purchase:05:IA=905464988	Album:06=Serious Hits... Live!	Track:06=2	Purchase:06:IS=300972313	Purchase:06:IA=300972308	Purchase:06:XS=music.7772FF05-0100-11DB-89CA-0019B92A3933	Album:07=Against All Odds: Music From The Original Motion Picture Soundtrack	Track:07=1	Purchase:07:XS=music.A5E10400-0100-11DB-89CA-0019B92A3933	Album:08=Against All Odds",
                });

            // TODONEXT: Figure out why rs/us are stuttering!
            var rs = new string(LogBase.RecordSeparator, 1);
            var us = new string(LogBase.UnitSeparator, 1);
            //var temp = MakeRecord("Title", "Against All Odds", "Against All Odds (Take a Look at Me Now)");
            var logString =
                string.Join(LogBase.RecordString,
                "Music4Dance","7/5/2015 1:35:58 PM",".Edit","89077001-49c3-47b1-8f0e-e9738ea4c89d","PHILCOLLINS AGAINSTALLODDS",
                MakeRecord("Title","Against All Odds","Against All Odds (Take a Look at Me Now)"),
                MakeRecord("Artist","P. C. The Man","Phil Collins"), 
                MakeRecord("Tempo","120","118.00"), 
                MakeRecord("Length","210","204"), 
                MakeRecord("Album:09","Fake Album"), 
                MakeRecord("Track:09","7"),
                MakeRecord("Publisher:09","Gramamoto"), 
                "Album:00", 
                "Track:00", 
                MakeRecord("Tag+","First Dance:Other|Weak:Tempo|Wedding:Other"),
                MakeRecord("Tag+:FXT", "Very Slow:Tempo"));
            var log = new SongLog();

            var i = logString.IndexOf(rs + rs);
            var j = logString.IndexOf(us + us);
            log.Initialize(logString, service.Dms);
            service.Dms.RestoreFromLog(log,SongBase.EditCommand);

            var song = service.Dms.FindSong(new Guid("89077001-49c3-47b1-8f0e-e9738ea4c89d"));
            Assert.IsNotNull(song);

            const string expected = @"SongId={89077001-49c3-47b1-8f0e-e9738ea4c89d}	.Create=	User=dwgray	Time=05/07/2015 15:35:11	Title=Against All Odds	Artist=Phil Collins	Tempo=64.0	Tag+=Castle Foxtrot:Dance|First Dance:Other|Wedding:Other	DanceRating=CFT+4	DanceRating=FXT+1	.Edit=	User=batch	Time=5/7/2015 4:04:41 PM	Title=Against All Odds (Take A Look At Me Now)	Length=204	Tag+=Rock:Music|Soundtrack:Music|soundtracks:Music|Soundtracks:Music	.Create=	user=bdlist	Time=04/16/2015 08:37:15	Title=Against All Odds	Artist=Phil Collins	Tempo=118.0	Length=204	Tag+=Rumba:Dance	DanceRating=RMB+3	DanceRating=LTN+1	.Edit=	User=batch-a	Time=4/16/2015 9:03:48 AM	Title=Against All Odds (Take A Look At Me Now)	Tag+=soundtracks:Music	.Edit=	User=batch-i	Time=4/16/2015 9:03:48 AM	Title=Against All Odds (Take a Look At Me Now) [Live]	Length=209	Tag+=Pop:Music|Rock:Music|Soundtrack:Music	.Edit=	User=batch-x	Time=4/16/2015 9:03:49 AM	Title=Against All Odds (Take a Look at Me Now)	Length=204	Tag+=Pop:Music|Soundtracks:Music	.Merge=f7466922-7863-4127-a2be-0dd958ff5cfc;36fc4289-7fa5-486c-b458-a10085cb95c3	User=batch	Time=06/10/2015 15:06:26	.Edit=	User=batch	Time=6/10/2015 3:06:27 PM	Title=Against All Odds (Take a Look at Me Now)	Tempo=118.0	Album:00=...Hits	Track:00=7	Purchase:00:AS=D:B001RHPJNS	Purchase:00:AA=D:B001RHPJBU	Purchase:00:XS=music.C7410000-0100-11DB-89CA-0019B92A3933	Album:01=Against All Odds (Original Motion Picture Soundtrack)	Track:01=1	Purchase:01:IS=320264177	Purchase:01:IA=320264133	Album:02=Now That's What I Call Movies	Track:02=6	Purchase:02:IS=956877195	Purchase:02:IA=956877184	Purchase:02:XS=music.605FBE08-0100-11DB-89CA-0019B92A3933	Album:03=NOW That's What I Call 80s Hits	Track:03=16	Purchase:03:IS=716299575	Purchase:03:IA=716298811	Purchase:03:XS=music.E307C506-0100-11DB-89CA-0019B92A3933	Album:04=Ultimate GRAMMY Collection: Classic Pop	Track:04=14	Purchase:04:IS=325080106	Purchase:04:IA=325080047	Purchase:04:XS=music.88EAE606-0100-11DB-89CA-0019B92A3933	Album:05=NOW That's What I Call 80s Hits (Deluxe Edition)	Track:05=12	Purchase:05:IS=905465095	Purchase:05:IA=905464988	Album:06=Serious Hits... Live!	Track:06=2	Purchase:06:IS=300972313	Purchase:06:IA=300972308	Purchase:06:XS=music.7772FF05-0100-11DB-89CA-0019B92A3933	Album:07=Against All Odds: Music From The Original Motion Picture Soundtrack	Track:07=1	Purchase:07:XS=music.A5E10400-0100-11DB-89CA-0019B92A3933	Album:08=Against All Odds	.Edit=	User=Music4Dance	Time=07/05/2015 13:35:58	Title=Against All Odds	Artist=P. C. The Man	Tempo=120.0	Length=210	Album:09=Fake Album	Track:09=7	Publisher:09=Gramamoto	Album:00=	Track:00=	Tag+=First Dance:Other|Weak:Tempo|Wedding:Other	Tag+:FXT=Very Slow:Tempo";
            var actual = song.Serialize(new string[] {});
            Assert.AreEqual(expected,actual);
        }
    }
}

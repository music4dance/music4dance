using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class EditTests
    {
        [TestMethod]
        public void Merge()
        {
            var service = new DanceMusicTester(
                new List<string>()
                {
                    @"SongId={2ea20b99-b2f2-4139-ba22-ad84a73ed8f3}	.Create=	User=SalsaSwingBallroom	Time=3/17/2014 5:44:03 PM	Title=You Should Be Dancing	Artist=The Bee Gees	Tempo=124.0	Album:0=The Bee Gees - Their Greatest Hits	DanceRating=HST+5	User=batch	Time=10/9/2014 11:00:37 AM	DanceRating=SWG+3	User=batch	Time=10/9/2014 12:09:19 PM	DanceRating=CSG+4	DanceRating=WCS+4	DanceRating=LHP+4	User=SalsaSwingBallroom	Time=11/20/2014 11:32:37 AM	Tag+=Hustle:Dance	.FailedLookup=-:0", 
                    @"SongId={80c8c7d8-486c-4169-bb5e-0e0091cfe435}	.Create=	User=ithaca	Time=9/30/2014 10:55:53 AM	Title=You Should Be Dancin	Artist=Bee Gees	Tempo=124.0	DanceRating=HST+5	User=batch	Time=10/1/2014 3:16:26 PM	User=ithaca	Time=11/20/2014 11:31:18 AM	Tag+=Hustle:Dance	User=batch	Time=11/20/2014 11:31:18 AM	Tag+=Pop:Music|Rock:Music	DanceRating=LHP+4	DanceRating=WCS+4	DanceRating=CSG+4	Time=10/9/2014 12:08:44 PM	User=batch	DanceRating=SWG+3	Time=10/9/2014 11:00:08 AM	User=batch	Purchase:01:AS=D:B001KQLYIA	Purchase:01:AA=D:B001KQE5PE	Purchase:01:IS=294932501	Purchase:01:IA=294932492	Purchase:01:XS=music.01C52301-0100-11DB-89CA-0019B92A3933	Track:01=9	Album:01=Number Ones	Length=258	Title=You Should Be Dancing	.Edit=	User=batch-a	Time=12/11/2014 10:49:28 AM	Length=256	Album:02=Saturday Night Fever [The Original Movie Soundtrack]	Track:02=13	Purchase:02:AS=D:B00122S818	Purchase:02:AA=D:B00122KBV8	Album:03=Children Of The World	Track:03=1	Purchase:03:AS=D:B00124SGOA	Purchase:03:AA=D:B00124LFR0	Album:04=The Ultimate Bee Gees	Track:04=1	Purchase:04:AS=D:B002TSKCLS	Purchase:04:AA=D:B002TSKCKY	Tag+=adult-contemporary-pop:Music|pop:Music	.Edit=	User=batch-i	Time=12/11/2014 10:49:31 AM	Length=253	Album:03=Children of the World	Purchase:03:IS=270905494	Purchase:03:IA=270905436	Purchase:04:IS=336672046	Purchase:04:IA=336671732	Album:05=Saturday Night Fever (The Original Movie Soundtrack) [Remastered]	Track:05=13	Purchase:05:IS=263509379	Purchase:05:IA=263508370	Album:06=Despicable Me (Original Motion Picture Soundtrack)	Track:06=9	Purchase:06:IS=379630704	Purchase:06:IA=379630607	Album:07=One Night Only (Live)	Track:07=24	Purchase:07:IS=270899203	Purchase:07:IA=270899149	Tag+=Pop:Music|Rock:Music|Soundtrack:Music	.Edit=	User=batch-x	Time=12/11/2014 10:49:32 AM	Length=258	Album:03=Children Of The World	Purchase:03:XS=music.E145BF00-0100-11DB-89CA-0019B92A3933	Purchase:04:XS=music.EFFE0802-0100-11DB-89CA-0019B92A3933	Album:05=Saturday Night Fever: The Original Movie Soundtrack (Remastered)	Purchase:05:XS=music.577CAD00-0100-11DB-89CA-0019B92A3933	Purchase:07:XS=music.8144BF00-0100-11DB-89CA-0019B92A3933	Album:08=Despicable Me	Track:08=9	Purchase:08:XS=music.33F76806-0100-11DB-89CA-0019B92A3933	Tag+=R&B / Soul:Music|Rock:Music	.FailedLookup=-:0"
                });

            var songs = new List<Song>
            {
                service.Dms.FindSong(new Guid("{2ea20b99-b2f2-4139-ba22-ad84a73ed8f3}")),
                service.Dms.FindSong(new Guid("{80c8c7d8-486c-4169-bb5e-0e0091cfe435}"))
            };

            // TODONEXT: test for mergesongs
            var song = service.Dms.MergeSongs(
                service.Dms.FindUser("batch"),
                songs,
                @"You Should Be Dancing",
                @"Bee Gees",
                124,
                258,
                "1",
                new HashSet<string>()
                {
                    "AlbumList_0",
                    "AlbumList_1",
                    "AlbumList_3",
                    "AlbumList_4",
                    "AlbumList_5",
                    "AlbumList_6",
                    "AlbumList_7",
                    "AlbumList_8",
                    "AlbumList_9"
                });

            var result = DanceMusicTester.ReplaceTime(song.Serialize(new string[] { SongBase.NoSongId }));
            var expected = DanceMusicTester.ReplaceTime(MergeResult);

            Assert.AreEqual(expected,result);
        }

        //[TestMethod]
        //public void Edit()
        //{
        //}

        private const string MergeResult = @".Merge=2ea20b99-b2f2-4139-ba22-ad84a73ed8f3;80c8c7d8-486c-4169-bb5e-0e0091cfe435	User=batchTime=00/00/0000 0:00:00 PM	Title=You Should Be Dancing	Artist=Bee Gees	Tempo=124.0	Length=258	Album:00=Number Ones	Track:00=9	Purchase:00:AS=D:B001KQLYIA	Purchase:00:AA=D:B001KQE5PE	Purchase:00:IS=294932501	Purchase:00:IA=294932492	Purchase:00:XS=music.01C52301-0100-11DB-89CA-0019B92A3933	Album:01=The Bee Gees - Their Greatest Hits	Album:02=Children Of The World	Track:02=1	Purchase:02:AS=D:B00124SGOA	Purchase:02:AA=D:B00124LFR0	Purchase:02:IS=270905494	Purchase:02:IA=270905436	Purchase:02:XS=music.E145BF00-0100-11DB-89CA-0019B92A3933	Album:03=The Ultimate Bee Gees	Track:03=1	Purchase:03:AS=D:B002TSKCLS	Purchase:03:AA=D:B002TSKCKY	Purchase:03:IS=336672046	Purchase:03:IA=336671732	Purchase:03:XS=music.EFFE0802-0100-11DB-89CA-0019B92A3933	Album:04=Saturday Night Fever: The Original Movie Soundtrack (Remastered)	Track:04=13	Purchase:04:IS=263509379	Purchase:04:IA=263508370	Purchase:04:XS=music.577CAD00-0100-11DB-89CA-0019B92A3933	Album:05=Despicable Me (Original Motion Picture Soundtrack)	Track:05=9	Purchase:05:IS=379630704	Purchase:05:IA=379630607	Album:06=One Night Only (Live)	Track:06=24	Purchase:06:IS=270899203	Purchase:06:IA=270899149	Purchase:06:XS=music.8144BF00-0100-11DB-89CA-0019B92A3933	Album:07=Despicable Me	Track:07=9	Purchase:07:XS=music.33F76806-0100-11DB-89CA-0019B92A3933	User=SalsaSwingBallroom	.Edit=	User=SalsaSwingBallroomTime=00/00/0000 0:00:00 PM	Tag+=Hustle:Dance	User=ithaca	User=batch-a	User=batch-i	User=batch-x	.Edit=	User=ithacaTime=00/00/0000 0:00:00 PM	Tag+=Hustle:Dance	.Edit=	User=batchTime=00/00/0000 0:00:00 PM	Tag+=Pop:Music|Rock:Music	.Edit=	User=batch-aTime=00/00/0000 0:00:00 PM	Tag+=adult-contemporary-pop:Music|pop:Music	.Edit=	User=batch-iTime=00/00/0000 0:00:00 PM	Tag+=Pop:Music|Rock:Music|Soundtrack:Music	.Edit=	User=batch-xTime=00/00/0000 0:00:00 PM	Tag+=R&B / Soul:Music|Rock:Music	DanceRating=HST+10	DanceRating=SWG+6	DanceRating=CSG+8	DanceRating=WCS+8	DanceRating=LHP+8";
    }
}



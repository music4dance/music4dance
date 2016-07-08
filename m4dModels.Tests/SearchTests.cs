using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class SearchTests
    {
        [TestMethod]
        public void LoadSearchRecord()
        {
            var service = new DanceMusicTester(
                new List<string>
                {
                    @"SongId={60853549-C634-4F22-A820-4DAEF48262B2}	.Create=	User=dwgray	Time=3/17/2014 5:45:15 PM	Title=A Song	Artist=Me",
                    @"SongId={34f612f4-feb9-4f19-900d-59671007a28a}	.Create=	User=dwgray	Time=3/17/2014 5:45:15 PM	Title=Where There's A Heartache	Artist=Pat Boone	Tempo=87.0	Album:0=Ballroom Stars Vol. 1	DanceRating=WLZ+5	User=batch	Time=5/21/2014 3:33:11 PM	Title=Where There's A Heartache	Length=140	Album:1=Diamond Master Series - Pat Boone	Track:1=19	Purchase:1:XS=music.21D6A006-0100-11DB-89CA-0019B92A3933	PromoteAlbum:1=	User=batch	Time=5/21/2014 9:04:22 PM	User=dwgray	Time=11/20/2014 11:31:56 AM	Tag+=Waltz:Dance	User=batch	Time=11/20/2014 11:31:56 AM	Tag+=Vocal:Music	DanceRating=SWZ+4	Time=10/9/2014 12:09:03 PM	User=batch	Time=9/4/2014 8:11:38 PM	User=batch	Time=9/4/2014 8:11:38 PM	User=dwgray	Time=9/4/2014 8:06:35 PM	User=batch	Time=9/4/2014 8:06:35 PM	User=dwgray	Purchase:1:IA=267630650	Purchase:1:IS=267633542	Album:1=Diamond Master Series: Pat Boone	Length=141	Title=Where There's a Heartache	.Edit=	User=batch-a	Time=12/10/2014 9:14:48 PM	Length=137	Album:02=A Wonderful Time	Track:02=19	Purchase:02:AS=D:B0037JF5FQ	Purchase:02:AA=D:B0037JF538	Album:03=Love Letters In The Sand - Greatest Hits	Track:03=19	Purchase:03:AS=D:B002C9936O	Purchase:03:AA=D:B002C8Z9P4	Album:04=Love Letters In The Sand	Track:04=17	Purchase:04:AS=D:B00476CRL8	Purchase:04:AA=D:B00476CQVO	Album:05=Love Letters	Track:05=10	Purchase:05:AS=D:B0029PYHKI	Purchase:05:AA=D:B0029Q064I	Album:06=Jambalaya	Track:06=9	Purchase:06:AS=D:B001BHHVXA	Purchase:06:AA=D:B001BHJQZ6	Tag+=blues:Music|Opera:Music|pop:Music	.Edit=	User=batch-i	Time=12/10/2014 9:14:48 PM	Title=Where There's A Heartache (Stereo Version / Remastered)	Length=142	Purchase:03:IS=318974720	Purchase:03:IA=318974579	Purchase:05:IS=314546087	Purchase:05:IA=314545795	Purchase:06:IS=282897207	Purchase:06:IA=282897046	Album:07=April Love	Track:07=19	Purchase:07:IS=139114159	Purchase:07:IA=139109687	Album:08=A Wonderful Time (Re-Recorded Versions)	Track:08=19	Purchase:08:IS=355227331	Purchase:08:IA=355226956	Tag+=Blues:Music|Pop:Music|Rock:Music|Vocal:Music	.Edit=	User=batch-x	Time=12/10/2014 9:14:49 PM	Title=Where There's A Heartache	Length=141	Album:09=Love Letters In The Sand - Greatest Hits (Remastered Version)	Track:09=19	Purchase:09:XS=music.3921D801-0100-11DB-89CA-0019B92A3933	Tag+=Pop:Music	.Edit=	User=batch	Time=01/11/2016 18:31:51	Purchase:02:IS=355227331	Purchase:02:IA=355226956	Purchase:03:XS=music.3921D801-0100-11DB-89CA-0019B92A3933	Album:08=	Track:08=	Album:09=	Track:09=	OrderAlbums=1,0,2,3,4,5,6,7	.Edit=	User=batch-s	Time=01/15/2016 21:10:51	Purchase:08:SS=5TQJrT719esjXjKd9kF3X2	Purchase:08:SA=6NhPEHochkiPebsSGflxow	OrderAlbums=1,0,2,3,4,5,6,7,8	.FailedLookup=-:0	.Edit=	User=batch-s	Time=02/08/2016 22:29:50	Sample=.	OrderAlbums=1,0,2,3,4,5,6,7	.Edit=	User=batch-s	Time=02/17/2016 16:35:15	Sample=.",
                    @"SongId={42a17a7a-b2be-46bc-9276-f205f270a7d7}	.Merge=5519eddd-69e5-4551-842b-08d8d70c6866;7e5ade72-9953-4272-81ba-128c7bf53bb1;4c553e08-42b1-416e-b6de-7b46ef8caaf9;a3ed8e44-8d64-4a50-8c33-8b93f06838b1;b9ed520d-3290-4ad6-885e-9e3415a5dc48;e133c815-9782-4a1f-b764-bab4009c6126	User=dwgray	Time=10/20/2015 23:55:00	.Create=	User=DWTS	Time=10/20/2015 23:26:33	Title=You're the One That I Want	Artist=John Travolta & Olivia Newton-John	Tag+=Jazz:Dance	Tag+=DWTS:Other|Episode 6:Other|Season 21:Other|United States:Other	DanceRating=JAZ+3	Tag+:JAZ=Emma:Other|Hayes:Other	.Create=	User=JuliaS	Time=6/7/2014 9:38:55 PM	Title=You're the One That I Want	Artist=John Travolta & Olivia Newton	DanceRating=QST+5	User=batch	Time=6/7/2014 9:57:06 PM	Title=You're The One That I Want	Artist=John Travolta	Length=204	PromoteAlbum:00=	User=batch	Time=6/27/2014 9:03:48 PM	Title=You're the One That I Want	Artist=John Travolta & Olivia Newton-John	Length=205	PromoteAlbum:01=	User=JuliaS	Time=9/4/2014 3:13:23 PM	User=batch	Time=9/4/2014 3:13:25 PM	User=batch	Time=10/9/2014 11:00:08 AM	DanceRating=FXT+3	User=JuliaS	Time=11/20/2014 11:29:33 AM	Tag+=QuickStep:Dance	User=batch	Time=11/20/2014 11:29:33 AM	Tag+=Pop:Music	.Edit=	User=batch-i	Time=12/11/2014 12:12:11 PM	.Create=	User=AdamT	Time=04/16/2015 08:26:27	Title=You're The One that I Want (Original Version)	Artist=Olivia Newton	Tempo=216.0	Length=171	Tag+=QuickStep:Dance	DanceRating=QST+4	DanceRating=FXT+1	Tag+:QST=Contemporary:Style	.Create=	User=OliviaL	Time=10/1/2014 6:07:44 PM	Title=You're The One That I Want	Artist=Grease Soundtrack	Tempo=212.0	DanceRating=JSW+5	User=batch	Time=10/9/2014 11:00:31 AM	DanceRating=SWG+3	User=batch	Time=10/9/2014 12:09:12 PM	DanceRating=BBA+4	DanceRating=JSW+4	User=OliviaL	Time=11/20/2014 11:32:19 AM	Tag+=Jump Swing:Dance	.Edit=	User=batch	Time=4/23/2015 4:56:59 PM	DanceRating=CST+4	.Create=	User=dwgray	OwnerHash=1341511032	Time=1/12/2015 12:59:07 PM	Title=You're The One That I Want	Artist=John Travolta	Length=169	.Create=	User=DanielQ	Time=08/23/2015 20:51:26	Title=You're The One That I Want	Artist=from Grease	Tag+=First Dance:Other|Hustle:Dance|Wedding:Other	DanceRating=HST+3	DanceRating=SWG+1	.Edit=	User=dwgray	Time=10/20/2015 23:55:01	Title=You're the One That I Want	Artist=John Travolta & Olivia Newton-John	Tempo=216.0	Length=205	Album:00=Grease (Soundtrack from the Motion Picture) [Deluxe Edition]	Track:00=12	Purchase:00:IS=2558219	Purchase:00:IA=2558222	Album:01=Grease: 25th Anniversary Deluxe Edition	Track:01=36	Purchase:01:XS=music.DD511200-0100-11DB-89CA-0019B92A3933	Album:02=Magic: The Very Best Of Olivia Newton-John	Track:02=9	.Edit=	User=dwgray	Time=10/20/2015 23:58:35	Tempo=210.0	Tag+=Balboa:Dance|Charleston:Dance|Jump Swing:Dance|QuickStep:Dance|Slow Foxtrot:Dance	DanceRating=BBA+3	DanceRating=CST+3	DanceRating=JSW+3	DanceRating=QST+3	DanceRating=SFT+4	DanceRating=SWG+3	DanceRating=FXT+2	DanceRating=BBA+1	DanceRating=CST+1	DanceRating=JSW+1	.Edit=	User=dwgray	Time=10/21/2015 02:15:32	Tag+:HST=half-time:Tempo	Tag+:SFT=half-time:Tempo	DanceRating=SWG+3	DanceRating=FXT+2	DanceRating=BBA+1	DanceRating=CST+1	DanceRating=JSW+1	.Edit=	User=batch-a	Time=01/13/2016 17:06:00	Purchase:00:AS=D:B001O4TTUS	Purchase:00:AA=D:B001O4MZ6I	Tag+=soundtracks:Music	.Edit=	User=batch-s	Time=01/13/2016 17:06:00	Purchase:02:SS=3u4foTqxZAx7MoEBMDuQJf	Purchase:02:SA=1qgQnqu9JT9JYpi8GnJncK	OrderAlbums=1,0,2	.FailedLookup=-:0	.Edit=	User=batch-s	Time=02/08/2016 19:38:38	Sample=https://p.scdn.co/mp3-preview/e384837983ce1a1c569e9e7b2c09686a79694ab2	.Edit=	User=batch-e	Time=02/09/2016 18:56:47	Tempo=119.9	Danceability=0.7219507	Energy=0.8326042	Valence=0.7062438	Tag+=4/4:Tempo",
                });

            var songs = new List<Song>
            {
                service.Dms.FindSong(new Guid("{60853549-C634-4F22-A820-4DAEF48262B2}")),
                service.Dms.FindSong(new Guid("{34f612f4-feb9-4f19-900d-59671007a28a}")),
                service.Dms.FindSong(new Guid("{42a17a7a-b2be-46bc-9276-f205f270a7d7}"))
            };

            foreach (var song in songs)
            {
                var sd = new SongDetails(song);
                var result = sd.GetIndexDocument();
                Assert.IsNotNull(result);
                Assert.AreEqual(sd.SongId.ToString(),result[SongBase.SongIdField]);
            }
        }
    }
}

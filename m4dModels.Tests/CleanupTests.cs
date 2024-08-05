using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class CleanupTests
    {
        private const string SongA =
            @"SongId={defd92b3-0a2a-4d49-b8ec-cc57fcb6fa80}	.Merge=03245880-548c-45cf-a3a0-5798de4db80a;ee7c202c-9aea-479f-9dde-7a4130c71723;2c40655a-eee6-4611-a03f-ded86b7c5cc6	User=dwgray	Time=07/31/2015 18:34:41	.Create=	User=TaylorZ	Time=05/07/2015 15:35:43	Title=From This Moment	Artist=Shania Twain / Bryan White	Tempo=52.0	Tag+=Castle Foxtrot:Dance|First Dance:Other|Wedding:Other	DanceRating=CFT+3	DanceRating=FXT+1	.Create=	User=OliviaL	Time=10/1/2014 5:29:24 PM	Title=From This Moment	Artist=Shania Twain	Tempo=66.0	DanceRating=CFT+5	.Edit=	User=batch	Time=10/9/2014 11:00:27 AM	DanceRating=FXT+3	.Edit=	User=batch	Time=10/9/2014 12:09:09 PM	DanceRating=CFT+4	.Edit=	User=OliviaL	Time=11/20/2014 11:32:10 AM	Tag+=Castle Foxtrot:Dance	.Edit=	User=AshleyF	Time=5/7/2015 5:36:09 PM	Tag+=Castle Foxtrot:Dance|First Dance:Other|Wedding:Other	DanceRating=CFT+3	DanceRating=FXT+1	.Create=	User=AudreyK	Time=6/21/2014 8:22:43 PM	Title=From This Moment On	Artist=Shania Twain	DanceRating=WLZ+5	.Edit=	User=batch	Time=6/21/2014 8:24:01 PM	Length=235	PromoteAlbum:00=	.Edit=	User=batch	Time=6/21/2014 8:25:25 PM	PromoteAlbum:01=	.Edit=	User=batch	Time=6/21/2014 8:26:17 PM	Title=From This Moment On (Pop On-Tour Version)	.Edit=	User=AudreyK	Time=9/4/2014 8:07:13 PM	.Edit=	User=batch	Time=9/4/2014 8:07:13 PM	.Edit=	User=AudreyK	Time=9/4/2014 8:12:15 PM	.Edit=	User=batch	Time=9/4/2014 8:12:15 PM	.Edit=	User=AudreyK	Time=11/20/2014 11:29:16 AM	Tag+=Waltz:Dance	.Edit=	User=batch	Time=11/20/2014 11:29:16 AM	Tag+=Country:Music	.Edit=	User=batch	Time=12/9/2014 3:22:57 PM	.Edit=	User=batch-a	Time=12/10/2014 4:05:12 PM	Tag+=country:Music	.Edit=	User=batch-i	Time=12/10/2014 4:05:14 PM	Title=From This Moment On	.Edit=	User=batch-x	Time=12/10/2014 4:05:14 PM	Tag+=Country:Music	.Edit=	User=batch-s	Time=1/6/2015 4:41:47 PM	Length=237	.Edit=	User=LucyM	Time=5/7/2015 6:22:36 PM	Tag+=First Dance:Other|Slow Waltz:Dance|Wedding:Other	DanceRating=SWZ+3	DanceRating=WLZ+1	.Edit=	User=dwgray	Time=07/31/2015 18:34:41	Title=From This Moment	Artist=Shania Twain / Bryan White	Album:00=Greatest Hits	Track:00=7	Purchase:00:IS=27890875	Purchase:00:IA=27890832	Purchase:00:XS=music.75DB2900-0100-11DB-89CA-0019B92A3933	Purchase:00:AS=D:B001NZ1ERE	Purchase:00:AA=D:B001NZ3DY6	Album:01=Come on Over	.Edit=	User=dwgray	Time=07/31/2015 18:37:43	Tempo=68.8	OrderAlbums=1,0	Tag+=Castle Foxtrot:Dance|First Dance:Other|Slow Foxtrot:Dance|Smooth:Tempo|Wedding:Other	DanceRating=CFT+3	DanceRating=SFT+4	Tag+:SWZ=Fake:Tempo	Tag+:WLZ=Fake:Tempo	Tag+:SFT=double-time:Tempo	DanceRating=FXT+2	DanceRating=CFT+1	.FailedLookup=-:0	.Edit=	User=batch-s	Time=02/17/2016 16:28:00	Sample=http://a1928.phobos.apple.com/us/r1000/120/Music/bb/df/c6/mzm.pfgqymeb.aac.p.m4a";

        private const string SongB =
            @"SongId={250b462b-5f8f-420c-81cc-74cdcc03a48f}	.Create=	User=LucyM	Time=05/07/2015 18:23:10	Title=The Way You Look Tonight	Artist=Frank Sinatra	Tag+=First Dance:Other|Slow Waltz:Dance|Wedding:Other	DanceRating=SWZ+3	DanceRating=WLZ+1	.Edit=	User=batch-a	Time=5/7/2015 6:25:53 PM	Length=201	Tag+=jazz:Music|pop:Music	.Edit=	User=batch-i	Time=5/7/2015 6:25:53 PM	Length=202	Tag+=Jazz:Music|Vocal Pop:Music	.Edit=	User=batch-s	Time=5/7/2015 6:25:53 PM	Length=201	.Edit=	User=batch-x	Time=5/7/2015 6:25:55 PM	Length=197	Tag+=Jazz:Music|Pop:Music	.Create=	User=AshleyF	Time=05/07/2015 17:41:00	Title=The Way You Look Tonight	Artist=Frank Sinatra	Tag+=First Dance:Other|Slow Foxtrot:Dance|Wedding:Other	DanceRating=SFT+3	DanceRating=FXT+1	.Edit=	User=batch-a	Time=5/7/2015 6:13:27 PM	Length=201	Tag+=jazz:Music|pop:Music	.Edit=	User=batch-i	Time=5/7/2015 6:13:27 PM	Length=202	Tag+=Jazz:Music|Vocal Pop:Music	.Edit=	User=batch-s	Time=5/7/2015 6:13:27 PM	Length=201	.Edit=	User=batch-x	Time=5/7/2015 6:13:29 PM	Length=197	Tag+=Jazz:Music|Pop:Music	.Create=	User=TaylorZ	Time=05/07/2015 15:39:50	Title=The Way You Look Tonight	Artist=Frank Sinatra	Tempo=120.0	Tag+=First Dance:Other|Slow Foxtrot:Dance|Wedding:Other	DanceRating=SFT+4	DanceRating=FXT+1	.Edit=	User=TaylorZ	Time=5/7/2015 3:45:32 PM	Tag+=Father Daughter:Other	DanceRating=SFT+4	DanceRating=FXT+1	.Edit=	User=TaylorZ	Time=5/7/2015 3:46:24 PM	Tag+=Mother Son:Other	DanceRating=SFT+4	DanceRating=FXT+1	.Create=	User=dwgray	Time=1/12/2015 12:58:27 PM	OwnerHash=551960923	Title=The Way You Look Tonight	Artist=Frank Sinatra	Tempo=130.5	Length=203	.Edit=	User=AdamT	Time=4/15/2015 9:15:26 PM	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+6	DanceRating=FXT+1	Tag+:SFT=Traditional:Style	.Edit=	User=batch-a	Time=4/15/2015 9:23:51 PM	Title=The Way You Look Tonight (Remastered)	Length=200	Tag+=easy-listening:Music|jazz:Music	.Edit=	User=batch-i	Time=4/15/2015 9:23:51 PM	Title=The Way You Look Tonight	Length=202	Tag+=Vocal Pop:Music	.Edit=	User=batch-s	Time=4/15/2015 9:23:52 PM	Length=205	.Edit=	User=batch-x	Time=4/15/2015 9:23:53 PM	Length=200	Tag+=Jazz:Music|Pop:Music	.Create=	User=batch	Time=3/19/2014 12:35:20 PM	Title=The Way You Look Tonight	Artist=Frank Sinatra	Tempo=128.0	.Edit=	User=ChaseP	Time=5/7/2014 1:58:13 PM	DanceRating=ECS+10	DanceRating=SFT+5	.Edit=	User=batch	Time=5/7/2014 1:58:13 PM	Length=158	PromoteAlbum:0=	.Edit=	User=AudreyK	Time=6/21/2014 8:22:43 PM	DanceRating=WLZ+6	.Edit=	User=OliviaL	Time=10/1/2014 5:29:29 PM	DanceRating=CFT+6	.Edit=	User=OliviaL	Time=10/1/2014 5:55:42 PM	DanceRating=ECS+3	.Edit=	User=OliviaL	Time=10/2/2014 11:51:11 AM	DanceRating=JIV+6	.Edit=	User=OliviaL	Time=10/2/2014 1:47:19 PM	DanceRating=SFT+3	.Edit=	User=OliviaL	Time=10/2/2014 1:47:27 PM	DanceRating=SFT+3	.Edit=	User=batch	Time=10/9/2014 11:00:33 AM	DanceRating=FXT+5	DanceRating=SWG+5	.Edit=	User=batch	Time=10/9/2014 12:09:15 PM	DanceRating=SFT+4	DanceRating=CSG+4	DanceRating=WCS+4	DanceRating=LHP+4	.Edit=	User=HunterZ	Time=11/20/2014 11:32:25 AM	Tag+=East Coast Swing:Dance|Slow Foxtrot:Dance	.Edit=	User=ChaseP	Time=11/20/2014 11:32:25 AM	Tag+=East Coast Swing:Dance|Slow Foxtrot:Dance	.Edit=	User=AudreyK	Time=11/20/2014 11:32:25 AM	Tag+=Waltz:Dance	.Edit=	User=batch	Time=11/20/2014 11:32:25 AM	Tag+=Pop:Music	.Edit=	User=OliviaL	Time=11/20/2014 11:32:25 AM	Tag+=Castle Foxtrot:Dance|East Coast Swing:Dance|Jive:Dance|Slow Foxtrot:Dance	.Edit=	User=batch-a	Time=12/10/2014 8:48:02 PM	Length=157	Tag+=jazz:Music|pop:Music	.Edit=	User=batch-i	Time=12/10/2014 8:48:02 PM	Length=162	Tag+=Jazz:Music|Pop:Music|Vocal Jazz:Music|Vocal:Music	.Edit=	User=batch-x	Time=12/10/2014 8:48:03 PM	Length=157	Tag+=Jazz:Music|Pop:Music	.Edit=	User=batch-s	Time=1/6/2015 5:01:17 PM	Length=162	.Merge=619377cc-ebdd-4e2f-a39a-04ddb34c11b1;2c217c31-c5d2-40ee-879c-96557384a303;a369451c-f0b2-4b15-b0dc-caa704d2e5cf;c8aefccb-3e0f-4f95-b820-d0e4f9b7b5c8;ed4c365f-3d93-4715-8ca0-fabaf730d4c2	.Edit=	User=batch	Time=06/10/2015 14:23:25	.Edit=	User=batch	Time=6/10/2015 2:23:27 PM	Tempo=128.0	Album:00=Nothing But the Best (Remastered)	Track:00=3	Purchase:00:AS=D:B00FHU141M	Purchase:00:AA=D:B00FHU10UM	Purchase:00:IS=717552720	Purchase:00:IA=717552717	Album:01=Ultimate Sinatra	Track:01=15	Purchase:01:AS=D:B00TU1BHKW	Purchase:01:AA=D:B00TU1AI84	Purchase:01:IS=969299403	Purchase:01:IA=969298500	Purchase:01:SS=0shGCs5AkhwJIgUb0SSz2B[AU,CA,MX,US]	Purchase:01:SA=4G6ZaR4A7tkkMsglaYpDeS	Purchase:01:XS=music.31C3DE08-0100-11DB-89CA-0019B92A3933	Album:02=Sinatra, With Love	Track:02=7	Purchase:02:AS=D:B00HWM17ME	Purchase:02:AA=D:B00HWM11OS	Purchase:02:XS=music.85701508-0100-11DB-89CA-0019B92A3933	Album:03=Days Of Wine And Roses, Moon River And Other Academy Award Winners	Track:03=3	Purchase:03:AS=D:B00FAXHGXQ	Purchase:03:AA=D:B00FAXHDPC	Purchase:03:IS=711636279	Purchase:03:IA=711636230	Purchase:03:SS=0elmUoU7eMPwZX1Mw1MnQo[0]	Purchase:03:SA=1BA6ebXEZ79g2TQ1o6dklD	Purchase:03:XS=music.6D54E807-0100-11DB-89CA-0019B92A3933	Album:04=Ultimate Sinatra: The Centennial Collection	Track:04=77	Purchase:04:AS=D:B00V07JT8G	Purchase:04:AA=D:B00V07G4H0	Purchase:04:IS=978885100	Purchase:04:IA=978881306	Purchase:04:SS=2VNazQ7xXeC0o3p27nSti0[1]	Purchase:04:SA=22KCcelRH1AeHz7S7x5XhY	Purchase:04:XS=music.6BA4E908-0100-11DB-89CA-0019B92A3933	Album:05=Sinatra, With Love (Remastered)	Track:05=7	Purchase:05:IS=798940769	Purchase:05:IA=798940614	Album:06=Nothing But The Best	Track:06=3	Purchase:06:XS=music.F734EB07-0100-11DB-89CA-0019B92A3933	Album:07=Days Of Wine And Roses	Track:07=3	Purchase:07:XS=music.BB08A006-0100-11DB-89CA-0019B92A3933	Album:08=30 Grandes de Frank Sinatra	Track:08=29	Purchase:08:XS=music.931AEC06-0100-11DB-89CA-0019B92A3933	Album:09=15 Grandes Exitos de Frank Sinatra Vol. 2	Track:09=14	Purchase:09:AS=D:B00OBO8C5Q	Purchase:09:AA=D:B00OBO65S2	Purchase:09:XS=music.D3629408-0100-11DB-89CA-0019B92A3933	Album:10=The ""V Discs"" - The Columbia Years 1943 - 1952	Track:10=6	Purchase:10:AS=D:B00137XETS	Purchase:10:AA=D:B00138EWXE	Purchase:10:XS=music.11B0AE00-0100-11DB-89CA-0019B92A3933	Purchase:10:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:10:SA=4F2N7GHRi69w5dxeFY9WLD	Album:11=The Columbia Years 1943-1952: The V-Discs	Track:11=6	Purchase:11:IS=264571453	Purchase:11:IA=264571342	Purchase:11:XS=music.E1900D00-0100-11DB-89CA-0019B92A3933	Album:12=The Rat Pack (Classic Collection Presents)	Track:12=35	Purchase:12:IS=945805319	Purchase:12:IA=945800842	Album:13=50 Traditional Jazz Standards, Vol. 1	Track:13=27	Purchase:13:IS=719456321	Purchase:13:IA=719455421	Album:14=75 of the Best from Edith, Doris, Bing, Ella and Others	Track:14=44	Purchase:14:IS=719441859	Purchase:14:IA=719439994	Album:15=Frank Sinatra Volume 2	Track:15=1	Purchase:15:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:15:SA=2dOOt2mKM8BI0LcEAot24a	Album:16=The One and Only: Frank Sinatra (Remastered)	Track:16=13	Purchase:16:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:16:SA=4p3uNhVACh7egypBw5HRTQ	Album:17=They Way You Look Tonight	Track:17=1	Purchase:17:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:17:SA=5QmrC9fUp50K8uYqAzgulL	Album:18=Behind The Legend - Frank Sinatra	Track:18=8	Purchase:18:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:18:SA=1YBmaTO8hzRgU7u6lq8Ld3	Album:19=Jazz Giants: Frank Sinatra	Track:19=4	Purchase:19:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:19:SA=6LR4TdpIkdDvGccE3Jqv6T	Album:20=The Night Will Never End (Ultimate Legends Presents Frank Sinatra)	Track:20=6	Purchase:20:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:20:SA=5cqQHZBoEeUHRuH60gNNvR	Album:21=Beyond Patina Jazz Masters: Frank Sinatra	Track:21=16	Purchase:21:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:21:SA=2N9yk7JG4YnnEGugjWu5oU	Album:22=Moments (Ultimate Legends Presents Frank Sinatra)	Track:22=6	Purchase:22:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:22:SA=0vURInfVzaTsIS0v5jNBtB	Album:23=There's No Business Like Show Business Volume 3	Track:23=8	Purchase:23:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:23:SA=1Z3ox51c3Z9XUDS6LnxLtp	Album:24=Close To You	Track:24=15	Purchase:24:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:24:SA=7bjUs0jXoIU87xq4uSV0v7	Album:25=La Voz De Frank Sinatra	Track:25=13	Purchase:25:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:25:SA=7s1VXAEVTrFUNfT4n0w7l7	Album:26=The V Discs	Track:26=6	Purchase:26:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:26:SA=7v4Q8mQZKNjX2WsWWRjzKm	Album:27=The Very Good Years	Track:27=5	Album:28=Nothing But the Best (Remastered)	Track:28=3	Purchase:28:AS=D:B00FHU141M	Purchase:28:AA=D:B00FHU10UM	Purchase:28:IS=717552720	Purchase:28:IA=717552717	Album:29=Days Of Wine And Roses, Moon River And Other Academy Award Winners	Track:29=3	Purchase:29:AS=D:B00FAXHGXQ	Purchase:29:AA=D:B00FAXHDPC	Purchase:29:SS=0elmUoU7eMPwZX1Mw1MnQo[1]	Purchase:29:SA=7FAo3wmrJNNzz2W5Z5ZG80	Purchase:29:XS=music.6D54E807-0100-11DB-89CA-0019B92A3933	Album:30=Ultimate Sinatra	Track:30=15	Purchase:30:AS=D:B00TU1BHKW	Purchase:30:AA=D:B00TU1AI84	Album:31=Ultimate Sinatra: The Centennial Collection	Track:31=1	Purchase:31:AS=D:B00V07JT8G	Purchase:31:AA=D:B00V07G4H0	Album:32=Sinatra, With Love	Track:32=7	Purchase:32:AS=D:B00HWM17ME	Purchase:32:AA=D:B00HWM11OS	Purchase:32:XS=music.85701508-0100-11DB-89CA-0019B92A3933	Album:33=Sinatra, With Love (Remastered)	Track:33=7	Purchase:33:IS=798940769	Purchase:33:IA=798940614	Album:34=Unforgettable Songs	Track:34=22	Purchase:34:SS=5j4oXrueFBcaZoCdwd0iMi	Purchase:34:SA=0VAWFK1jEZvekVaZKm8Fls	Album:35=Nothing But The Best	Track:35=3	Purchase:35:XS=music.F734EB07-0100-11DB-89CA-0019B92A3933	Album:36=Very Best Of	Album:37=Nothing But the Best (Remastered)	Track:37=3	Purchase:37:AS=D:B00FHU141M	Purchase:37:AA=D:B00FHU10UM	Purchase:37:IS=717552720	Purchase:37:IA=717552717	Album:38=Ultimate Sinatra	Track:38=15	Purchase:38:AS=D:B00TU1BHKW	Purchase:38:AA=D:B00TU1AI84	Purchase:38:IS=969299403	Purchase:38:IA=969298500	Purchase:38:SS=0shGCs5AkhwJIgUb0SSz2B[AU,CA,MX,US]	Purchase:38:SA=4G6ZaR4A7tkkMsglaYpDeS	Purchase:38:XS=music.31C3DE08-0100-11DB-89CA-0019B92A3933	Album:39=Sinatra, With Love	Track:39=7	Purchase:39:AS=D:B00HWM17ME	Purchase:39:AA=D:B00HWM11OS	Purchase:39:XS=music.85701508-0100-11DB-89CA-0019B92A3933	Album:40=Days Of Wine And Roses, Moon River And Other Academy Award Winners	Track:40=3	Purchase:40:AS=D:B00FAXHGXQ	Purchase:40:AA=D:B00FAXHDPC	Purchase:40:IS=711636279	Purchase:40:IA=711636230	Purchase:40:SS=0elmUoU7eMPwZX1Mw1MnQo[0]	Purchase:40:SA=1BA6ebXEZ79g2TQ1o6dklD	Purchase:40:XS=music.6D54E807-0100-11DB-89CA-0019B92A3933	Album:41=Ultimate Sinatra: The Centennial Collection	Track:41=77	Purchase:41:AS=D:B00V07JT8G	Purchase:41:AA=D:B00V07G4H0	Purchase:41:IS=978885100	Purchase:41:IA=978881306	Purchase:41:SS=2VNazQ7xXeC0o3p27nSti0[1]	Purchase:41:SA=22KCcelRH1AeHz7S7x5XhY	Purchase:41:XS=music.6BA4E908-0100-11DB-89CA-0019B92A3933	Album:42=Sinatra, With Love (Remastered)	Track:42=7	Purchase:42:IS=798940769	Purchase:42:IA=798940614	Album:43=Nothing But The Best	Track:43=3	Purchase:43:XS=music.F734EB07-0100-11DB-89CA-0019B92A3933	Album:44=Days Of Wine And Roses	Track:44=3	Purchase:44:XS=music.BB08A006-0100-11DB-89CA-0019B92A3933	.Edit=	User=dwgray	Time=09/14/2015 21:20:16	Tempo=132.0	Tag+=Castle Foxtrot:Dance|Slow Foxtrot:Dance	DanceRating=CFT+3	DanceRating=SFT+3	Tag+:CFT=half-time:Tempo	Tag+:ECS=Slow:Tempo	Tag+:SWZ=Fake:Tempo	Tag+:WCS=Fast:Tempo	Tag+:WLZ=Fake:Tempo	DanceRating=FXT+2	DanceRating=SFT+1	.Edit=	User=batch	Time=01/11/2016 18:35:07	Album:00=Nothing But The Best	Purchase:00:XS=music.F734EB07-0100-11DB-89CA-0019B92A3933	Purchase:02:IS=798940769	Purchase:02:IA=798940614	Album:05=	Track:05=	Album:06=	Track:06=	Album:28=	Track:28=	Album:29=	Track:29=	Album:30=	Track:30=	Album:32=	Track:32=	Album:33=	Track:33=	Album:35=	Track:35=	Album:37=	Track:37=	Album:38=	Track:38=	Album:39=	Track:39=	Album:40=	Track:40=	Album:41=	Track:41=	Album:42=	Track:42=	Album:43=	Track:43=	Album:44=	Track:44=	OrderAlbums=0,1,4,31,2,3,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,34,36	.FailedLookup=-:0	.Edit=	User=batch-s	Time=02/08/2016 19:39:01	Sample=https://p.scdn.co/mp3-preview/e05c5b2c4090c64ff22a9353a06915ddd5dd3659	.Edit=	User=batch-e	Time=02/09/2016 19:07:21	Tempo=132.9	Danceability=0.5561369	Energy=0.3706279	Valence=0.490889	Tag+=4/4:Tempo";

        private const string SongC =
            @"SongId={05178359-eaae-4463-8de1-4c79c403adb4}	.Merge=467220be-feac-4184-af47-1b15213bbc9b;0e288e64-6158-43fa-95b2-43dad1d085bf	.Edit=	User=batch	Time=6/12/2015 11:36:15 AM	Title=I Get a Kick out of You	Tempo=102.5	Album:00=The Very Good Years	Track:00=3	Album:01=Sinatra and Swingin' Brass	Track:01=6	Purchase:01:AS=D:B00FAXDMWU	Purchase:01:AA=D:B00FAXDD7Y	Purchase:01:IS=923098168	Purchase:01:IA=923094701	Album:02=X-Mas, Vol. 5 (Let It Snow! Let It Snow! Let It Snow!)	Track:02=10	Purchase:02:IS=487823078	Purchase:02:IA=487822808	Album:03=In French or English - 3	Track:03=9	Purchase:03:IS=656991553	Purchase:03:IA=656991512	Album:04=In French or English ? 2	Track:04=17	Purchase:04:IS=656625882	Purchase:04:IA=656625397	Album:05=Jukebox Favourites - Best of Swing	Track:05=59	Purchase:05:IS=814204173	Purchase:05:IA=814204056	Album:06=Song Shop - Song Doubles	Track:06=6	Purchase:06:IS=663133012	Purchase:06:IA=663132879	Album:07=Song Doubles Volume One	Track:07=18	Purchase:07:IS=943867362	Purchase:07:IA=943867288	Album:08=Your Birthday Present - Song Doubles	Track:08=18	Purchase:08:IS=660024227	Purchase:08:IA=660024148	Album:09=Frank Sinatra & Sextet: In Paris - Live	Track:09=15	Purchase:09:SS=3tMZgBzebiTyQTP788GQM9	Purchase:09:SA=0U0dbAPfsrCn3P1ewtyJj0	.Create=	User=dwgray	Time=1/12/2015 12:58:27 PM	OwnerHash=-984185430	Title=I Get A Kick Out Of You	Artist=Frank Sinatra	Tempo=102.5	Length=194	.Edit=	User=AdamT	Time=4/16/2015 8:26:04 AM	Tag+=QuickStep:Dance	DanceRating=QST+4	DanceRating=FXT+1	Tag+:QST=Traditional:Style	.Edit=	User=batch-a	Time=4/16/2015 8:29:28 AM	Tag+=big-band-swing:Music	.Edit=	User=batch-i	Time=4/16/2015 8:29:29 AM	Title=I Get a Kick Out of You	Length=189	Tag+=Jazz:Music|Pop:Music|Swing:Music|Vocal Jazz:Music	.Edit=	User=batch-s	Time=4/16/2015 8:29:29 AM	Title=I Get a Kick out of You	Length=186	.Create=	User=TaylorZ	Time=05/07/2015 15:36:09	Title=I Get A Kick Out of You	Artist=Frank Sinatra	Tempo=144.0	Tag+=East Coast Swing:Dance|First Dance:Other|Foxtrot:Dance|Wedding:Other	DanceRating=FXT+3	DanceRating=ECS+4	DanceRating=SWG+1	DanceRating=LHP+1	.Edit=	User=TaylorZ	Time=5/7/2015 3:45:21 PM	Tag+=Father Daughter:Other|Slow Foxtrot:Dance	DanceRating=SFT+3	DanceRating=ECS+4	DanceRating=SWG+1	DanceRating=FXT+1	DanceRating=LHP+1	.Edit=	User=TaylorZ	Time=5/7/2015 3:46:18 PM	Tag+=Mother Son:Other	DanceRating=SFT+3	DanceRating=ECS+4	DanceRating=SWG+1	DanceRating=FXT+1	DanceRating=LHP+1	.Edit=	User=batch-x	Time=01/13/2016 21:54:27	Purchase:07:XS=music.0A82AA08-0100-11DB-89CA-0019B92A3933	Tag+=Pop:Music	.FailedLookup=-:0	.Edit=	User=batch-s	Time=02/08/2016 22:22:14	Sample=.	.Edit=	User=batch-e	Time=02/09/2016 19:31:22	Tempo=140.1	Danceability=0.5725134	Energy=0.3595052	Valence=0.4253719	Tag+=4/4:Tempo	.Edit=	User=batch-s	Time=02/17/2016 16:27:27	Sample=.";

        private const string SongD =
            @"SongId={9e8317e8-a965-4cd5-8091-b1d08b1cdef2}	.Create=	User=HunterZ	Time=3/17/2014 5:43:50 PM	Title=Bye Bye Blackbird	Artist=Sammy Davis Jr	Tempo=116.0	Time=9/4/2014 8:12:02 PM	.Edit=	User=batch	Time=9/4/2014 8:12:02 PM	.Edit=	User=batch	Time=10/9/2014 12:09:30 PM	DanceRating=SFT+4	.Edit=	User=HunterZ	Time=11/20/2014 11:32:39 AM	Tag+=Foxtrot:Dance	.Edit=	User=batch	Time=11/20/2014 11:32:39 AM	Tag+=Jazz:Music	.Edit=	User=HunterZ	Time=9/4/2014 8:06:58 PM	.Edit=	User=batch	Time=9/4/2014 8:06:58 PM	OrderAlbums=1,0	.Edit=	User=HunterZ	Time=6/16/2014 12:41:48 PM	Artist=Sammy Davis Jr	.Edit=	User=batch	Time=9/4/2014 8:06:58 PM	PromoteAlbum:1=	Purchase:1:XS=music.086DC007-0100-11DB-89CA-0019B92A3933	Track:1=36	Album:1=The Great American Song Book	Length=171	Artist=Bye Bye Blackbird	Time=5/21/2014 2:05:46 PM	User=batch	Time=9/4/2014 8:06:58 PM	DanceRating=FXT+5	Album:0=The Ultimate Ballroom Album 10	.FailedLookup=-:0";

        private const string SongE =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602c}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Harmonica Man (rekmix)	Artist=Paul Lamb & the King Snakes	Album:0=Harmonica Man	DanceRating=PLK+5	.Edit=	User=batch	Time=6/11/2014 9:02:15 PM	Title=Harmonica Man	Artist=Paul Lamb & The King Snakes	Length=243	Album:01=Harmonica Man - The Paul Lamb Anthology 1986-2002	Track:01=17	Purchase:01:XS=music.EB85DB07-0100-11DB-89CA-0019B92A3933	PromoteAlbum:01=	.Edit=	User=HunterZ	Time=9/4/2014 8:06:30 PM	.Edit=	User=batch	Time=9/4/2014 8:06:30 PM	.Edit=	User=HunterZ	Time=9/4/2014 8:11:32 PM	.Edit=	User=batch	Time=9/4/2014 8:11:32 PM	.Edit=	User=HunterZ	Time=11/20/2014 11:29:06 AM	Tag+=Polka:Dance	.Edit=	User=batch	Time=11/20/2014 11:29:06 AM	Tag+=Blues / Folk:Music	.Edit=	User=batch-a	Time=12/10/2014 6:22:32 PM	Artist=Paul Lamb And The King Snakes	Length=246	Track:01=1	Purchase:01:AS=D:B00E74SKH0	Purchase:01:AA=D:B00E74RS46	Album:02=Harmonica Man: The Anthology 1986-2002	Track:02=1	Purchase:02:AS=D:B000SHB22K	Purchase:02:AA=D:B000S59N3M	Tag+=alternative:Music|blues:Music	.Edit=	User=batch-i	Time=12/10/2014 6:22:33 PM	Artist=Paul Lamb & The King Snakes	Length=244	Purchase:01:IS=682496811	Purchase:01:IA=682496394	Tag+=Blues:Music	.Edit=	User=batch-x	Time=12/10/2014 6:22:33 PM	Length=243	Track:01=17	Tag+=Blues / Folk:Music	.Edit=	User=batch-s	Time=1/6/2015 4:52:58 PM	Length=244	Track:01=2001	Purchase:01:SS=5PEcFNy8EO8TseIRWE1f1H[0]	Purchase:01:SA=1RNDiDdVEPepXslQPh1lBm	OrderAlbums=1,0,2	.Edit=	User=batch	Time=01/11/2016 18:29:57	Album:02=Harmonica Man	Album:00=	.FailedLookup=-:0	.Edit=	User=dwgray	Time=02/06/2016 01:42:39	Tag+=Polka:Dance	DanceRating=PLK+2	.Edit=	User=dwgray	Time=02/06/2016 01:42:45	Tag+=!Polka:Dance	Tag-=Polka:Dance	DanceRating=PLK-3	.Edit=	User=batch-s	Time=02/08/2016 19:25:07	Sample=https://p.scdn.co/mp3-preview/53916c89ece0d94242dac076fe91f8f86d8a3713	.Edit=	User=batch-e	Time=02/09/2016 18:04:50	Tempo=122.1	Danceability=0.5333328	Energy=0.9064238	Valence=0.7872825	Tag+=4/4:Tempo";

        private const string SongLength =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602d}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Test Track	Artist=Test Artist	Length=2:05";

        private const string ExtraLength =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602d}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Test Track	Artist=Test Artist	Length=1:02:05";

        private const string SongPurchase =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602d}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Test Track	Album:01=Test Album	Track:01=6	Purchase:01:MS=T B00FAXDMWU";

        private const string SongDuplicateTags =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602d}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Test Track	Tag+=International:Style|Pop:Music|Pop:Music|Zoom:Other	Tag+=International:Style|Soundtrack:Music|Soundtracks:Music|soundtracks:Music|soundtrack:Music|Zoom:Other";

        private const string SongBadCategoryTags =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602d}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Test Track	Tag+=Christmas: Pop	.Edit=	User=FlowZ	Time=3/17/2014 5:46:08 PM	Tag+=Christmas: Other";

        private const string SongSampleSinUser =
            @".Create=	User=DanielLibatique|P	Time=09/23/2017 22:19:48	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=1	.Edit=	Time=09/23/2017 22:41:28	Sample=https://p.scdn.co/mp3-preview/d552c872238b3c8d869aadb1c9f0044d361e98f7?cid=e6dc118cd7604cd2b8bd0a979a18e6f8";

        private const string SongSampleWrongUser =
            @".Create=	User=DanielLibatique|P	Time=09/23/2017 22:19:48	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=1	.Edit=	User=batch-i|P	Time=09/23/2017 22:41:28	Sample=https://p.scdn.co/mp3-preview/d552c872238b3c8d869aadb1c9f0044d361e98f7?cid=e6dc118cd7604cd2b8bd0a979a18e6f8";

        private const string SongSampleSinTime =
            @".Create=	User=DanielLibatique|P	Time=09/23/2017 22:19:48	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=1	.Edit=	User=batch-s|P	Sample=https://p.scdn.co/mp3-preview/d552c872238b3c8d869aadb1c9f0044d361e98f7?cid=e6dc118cd7604cd2b8bd0a979a18e6f8";

        private const string SongSampleSinUserAndTime =
            @".Create=	User=DanielLibatique|P	Time=09/23/2017 22:19:48	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=1	.Edit=	Sample=https://p.scdn.co/mp3-preview/d552c872238b3c8d869aadb1c9f0044d361e98f7?cid=e6dc118cd7604cd2b8bd0a979a18e6f8";

        private const string SongPurchaseEnd =
            @".Create=	User=PaulsPiano|P	Time=05/16/2021 09:02:03	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=5	.Edit=	Time=05/16/2021 09:02:04	Purchase:00:IS=1488955823	Purchase:00:IA=1488955809";

        private const string SongPurchaseMiddle =
            @".Create=	User=PaulsPiano|P	Time=05/16/2021 09:02:03	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=5	.Edit=	Time=05/16/2021 09:02:04	Purchase:00:IS=1488955823	Purchase:00:IA=1488955809	.Edit=	User=batch-s|P	Time=07/30/2023 09:25:43	Tag+=Ballroom:Music";

        private const string SongPurchaseEmbedded =
            @".Create=	User=PaulsPiano|P	Time=05/16/2021 09:02:03	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=5	Purchase:00:IS=1488955823	Purchase:00:IA=1488955809";

        private const string SongMissingEdit =
            @".Create=	User=PaulsPiano|P	Time=05/16/2021 09:02:03	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=5	User=dwgray	Time=07/30/2023 09:25:43	Tag+=Ballroom:Music";

        private const string SongMissingEditAndTime =
            @".Create=	User=PaulsPiano|P	Time=05/16/2021 09:02:03	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=5	User=dwgray	Tag+=Ballroom:Music";

        private const string SongMissingEditAndTimeBatch =
            @".Create=	User=PaulsPiano|P	Time=05/16/2021 09:02:03	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=5	User=batch-s|P	Tag+=Ballroom:Music";

        private const string SongWithOwner =
            @".Create=	User=PaulsPiano|P	OwnerHash=-1784228082	Time=05/16/2021 09:02:03	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=5";

        private const string SongWithEmpties =
            @".Create=	User=DanielLibatique|P	Time=09/23/2017 22:19:48	Title=TestT	Artist=TestA	Album:00=TestLP	Track:00=1	Tag+=Slow Waltz:Dance	DanceRating=SWZ+1	Tag+:SWZ=	.Edit=	User=batch-s|P	Time=09/23/2017 22:41:28	Sample=.";



        public CleanupTests()
        {
            General = new TraceSwitch("General", "All Tests", "Info");
        }

        private TraceSwitch General { get; }

        [TestMethod]
        public async Task CleanEmpties()
        {
            var dms = await DanceMusicTester.CreateService("Cleanup");

            var songs = new List<Song>
            {
                await Song.Create(SongA, dms),
                await Song.Create(SongD, dms)
            };

            var deltas = new List<int> { 15, 6 };

            for (var index = 0; index < songs.Count; index++)
            {
                var song = songs[index];
                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"---------------Predump for Song {song.SongId}");
                DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
                var c = song.SongProperties.Count;
                Assert.IsTrue(song.RemoveEmptyEdits());
                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"{song.SongId}:{song.SongProperties.Count - c}");
                Assert.AreEqual(c - deltas[index], song.SongProperties.Count);

                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"---------------Postdump for Song {song.SongId}");
                DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
            }
        }

        [TestMethod]
        public async Task CleanRatings()
        {
            var dms = await DanceMusicTester.CreateService("Cleanup");

            var songs = new List<Song>
            {
                await Song.Create(SongB, dms),
                await Song.Create(SongC, dms),
                await Song.Create(SongE, dms)
            };

            var deltas = new List<int> { 17, 11, 3 };

            for (var index = 0; index < songs.Count; index++)
            {
                var song = songs[index];
                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"---------------Predump for Song {song.SongId}");
                DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
                var c = song.SongProperties.Count;
                Assert.IsTrue(song.NormalizeRatings());
                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"{song.SongId}:{song.SongProperties.Count - c}");
                Assert.AreEqual(c - deltas[index], song.SongProperties.Count);

                var sd = await Song.Create(song.SongId, song.SongProperties, dms);
                Assert.AreEqual(song.DanceRatings.Count, sd.DanceRatings.Count);
                foreach (var dr in song.DanceRatings)
                {
                    var drT = sd.DanceRatings.FirstOrDefault(r => r.DanceId == dr.DanceId);
                    Assert.IsNotNull(drT);
                    Assert.AreEqual(dr.Weight, drT.Weight);
                }

                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"---------------Postdump for Song {song.SongId}");
                DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
            }
        }

        [TestMethod]
        public async Task CleanDurations()
        {
            var dms = await DanceMusicTester.CreateService("Cleanup");

            var songs = new List<Song>
            {
                await Song.Create(SongB, dms),
                await Song.Create(SongC, dms)
            };

            var deltas = new List<int> { 17, 2 };

            for (var index = 0; index < songs.Count; index++)
            {
                var song = songs[index];
                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"---------------Predump for Song {song.SongId}");
                DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
                var c = song.SongProperties.Count;
                Assert.IsTrue(song.RemoveDuplicateDurations());
                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"{song.SongId}:{song.SongProperties.Count - c}");
                Assert.AreEqual(c - deltas[index], song.SongProperties.Count);

                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"---------------Postdump for Song {song.SongId}");
                DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
            }
        }

        [TestMethod]
        public async Task CleanAlbums()
        {
            var dms = await DanceMusicTester.CreateService("Cleanup");

            var song = await Song.Create(SongB, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(song.CleanupAlbums());
            Trace.WriteLineIf(General.TraceInfo, $"{song.SongId}:{song.SongProperties.Count - c}");

            Assert.AreEqual(c - 118, song.SongProperties.Count);

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
        }

        [TestMethod]
        public async Task CleanAll()
        {
            var dms = await DanceMusicTester.CreateService("Cleanup");

            var songs = new List<Song>
            {
                await Song.Create(SongA, dms),
                await Song.Create(SongB, dms),
                await Song.Create(SongC, dms),
                await Song.Create(SongD, dms),
                await Song.Create(SongE, dms)
            };

            var deltas = new List<List<int>>
            {
                new() { 1, 4, 5, 26 },
                new() { 17, 135, 152, 188 },
                new() { 2, 2, 13, 13 },
                new() { 0, 2, 2, 11 },
                new() { 4, 8, 11, 23 }
            };

            for (var index = 0; index < songs.Count; index++)
            {
                var song = songs[index];
                var delta = deltas[index];
                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"---------------Predump for Song {song.SongId}");
                DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
                var c = song.SongProperties.Count;
                song.RemoveDuplicateDurations();
                Trace.WriteLineIf(
                    General.TraceVerbose,
                    $"++++After Durations: {song.SongId}:{song.SongProperties.Count - c}");
                DanceMusicTester.DumpSongProperties(song, General.TraceVerbose);
                Assert.AreEqual(delta[0], c - song.SongProperties.Count);
                song.CleanupAlbums();
                Trace.WriteLineIf(
                    General.TraceVerbose,
                    $"++++After Albums: {song.SongId}:{song.SongProperties.Count - c}");
                DanceMusicTester.DumpSongProperties(song, General.TraceVerbose);
                Assert.AreEqual(delta[1], c - song.SongProperties.Count);
                song.NormalizeRatings();
                Trace.WriteLineIf(
                    General.TraceVerbose,
                    $"++++After Ratings: {song.SongId}:{song.SongProperties.Count - c}");
                DanceMusicTester.DumpSongProperties(song, General.TraceVerbose);
                Assert.AreEqual(delta[2], c - song.SongProperties.Count);
                song.RemoveEmptyEdits();
                Trace.WriteLineIf(
                    General.TraceVerbose,
                    $"++++After Empty: {song.SongId}:{song.SongProperties.Count - c}");
                DanceMusicTester.DumpSongProperties(song, General.TraceVerbose);
                Assert.AreEqual(delta[3], c - song.SongProperties.Count);
                Trace.WriteLineIf(
                    General.TraceInfo,
                    $"---------------Postdump for Song {song.SongId}");
                DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

                // May be worth doing some verification on this, but for now just want to make sure it loads...
                var sd = await Song.Create(song.SongId, song.SongProperties, dms);
                Assert.IsNotNull(sd);
            }
        }

        [TestMethod]
        public async Task FixupLengths()
        {
            var dms = await DanceMusicTester.CreateService("Cleanup");

            var song = await Song.Create(SongLength, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(song.FixupLengths());
            Trace.WriteLineIf(General.TraceInfo, $"{song.SongId}:{song.SongProperties.Count - c}");

            Assert.AreEqual(c, song.SongProperties.Count);
            await song.Load(song.SongProperties, dms);
            Assert.AreEqual(song.Length, 125);

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
        }

        [TestMethod]
        public async Task FixupExtraLong()
        {
            var dms = await DanceMusicTester.CreateService("Cleanup");

            var song = await Song.Create(ExtraLength, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(song.FixupLengths());
            Trace.WriteLineIf(General.TraceInfo, $"{song.SongId}:{song.SongProperties.Count - c}");

            Assert.AreEqual(c, song.SongProperties.Count);
            await song.Load(song.SongProperties, dms);
            Assert.AreEqual(song.Length, 3725);

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
        }

        [TestMethod]
        public async Task FixObsoletePurchase()
        {
            var dms = await DanceMusicTester.CreateService("Cleanup");

            var song = await Song.Create(SongPurchase, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(song.RemoveObsoletePurchases());
            Trace.WriteLineIf(General.TraceInfo, $"{song.SongId}:{song.SongProperties.Count - c}");

            Assert.AreEqual(c - 1, song.SongProperties.Count);

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
        }

        [TestMethod]
        public async Task CleanupSampleWithoutUser()
        {
            await CleanupSamples(SongSampleSinUser, 1);
        }

        [TestMethod]
        public async Task CleanupSampleWrongUser()
        {
            await CleanupSamples(SongSampleWrongUser, 0);
        }

        [TestMethod]
        public async Task CleanupSampleWithoutTime()
        {
            await CleanupSamples(SongSampleSinTime, 1);
        }

        [TestMethod]
        public async Task CleanupSampleWithoutUserAndTime()
        {
            await CleanupSamples(SongSampleSinUserAndTime, 2);
        }

        private async Task CleanupSamples(string s, int delta)
        {
            await CleanupHeader(s, "batch-s|P", Song.SampleField, delta, (song, _) => Task.FromResult(song.CleanupSamples()));
        }

        [TestMethod]
        public async Task CleanupPurchasesEnd()
        {
            await CleanupPurchases(SongPurchaseEnd, 1);
        }

        [TestMethod]
        public async Task CleanupPurchasesMiddle()
        {
            await CleanupPurchases(SongPurchaseMiddle, 1);
        }

        private async Task CleanupPurchases(string s, int delta)
        {
            await CleanupHeader(s, "batch-i|P", Song.PurchaseField, delta, (song, _) => Task.FromResult(song.CleanupPurchases()));
        }

        [TestMethod]
        public async Task CleanupMissingEditsSimple()
        {
            await CleanupMissingEdits(SongMissingEdit, "dwgray", 1);
        }

        [TestMethod]
        public async Task CleanupMissingEditsAndTime()
        {
            await CleanupMissingEdits(SongMissingEditAndTime, "dwgray", 2);
        }

        [TestMethod]
        public async Task CleanupMissingEditsAndTimeBatch()
        {
            await CleanupMissingEdits(SongMissingEditAndTimeBatch, "batch-s|P", 2);
        }

        private async Task CleanupMissingEdits(string s, string user, int delta)
        {
            await CleanupHeader(s, user, Song.AddedTags, delta, (song, dms) => song.CleanupMissingEdits(dms));
        }

        private async Task CleanupHeader(string s, string userName, string field, int delta,
            Func<Song, DanceMusicCoreService, Task<bool>> clean)
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");

            var song = await Song.Create(s, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await clean(song, dms));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c + delta, props.Count);

            var i = props.FindIndex(p => p.BaseName == field);
            Assert.IsTrue(i > 3);
            var time = props[i - 1];
            var user = props[i - 2];
            var edit = props[i - 3];
            Assert.AreEqual(Song.EditCommand, edit.BaseName);
            Assert.AreEqual(Song.UserField, user.BaseName);
            Assert.AreEqual(userName, user.Value);
            Assert.AreEqual(Song.TimeField, time.BaseName);
        }

        [TestMethod]
        public async Task RemoveOwnerHash()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");

            var song = await Song.Create(SongWithOwner, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await song.CleanupProperties(dms, "O"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c - 1, props.Count);

            var i = props.FindIndex(p => p.BaseName == Song.OwnerHash);
            Assert.IsTrue(i == -1);
        }

        [TestMethod]
        public async Task FixupMergesStart()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");
            var s = @".Merge=ebd79d55-ce15-4fc5-80f3-46b70650ba7a;1c01b16b-75b9-400f-94db-7b3225bbd1f1	.Merge=ebd79d55-ce15-4fc5-80f3-46b70650ba7a;1c01b16b-75b9-400f-94db-7b3225bbd1f1	User=batch-i|P	Time=12/22/2014 2:54:10 PM	Title=Let It Whip	Artist=Dazz Band	Tempo=132.0	Length=289	Album:00=20th Century Masters: The Millennium Collection: Best Of The Dazz Band	";
            var song = await Song.Create(s, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await song.CleanupProperties(dms, "G"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c + 1, props.Count);

            var i = props.FindIndex(p => p.BaseName == Song.CreateCommand);
            Assert.IsTrue(i == 2);
        }

        [TestMethod]
        public async Task FixupMergesMiddle()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");
            var s = @".Create	User=batch-i|P	Time=12/22/2014 2:54:10 PM	Title=Let It Whip	Artist=Dazz Band	Tempo=132.0	Length=289	Album:00=20th Century Masters: The Millennium Collection: Best Of The Dazz Band	.Merge=ebd79d55-ce15-4fc5-80f3-46b70650ba7a;1c01b16b-75b9-400f-94db-7b3225bbd1f1	.Merge=ebd79d55-ce15-4fc5-80f3-46b70650ba7a;1c01b16b-75b9-400f-94db-7b3225bbd1f1	User=batch-x|P	Time=01/15/2016 00:42:58	Tag+=Jazz:Music|More:Music|Pop:Music";
            var song = await Song.Create(s, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await song.CleanupProperties(dms, "G"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c + 1, props.Count);

            var i = props.FindLastIndex(p => p.BaseName == Song.EditCommand);
            Assert.IsTrue(i == props.Count - 4);
        }

        [TestMethod]
        public async Task DontFixupMerges()
        {
            await DontAct("G");
        }

        [TestMethod]
        public async Task AddSpotifyTagMiddle()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");
            var s = @".Merge=6a844f27-ece1-48ee-ad90-492fafe1a6b9;ab4df0a0-ff1d-4713-a430-a692e59b1df9	.Create=	User=dwgray	Time=1/12/2015 1:00:05 PM	Title=At The Woodchoppers Ball	Artist=Woody Herman	Tempo=92.9	Length=266	.Edit=	Time=01/15/2016 00:42:56	Tag+=Big Band:Music|Jazz:Music	.Edit=	User=batch-x|P	Time=01/15/2016 00:42:58	Tag+=Jazz:Music|More:Music|Pop:Music";
            var song = await Song.Create(s, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await song.CleanupProperties(dms, "B"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c + 1, props.Count);

            var i = props.FindIndex(p => p.BaseName == Song.UserField && p.Value == "batch-s|P");
            Assert.IsTrue(i != -1);
        }

        [TestMethod]
        public async Task AddSpotifyTagEnd()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");
            var s = @".Merge=6a844f27-ece1-48ee-ad90-492fafe1a6b9;ab4df0a0-ff1d-4713-a430-a692e59b1df9	.Create=	User=dwgray	Time=1/12/2015 1:00:05 PM	Title=At The Woodchoppers Ball	Artist=Woody Herman	Tempo=92.9	Length=266	.Edit=	Time=01/15/2016 00:42:56	Tag+=Big Band:Music|Jazz:Music	";
            var song = await Song.Create(s, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await song.CleanupProperties(dms, "B"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c + 1, props.Count);

            var i = props.FindIndex(p => p.BaseName == Song.UserField && p.Value == "batch-s|P");
            Assert.IsTrue(i != -1);
        }

        [TestMethod]
        public async Task AddSpotifyTagLarge()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");
            var s = @".Create=	User=dwgray	Time=1/12/2015 1:00:05 PM	Title=At The Woodchoppers Ball	Artist=Woody Herman	Tempo=92.9	Length=266	.Edit=	Time=12/10/2014 3:35:44 PM	Title=Crazy In Love (feat. JAY Z)	Album:04=Ultimate GRAMMY Collection: Contemporary R&B	Track:04=16	Album:05=No More Doubt	Track:05=11	Album:06=Tag Team	Track:06=4	Tag+=Pop:Music|R&B / Soul:Music|Soundtrack:Music";
            var song = await Song.Create(s, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await song.CleanupProperties(dms, "B"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c + 1, props.Count);

            var i = props.FindIndex(p => p.BaseName == Song.UserField && p.Value == "batch-s|P");
            Assert.IsTrue(i != -1);
        }

        [TestMethod]
        public async Task DontAddSpotifyTag()
        {
            await DontAct("B");
        }

        [TestMethod]
        public async Task FixupTime()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");
            var s = @".Create	User=batch-i|P	Title=Let It Whip	Artist=Dazz Band	Tempo=132.0	Length=289	Album:00=20th Century Masters: The Millennium Collection: Best Of The Dazz Band	Time=12/22/2014 2:54:10 PM	.Merge=ebd79d55-ce15-4fc5-80f3-46b70650ba7a;1c01b16b-75b9-400f-94db-7b3225bbd1f1	.Merge=ebd79d55-ce15-4fc5-80f3-46b70650ba7a;1c01b16b-75b9-400f-94db-7b3225bbd1f1	User=batch-x|P	Time=01/15/2016 00:42:58	Tag+=Jazz:Music|More:Music|Pop:Music";
            var song = await Song.Create(s, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await song.CleanupProperties(dms, "I"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c, props.Count);

            var i = props.FindIndex(p => p.BaseName == Song.TimeField);
            Assert.IsTrue(i == 2);
        }

        [TestMethod]
        public async Task FixupTimeLater()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");
            var s = @".Create	User=batch-i|P	Title=Let It Whip	Artist=Dazz Band	Tempo=132.0	Length=289	Album:00=20th Century Masters: The Millennium Collection: Best Of The Dazz Band	.Merge=ebd79d55-ce15-4fc5-80f3-46b70650ba7a;1c01b16b-75b9-400f-94db-7b3225bbd1f1	.Merge=ebd79d55-ce15-4fc5-80f3-46b70650ba7a;1c01b16b-75b9-400f-94db-7b3225bbd1f1	User=batch-x|P	Time=01/15/2016 00:42:58	Tag+=Jazz:Music|More:Music|Pop:Music";
            var song = await Song.Create(s, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await song.CleanupProperties(dms, "I"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c, props.Count - 1);

            var i = props.FindIndex(p => p.BaseName == Song.TimeField);
            Assert.IsTrue(i == 2);
        }

        [TestMethod]
        public async Task FixupTimeEarlier()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");
            var s = @".Create	User=batch-i|P	Time=01/15/2016 00:42:58	Title=Let It Whip	Artist=Dazz Band	Tempo=132.0	Length=289	Album:00=20th Century Masters: The Millennium Collection: Best Of The Dazz Band	.Create	User=batch-s	Tag+=Jazz:Music|More:Music|Pop:Music";
            var song = await Song.Create(s, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await song.CleanupProperties(dms, "I"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c, props.Count - 1);

            var i = props.FindLastIndex(p => p.BaseName == Song.TimeField);
            Assert.IsTrue(i == props.Count - 2);
        }

        [TestMethod]
        public async Task DontFixupTime()
        {
            await DontAct("I");
        }

        public async Task DontAct(string action)
        {
            var dms = await DanceMusicTester.CreateService("Cleanup");

            var songs = new List<Song>
            {
                await Song.Create(SongA, dms),
                await Song.Create(SongB, dms),
                await Song.Create(SongC, dms),
                await Song.Create(SongD, dms)
            };

            foreach (var song in songs)
            {
                Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
                DanceMusicTester.DumpSongProperties(song);
                var c = song.SongProperties.Count;

                Assert.IsFalse(await song.CleanupProperties(dms, action));

                Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
                DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

                var props = song.SongProperties;
                Assert.AreEqual(c, props.Count);
            }
        }

        [TestMethod]
        public async Task RemoveEmptyProperties()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");

            var song = await Song.Create(SongWithEmpties, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsTrue(await song.CleanupProperties(dms, "YE"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c - 5, props.Count);

            var i = props.FindIndex(p => p.Name == "Tag+:SWZ");
            Assert.IsTrue(i == -1);

            i = props.FindIndex(p => p.BaseName == "Sample");
            Assert.IsTrue(i == -1);

            i = props.FindIndex(p => p.BaseName == "User" && p.Value == "batch-s|P");
            Assert.IsTrue(i == -1);
        }

        [TestMethod]
        public async Task PurchasePreservesRealUser()
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");

            var song = await Song.Create(SongPurchaseEmbedded, dms);

            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);
            var c = song.SongProperties.Count;

            Assert.IsFalse(await song.CleanupProperties(dms, "P"));

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);

            var props = song.SongProperties;
            Assert.AreEqual(c, props.Count);

            var i = props.FindIndex(p => p.BaseName == "User" && p.Value == "PaulsPiano|P");
            Assert.IsTrue(i >= 0);

            i = props.FindIndex(p => p.BaseName == "User" && p.Value == "batch-i|P");
            Assert.IsTrue(i == -1);

        }

        [TestMethod]
        public async Task CheckEditProperitesCUT()
        {
            var s =
                @".Merge=15b7eca6-ef69-4b35-bdeb-79d2309e3bb0;0cd52dce-097c-46a9-8228-6effd35c48a8	.Create=	User=HunterZ|P	Time=3/17/2014 5:44:04 PM	Title=So What	Artist=Pink	Tempo=128.0	DanceRating=PDL+1	.Edit=	User=batch|P	Time=5/21/2014 10:02:05 AM	Title=So What (Main Version) [Explicit]	Length=215	.Edit=	User=LincolnA|P	Time=6/23/2014 2:06:11 PM	DanceRating=CHA+1	.Edit=	User=HunterZ|P	Time=11/20/2014 11:30:11 AM	Tag+=Paso Doble:Dance	.Edit=	User=LincolnA|P	Time=11/20/2014 11:30:11 AM	Tag+=Cha Cha:Dance	.Edit=	User=BonnieL|P	Time=08/25/2016 17:04:34	Tag+=Polka:Dance	DanceRating=PLK+1	.Edit=	User=dwts|P	Time=09/21/2017 01:12:31	Tag+=DWTS:Other|Episode 1:Other|Season 25:Other|Tango (Ballroom):Dance|United States:Other	DanceRating=TGO+1	Tag+:TGO=Artem:Other|Nikki:Other	.Edit=	User=dwgray	Time=09/21/2017 01:19:57	Title=So What [Explicit]	Artist=P!nk	Tag+=4/4:Tempo|Pop:Music	.Edit=	User=dwgray	Time=09/21/2017 01:20:38	Tempo=126.0	Danceability=0.531	Energy=0.864	Valence=0.415	.Edit=	User=batch-s|P	Time=09/21/2017 01:20:55	Sample=https://p.scdn.co/mp3-preview/91f9d6e2c692afb77bed3a40e7bf72e95ac6dd67?cid=e6dc118cd7604cd2b8bd0a979a18e6f8	.Create=	User=BrittanyFalconer|P	Time=09/04/2017 15:38:14	Title=So What	Artist=P!nk	Tag+=East Coast Swing:Dance	DanceRating=ECS+1	Tag+:ECS=American:Style	.Edit=	User=batch-a|P	Time=09/04/2017 15:42:41	Tag+=Pop:Music|Rock:Music	.Edit=	User=batch-i|P	Time=09/04/2017 15:42:42	Tag+=Pop:Music	.Edit=	User=batch-e|P	Time=09/04/2017 15:42:42	Tempo=126.0	Danceability=0.531	Energy=0.864	Valence=0.415	Tag+=4/4:Tempo	.Edit=	User=batch-a|P	Time=09/04/2017 15:57:24	Sample=https://p.scdn.co/mp3-preview/91f9d6e2c692afb77bed3a40e7bf72e95ac6dd67?cid=e6dc118cd7604cd2b8bd0a979a18e6f8	Album:00=Funhouse	Track:00=1	Purchase:00:AS=D:B001I8A76K	Purchase:00:AA=D:B001I8A76A	Purchase:00:IA=293525340	Purchase:00:IS=293525380	Purchase:00:SA=0wcuOAo2w5jxwp7N57QKNN	Purchase:00:SS=6qYGUxPjQt5PJtWdiNppZx	Album:01=Greatest Hits...So Far!!!	Track:01=11	Purchase:01:IA=399891666	Purchase:01:IS=399891704	Purchase:01:SA=2tUn9E3nHXhUIJ47yv6ePD	Purchase:01:SS=7bprRkhvOXWmWpqOrEWbXu	Purchase:01:AS=D:B004BCIR2Q	Purchase:01:AA=D:B004BCNVOA	Album:02=Funhouse Deluxe Version [Clean]	Track:02=1	Purchase:02:AS=D:B001JDI9JQ	Purchase:02:AA=D:B001JDGK2E	Album:03=Break Up Songs	Track:03=8	Purchase:03:IS=1072024229	Purchase:03:IA=1072024143	Purchase:03:SS=6YGOqe9dZuq1RT3AdisNRv	Purchase:03:SA=4d6jNVUcYZsdbtNUMtgwFT	.Edit=	User=TylerQJoslin|P	Time=11/07/2018 04:06:06	Tag+=East Coast Swing:Dance	DanceRating=ECS+1	Tag+:ECS=Modern:Style	.Edit=	User=batch-a|P	Time=11/07/2018 04:09:11	Purchase:00:AS=D:B001IKYM78	Purchase:00:AA=D:B001IKVOE2	Album:04=Orgullo Gay [Explicit]	Track:04=21	Purchase:04:AS=D:B07DPFSCM4	Purchase:04:AA=D:B07DPG9NQ8	Album:05=Mother's Day Songs	Track:05=7	Purchase:05:AS=D:B079YVS18X	Purchase:05:AA=D:B079YXML4K	Album:06=NOW 29	Track:06=1	Purchase:06:AS=D:B001NGRLZW	Purchase:06:AA=D:B001NGV7RU	Album:07=NOW That's What I Call Music 29, 30, 31	Track:07=1	Purchase:07:AS=D:B07GL33HDX	Purchase:07:AA=D:B07GKVTM14	.Edit=	User=batch-i|P	Time=11/07/2018 04:09:27	Purchase:00:IA=293000131	Purchase:00:IS=293000132	Purchase:04:IS=1398208558	Purchase:04:IA=1398206945	Purchase:05:IS=1351343163	Purchase:05:IA=1351342657	.Edit=	User=batch-s|P	Time=11/07/2018 04:09:27	Purchase:00:SA=600cuygR195BtgX6pW9dRI	Purchase:00:SS=33GNzfnEGKGMrbTKV06zSO	Purchase:04:SS=3IoH70oCPRLaqGr71wlSed	Purchase:04:SA=6K6cgPLQlWUKdw1sSv7tYb	Purchase:05:SS=0URDFJlzg4sKcPLSn5A8to	Purchase:05:SA=0LplRIGNE4ds9AmJ6w0qXa	Purchase:06:SS=3zWExspclz8jTHhpsfaZso	Purchase:06:SA=2HT0BoIdGpgU0ruIirMpax	.Edit=	User=batch-e|P	Time=11/07/2018 04:09:27	Tempo=126.0	Danceability=0.538	Energy=0.873	Valence=0.4	.Edit=	User=batch-s|P	Time=11/07/2018 04:09:27	Sample=https://p.scdn.co/mp3-preview/92844d81295d127d48a8a0d9a674161844cba193?cid=e6dc118cd7604cd2b8bd0a979a18e6f8	.Edit=	User=spotify|P	Time=02/25/2019 03:25:02	Tag+=2000S:Other	.Edit=	User=batch-s|P	Time=02/25/2019 04:07:08	Purchase:00:SS=33GNzfnEGKGMrbTKV06zSO	Purchase:01:SS=7bprRkhvOXWmWpqOrEWbXu	Purchase:04:SS=3IoH70oCPRLaqGr71wlSed	Purchase:05:SS=0URDFJlzg4sKcPLSn5A8to	.Edit=	User=batch-e|P	Time=02/25/2019 04:07:08	Tempo=126.0	.Edit=	User=BatesBallroom|P	Time=03/17/2019 11:55:38	Tag+=East Coast Swing:Dance	DanceRating=ECS+1	Tag+:ECS=American:Style	.Edit=	User=batch-s|P	Time=03/17/2019 12:07:30	Purchase:00:SS=33GNzfnEGKGMrbTKV06zSO	Purchase:04:SS=3IoH70oCPRLaqGr71wlSed	Purchase:05:SS=0URDFJlzg4sKcPLSn5A8to	.Edit=	User=batch-e|P	Time=03/17/2019 12:07:30	Tempo=126.0";
            await CheckProperties(s);
        }

        [TestMethod]
        public async Task CheckEditProperitesCTU()
        {
            var s =
                @".Create=	Time=1/12/2015 1:00:05 PM	User=dwgray	Title=At The Woodchoppers Ball	Artist=Woody Herman	Tempo=92.9	Length=266	.Edit=	Time=01/15/2016 00:42:56	User=batch-a|P	Tag+=Big Band:Music|Jazz:Music";
            await CheckProperties(s);
        }

        private async Task CheckProperties(string s)
        {
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");

            var song = await Song.Create(s, dms);
            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);

            Assert.IsTrue(await song.CheckProperties());

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
        }

        [TestMethod]
        public async Task CleanupFailed()
        {
            var s =
                @".Create=	User=dwgray	OwnerHash=-703143399	Time=1/12/2015 12:58:58 PM	Title=Winter Wheat	Artist=Michelle Shocked	Tempo=111.4	Length=256	Album:00=Kind Hearted Woman	Track:00=3	Publisher:00=Private Music	.Edit=	User=batch-i|P	Time=01/15/2016 19:30:37	Purchase:00:IS=182954533	Purchase:00:IA=182953567	Tag+=Rock:Music	.FailedLookup=-:0";
            var dms = await DanceMusicTester.CreateServiceWithUsers("Cleanup");

            var song = await Song.Create(s, dms);
            Trace.WriteLine(General.TraceInfo, $"---------------Predump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song);

            Assert.IsTrue(song.FilteredProperties(".FailedLookup").Any());
            Assert.IsTrue(song.RemoveEmptyProperties());
            Assert.IsFalse(song.FilteredProperties(".FailedLookup").Any());

            Trace.WriteLineIf(General.TraceInfo, $"---------------Postdump for Song {song.SongId}");
            DanceMusicTester.DumpSongProperties(song, General.TraceInfo);
        }


        [TestMethod, Ignore]
        public void DumpCleanupCount()
        {
            Song.DumpCleanupCount();
        }

        
    }
}

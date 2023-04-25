using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests
{
    [TestClass]
    public class CleanupTests
    {
        private const string SongA =
            @"SongId={defd92b3-0a2a-4d49-b8ec-cc57fcb6fa80}	.Merge=03245880-548c-45cf-a3a0-5798de4db80a;ee7c202c-9aea-479f-9dde-7a4130c71723;2c40655a-eee6-4611-a03f-ded86b7c5cc6	User=dwgray	Time=07/31/2015 18:34:41	.Create=	User=TaylorZ	Time=05/07/2015 15:35:43	Title=From This Moment	Artist=Shania Twain / Bryan White	Tempo=52.0	Tag+=Castle Foxtrot:Dance|First Dance:Other|Wedding:Other	DanceRating=CFT+3	DanceRating=FXT+1	.Create=	User=OliviaL	Time=10/1/2014 5:29:24 PM	Title=From This Moment	Artist=Shania Twain	Tempo=66.0	DanceRating=CFT+5	User=batch	Time=10/9/2014 11:00:27 AM	DanceRating=FXT+3	User=batch	Time=10/9/2014 12:09:09 PM	DanceRating=CFT+4	User=OliviaL	Time=11/20/2014 11:32:10 AM	Tag+=Castle Foxtrot:Dance	.Edit=	User=AshleyF	Time=5/7/2015 5:36:09 PM	Tag+=Castle Foxtrot:Dance|First Dance:Other|Wedding:Other	DanceRating=CFT+3	DanceRating=FXT+1	.Create=	User=AudreyK	Time=6/21/2014 8:22:43 PM	Title=From This Moment On	Artist=Shania Twain	DanceRating=WLZ+5	User=batch	Time=6/21/2014 8:24:01 PM	Length=235	PromoteAlbum:00=	User=batch	Time=6/21/2014 8:25:25 PM	PromoteAlbum:01=	User=batch	Time=6/21/2014 8:26:17 PM	Title=From This Moment On (Pop On-Tour Version)	User=AudreyK	Time=9/4/2014 8:07:13 PM	User=batch	Time=9/4/2014 8:07:13 PM	User=AudreyK	Time=9/4/2014 8:12:15 PM	User=batch	Time=9/4/2014 8:12:15 PM	User=AudreyK	Time=11/20/2014 11:29:16 AM	Tag+=Waltz:Dance	User=batch	Time=11/20/2014 11:29:16 AM	Tag+=Country:Music	.Edit=	User=batch	Time=12/9/2014 3:22:57 PM	.Edit=	User=batch-a	Time=12/10/2014 4:05:12 PM	Tag+=country:Music	.Edit=	User=batch-i	Time=12/10/2014 4:05:14 PM	Title=From This Moment On	.Edit=	User=batch-x	Time=12/10/2014 4:05:14 PM	Tag+=Country:Music	.Edit=	User=batch-s	Time=1/6/2015 4:41:47 PM	Length=237	.Edit=	User=LucyM	Time=5/7/2015 6:22:36 PM	Tag+=First Dance:Other|Slow Waltz:Dance|Wedding:Other	DanceRating=SWZ+3	DanceRating=WLZ+1	.Edit=	User=dwgray	Time=07/31/2015 18:34:41	Title=From This Moment	Artist=Shania Twain / Bryan White	Album:00=Greatest Hits	Track:00=7	Purchase:00:IS=27890875	Purchase:00:IA=27890832	Purchase:00:XS=music.75DB2900-0100-11DB-89CA-0019B92A3933	Purchase:00:AS=D:B001NZ1ERE	Purchase:00:AA=D:B001NZ3DY6	Album:01=Come on Over	.Edit=	User=dwgray	Time=07/31/2015 18:37:43	Tempo=68.8	OrderAlbums=1,0	Tag+=Castle Foxtrot:Dance|First Dance:Other|Slow Foxtrot:Dance|Smooth:Tempo|Wedding:Other	DanceRating=CFT+3	DanceRating=SFT+4	Tag+:SWZ=Fake:Tempo	Tag+:WLZ=Fake:Tempo	Tag+:SFT=double-time:Tempo	DanceRating=FXT+2	DanceRating=CFT+1	.FailedLookup=-:0	.Edit=	User=batch-s	Time=02/17/2016 16:28:00	Sample=http://a1928.phobos.apple.com/us/r1000/120/Music/bb/df/c6/mzm.pfgqymeb.aac.p.m4a";

        private const string SongB =
            @"SongId={250b462b-5f8f-420c-81cc-74cdcc03a48f}	.Create=	User=LucyM	Time=05/07/2015 18:23:10	Title=The Way You Look Tonight	Artist=Frank Sinatra	Tag+=First Dance:Other|Slow Waltz:Dance|Wedding:Other	DanceRating=SWZ+3	DanceRating=WLZ+1	.Edit=	User=batch-a	Time=5/7/2015 6:25:53 PM	Length=201	Tag+=jazz:Music|pop:Music	.Edit=	User=batch-i	Time=5/7/2015 6:25:53 PM	Length=202	Tag+=Jazz:Music|Vocal Pop:Music	.Edit=	User=batch-s	Time=5/7/2015 6:25:53 PM	Length=201	.Edit=	User=batch-x	Time=5/7/2015 6:25:55 PM	Length=197	Tag+=Jazz:Music|Pop:Music	.Create=	User=AshleyF	Time=05/07/2015 17:41:00	Title=The Way You Look Tonight	Artist=Frank Sinatra	Tag+=First Dance:Other|Slow Foxtrot:Dance|Wedding:Other	DanceRating=SFT+3	DanceRating=FXT+1	.Edit=	User=batch-a	Time=5/7/2015 6:13:27 PM	Length=201	Tag+=jazz:Music|pop:Music	.Edit=	User=batch-i	Time=5/7/2015 6:13:27 PM	Length=202	Tag+=Jazz:Music|Vocal Pop:Music	.Edit=	User=batch-s	Time=5/7/2015 6:13:27 PM	Length=201	.Edit=	User=batch-x	Time=5/7/2015 6:13:29 PM	Length=197	Tag+=Jazz:Music|Pop:Music	.Create=	User=TaylorZ	Time=05/07/2015 15:39:50	Title=The Way You Look Tonight	Artist=Frank Sinatra	Tempo=120.0	Tag+=First Dance:Other|Slow Foxtrot:Dance|Wedding:Other	DanceRating=SFT+4	DanceRating=FXT+1	.Edit=	User=TaylorZ	Time=5/7/2015 3:45:32 PM	Tag+=Father Daughter:Other	DanceRating=SFT+4	DanceRating=FXT+1	.Edit=	User=TaylorZ	Time=5/7/2015 3:46:24 PM	Tag+=Mother Son:Other	DanceRating=SFT+4	DanceRating=FXT+1	.Create=	User=dwgray	OwnerHash=551960923	Time=1/12/2015 12:58:27 PM	Title=The Way You Look Tonight	Artist=Frank Sinatra	Tempo=130.5	Length=203	.Edit=	User=AdamT	Time=4/15/2015 9:15:26 PM	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+6	DanceRating=FXT+1	Tag+:SFT=Traditional:Style	.Edit=	User=batch-a	Time=4/15/2015 9:23:51 PM	Title=The Way You Look Tonight (Remastered)	Length=200	Tag+=easy-listening:Music|jazz:Music	.Edit=	User=batch-i	Time=4/15/2015 9:23:51 PM	Title=The Way You Look Tonight	Length=202	Tag+=Vocal Pop:Music	.Edit=	User=batch-s	Time=4/15/2015 9:23:52 PM	Length=205	.Edit=	User=batch-x	Time=4/15/2015 9:23:53 PM	Length=200	Tag+=Jazz:Music|Pop:Music	.Create=	User=batch	Time=3/19/2014 12:35:20 PM	Title=The Way You Look Tonight	Artist=Frank Sinatra	Tempo=128.0	User=HunterZ	User=ChaseP	DanceRating=ECS+10	DanceRating=SFT+5	User=batch	Time=5/7/2014 1:58:13 PM	Length=158	PromoteAlbum:0=	User=AudreyK	Time=6/21/2014 8:22:43 PM	DanceRating=WLZ+6	User=OliviaL	Time=10/1/2014 5:29:29 PM	DanceRating=CFT+6	User=OliviaL	Time=10/1/2014 5:55:42 PM	DanceRating=ECS+3	User=OliviaL	Time=10/2/2014 11:51:11 AM	DanceRating=JIV+6	User=OliviaL	Time=10/2/2014 1:47:19 PM	DanceRating=SFT+3	User=OliviaL	Time=10/2/2014 1:47:27 PM	DanceRating=SFT+3	User=batch	Time=10/9/2014 11:00:33 AM	DanceRating=FXT+5	DanceRating=SWG+5	User=batch	Time=10/9/2014 12:09:15 PM	DanceRating=SFT+4	DanceRating=CSG+4	DanceRating=WCS+4	DanceRating=LHP+4	User=HunterZ	Time=11/20/2014 11:32:25 AM	Tag+=East Coast Swing:Dance|Slow Foxtrot:Dance	User=ChaseP	Time=11/20/2014 11:32:25 AM	Tag+=East Coast Swing:Dance|Slow Foxtrot:Dance	User=AudreyK	Time=11/20/2014 11:32:25 AM	Tag+=Waltz:Dance	User=batch	Time=11/20/2014 11:32:25 AM	Tag+=Pop:Music	User=OliviaL	Time=11/20/2014 11:32:25 AM	Tag+=Castle Foxtrot:Dance|East Coast Swing:Dance|Jive:Dance|Slow Foxtrot:Dance	.Edit=	User=batch-a	Time=12/10/2014 8:48:02 PM	Length=157	Tag+=jazz:Music|pop:Music	.Edit=	User=batch-i	Time=12/10/2014 8:48:02 PM	Length=162	Tag+=Jazz:Music|Pop:Music|Vocal Jazz:Music|Vocal:Music	.Edit=	User=batch-x	Time=12/10/2014 8:48:03 PM	Length=157	Tag+=Jazz:Music|Pop:Music	.Edit=	User=batch-s	Time=1/6/2015 5:01:17 PM	Length=162	.Merge=619377cc-ebdd-4e2f-a39a-04ddb34c11b1;2c217c31-c5d2-40ee-879c-96557384a303;a369451c-f0b2-4b15-b0dc-caa704d2e5cf;c8aefccb-3e0f-4f95-b820-d0e4f9b7b5c8;ed4c365f-3d93-4715-8ca0-fabaf730d4c2	User=batch	Time=06/10/2015 14:23:25	.Edit=	User=batch	Time=6/10/2015 2:23:27 PM	Tempo=128.0	Album:00=Nothing But the Best (Remastered)	Track:00=3	Purchase:00:AS=D:B00FHU141M	Purchase:00:AA=D:B00FHU10UM	Purchase:00:IS=717552720	Purchase:00:IA=717552717	Album:01=Ultimate Sinatra	Track:01=15	Purchase:01:AS=D:B00TU1BHKW	Purchase:01:AA=D:B00TU1AI84	Purchase:01:IS=969299403	Purchase:01:IA=969298500	Purchase:01:SS=0shGCs5AkhwJIgUb0SSz2B[AU,CA,MX,US]	Purchase:01:SA=4G6ZaR4A7tkkMsglaYpDeS	Purchase:01:XS=music.31C3DE08-0100-11DB-89CA-0019B92A3933	Album:02=Sinatra, With Love	Track:02=7	Purchase:02:AS=D:B00HWM17ME	Purchase:02:AA=D:B00HWM11OS	Purchase:02:XS=music.85701508-0100-11DB-89CA-0019B92A3933	Album:03=Days Of Wine And Roses, Moon River And Other Academy Award Winners	Track:03=3	Purchase:03:AS=D:B00FAXHGXQ	Purchase:03:AA=D:B00FAXHDPC	Purchase:03:IS=711636279	Purchase:03:IA=711636230	Purchase:03:SS=0elmUoU7eMPwZX1Mw1MnQo[0]	Purchase:03:SA=1BA6ebXEZ79g2TQ1o6dklD	Purchase:03:XS=music.6D54E807-0100-11DB-89CA-0019B92A3933	Album:04=Ultimate Sinatra: The Centennial Collection	Track:04=77	Purchase:04:AS=D:B00V07JT8G	Purchase:04:AA=D:B00V07G4H0	Purchase:04:IS=978885100	Purchase:04:IA=978881306	Purchase:04:SS=2VNazQ7xXeC0o3p27nSti0[1]	Purchase:04:SA=22KCcelRH1AeHz7S7x5XhY	Purchase:04:XS=music.6BA4E908-0100-11DB-89CA-0019B92A3933	Album:05=Sinatra, With Love (Remastered)	Track:05=7	Purchase:05:IS=798940769	Purchase:05:IA=798940614	Album:06=Nothing But The Best	Track:06=3	Purchase:06:XS=music.F734EB07-0100-11DB-89CA-0019B92A3933	Album:07=Days Of Wine And Roses	Track:07=3	Purchase:07:XS=music.BB08A006-0100-11DB-89CA-0019B92A3933	Album:08=30 Grandes de Frank Sinatra	Track:08=29	Purchase:08:XS=music.931AEC06-0100-11DB-89CA-0019B92A3933	Album:09=15 Grandes Exitos de Frank Sinatra Vol. 2	Track:09=14	Purchase:09:AS=D:B00OBO8C5Q	Purchase:09:AA=D:B00OBO65S2	Purchase:09:XS=music.D3629408-0100-11DB-89CA-0019B92A3933	Album:10=The ""V Discs"" - The Columbia Years 1943 - 1952	Track:10=6	Purchase:10:AS=D:B00137XETS	Purchase:10:AA=D:B00138EWXE	Purchase:10:XS=music.11B0AE00-0100-11DB-89CA-0019B92A3933	Purchase:10:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:10:SA=4F2N7GHRi69w5dxeFY9WLD	Album:11=The Columbia Years 1943-1952: The V-Discs	Track:11=6	Purchase:11:IS=264571453	Purchase:11:IA=264571342	Purchase:11:XS=music.E1900D00-0100-11DB-89CA-0019B92A3933	Album:12=The Rat Pack (Classic Collection Presents)	Track:12=35	Purchase:12:IS=945805319	Purchase:12:IA=945800842	Album:13=50 Traditional Jazz Standards, Vol. 1	Track:13=27	Purchase:13:IS=719456321	Purchase:13:IA=719455421	Album:14=75 of the Best from Edith, Doris, Bing, Ella and Others	Track:14=44	Purchase:14:IS=719441859	Purchase:14:IA=719439994	Album:15=Frank Sinatra Volume 2	Track:15=1	Purchase:15:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:15:SA=2dOOt2mKM8BI0LcEAot24a	Album:16=The One and Only: Frank Sinatra (Remastered)	Track:16=13	Purchase:16:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:16:SA=4p3uNhVACh7egypBw5HRTQ	Album:17=They Way You Look Tonight	Track:17=1	Purchase:17:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:17:SA=5QmrC9fUp50K8uYqAzgulL	Album:18=Behind The Legend - Frank Sinatra	Track:18=8	Purchase:18:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:18:SA=1YBmaTO8hzRgU7u6lq8Ld3	Album:19=Jazz Giants: Frank Sinatra	Track:19=4	Purchase:19:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:19:SA=6LR4TdpIkdDvGccE3Jqv6T	Album:20=The Night Will Never End (Ultimate Legends Presents Frank Sinatra)	Track:20=6	Purchase:20:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:20:SA=5cqQHZBoEeUHRuH60gNNvR	Album:21=Beyond Patina Jazz Masters: Frank Sinatra	Track:21=16	Purchase:21:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:21:SA=2N9yk7JG4YnnEGugjWu5oU	Album:22=Moments (Ultimate Legends Presents Frank Sinatra)	Track:22=6	Purchase:22:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:22:SA=0vURInfVzaTsIS0v5jNBtB	Album:23=There's No Business Like Show Business Volume 3	Track:23=8	Purchase:23:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:23:SA=1Z3ox51c3Z9XUDS6LnxLtp	Album:24=Close To You	Track:24=15	Purchase:24:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:24:SA=7bjUs0jXoIU87xq4uSV0v7	Album:25=La Voz De Frank Sinatra	Track:25=13	Purchase:25:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:25:SA=7s1VXAEVTrFUNfT4n0w7l7	Album:26=The V Discs	Track:26=6	Purchase:26:SS=6jmlP3dDpGIoASQ9Ij2U1C[0]	Purchase:26:SA=7v4Q8mQZKNjX2WsWWRjzKm	Album:27=The Very Good Years	Track:27=5	Album:28=Nothing But the Best (Remastered)	Track:28=3	Purchase:28:AS=D:B00FHU141M	Purchase:28:AA=D:B00FHU10UM	Purchase:28:IS=717552720	Purchase:28:IA=717552717	Album:29=Days Of Wine And Roses, Moon River And Other Academy Award Winners	Track:29=3	Purchase:29:AS=D:B00FAXHGXQ	Purchase:29:AA=D:B00FAXHDPC	Purchase:29:SS=0elmUoU7eMPwZX1Mw1MnQo[1]	Purchase:29:SA=7FAo3wmrJNNzz2W5Z5ZG80	Purchase:29:XS=music.6D54E807-0100-11DB-89CA-0019B92A3933	Album:30=Ultimate Sinatra	Track:30=15	Purchase:30:AS=D:B00TU1BHKW	Purchase:30:AA=D:B00TU1AI84	Album:31=Ultimate Sinatra: The Centennial Collection	Track:31=1	Purchase:31:AS=D:B00V07JT8G	Purchase:31:AA=D:B00V07G4H0	Album:32=Sinatra, With Love	Track:32=7	Purchase:32:AS=D:B00HWM17ME	Purchase:32:AA=D:B00HWM11OS	Purchase:32:XS=music.85701508-0100-11DB-89CA-0019B92A3933	Album:33=Sinatra, With Love (Remastered)	Track:33=7	Purchase:33:IS=798940769	Purchase:33:IA=798940614	Album:34=Unforgettable Songs	Track:34=22	Purchase:34:SS=5j4oXrueFBcaZoCdwd0iMi	Purchase:34:SA=0VAWFK1jEZvekVaZKm8Fls	Album:35=Nothing But The Best	Track:35=3	Purchase:35:XS=music.F734EB07-0100-11DB-89CA-0019B92A3933	Album:36=Very Best Of	Album:37=Nothing But the Best (Remastered)	Track:37=3	Purchase:37:AS=D:B00FHU141M	Purchase:37:AA=D:B00FHU10UM	Purchase:37:IS=717552720	Purchase:37:IA=717552717	Album:38=Ultimate Sinatra	Track:38=15	Purchase:38:AS=D:B00TU1BHKW	Purchase:38:AA=D:B00TU1AI84	Purchase:38:IS=969299403	Purchase:38:IA=969298500	Purchase:38:SS=0shGCs5AkhwJIgUb0SSz2B[AU,CA,MX,US]	Purchase:38:SA=4G6ZaR4A7tkkMsglaYpDeS	Purchase:38:XS=music.31C3DE08-0100-11DB-89CA-0019B92A3933	Album:39=Sinatra, With Love	Track:39=7	Purchase:39:AS=D:B00HWM17ME	Purchase:39:AA=D:B00HWM11OS	Purchase:39:XS=music.85701508-0100-11DB-89CA-0019B92A3933	Album:40=Days Of Wine And Roses, Moon River And Other Academy Award Winners	Track:40=3	Purchase:40:AS=D:B00FAXHGXQ	Purchase:40:AA=D:B00FAXHDPC	Purchase:40:IS=711636279	Purchase:40:IA=711636230	Purchase:40:SS=0elmUoU7eMPwZX1Mw1MnQo[0]	Purchase:40:SA=1BA6ebXEZ79g2TQ1o6dklD	Purchase:40:XS=music.6D54E807-0100-11DB-89CA-0019B92A3933	Album:41=Ultimate Sinatra: The Centennial Collection	Track:41=77	Purchase:41:AS=D:B00V07JT8G	Purchase:41:AA=D:B00V07G4H0	Purchase:41:IS=978885100	Purchase:41:IA=978881306	Purchase:41:SS=2VNazQ7xXeC0o3p27nSti0[1]	Purchase:41:SA=22KCcelRH1AeHz7S7x5XhY	Purchase:41:XS=music.6BA4E908-0100-11DB-89CA-0019B92A3933	Album:42=Sinatra, With Love (Remastered)	Track:42=7	Purchase:42:IS=798940769	Purchase:42:IA=798940614	Album:43=Nothing But The Best	Track:43=3	Purchase:43:XS=music.F734EB07-0100-11DB-89CA-0019B92A3933	Album:44=Days Of Wine And Roses	Track:44=3	Purchase:44:XS=music.BB08A006-0100-11DB-89CA-0019B92A3933	.Edit=	User=dwgray	Time=09/14/2015 21:20:16	Tempo=132.0	Tag+=Castle Foxtrot:Dance|Slow Foxtrot:Dance	DanceRating=CFT+3	DanceRating=SFT+3	Tag+:CFT=half-time:Tempo	Tag+:ECS=Slow:Tempo	Tag+:SWZ=Fake:Tempo	Tag+:WCS=Fast:Tempo	Tag+:WLZ=Fake:Tempo	DanceRating=FXT+2	DanceRating=SFT+1	.Edit=	User=batch	Time=01/11/2016 18:35:07	Album:00=Nothing But The Best	Purchase:00:XS=music.F734EB07-0100-11DB-89CA-0019B92A3933	Purchase:02:IS=798940769	Purchase:02:IA=798940614	Album:05=	Track:05=	Album:06=	Track:06=	Album:28=	Track:28=	Album:29=	Track:29=	Album:30=	Track:30=	Album:32=	Track:32=	Album:33=	Track:33=	Album:35=	Track:35=	Album:37=	Track:37=	Album:38=	Track:38=	Album:39=	Track:39=	Album:40=	Track:40=	Album:41=	Track:41=	Album:42=	Track:42=	Album:43=	Track:43=	Album:44=	Track:44=	OrderAlbums=0,1,4,31,2,3,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,34,36	.FailedLookup=-:0	.Edit=	User=batch-s	Time=02/08/2016 19:39:01	Sample=https://p.scdn.co/mp3-preview/e05c5b2c4090c64ff22a9353a06915ddd5dd3659	.Edit=	User=batch-e	Time=02/09/2016 19:07:21	Tempo=132.9	Danceability=0.5561369	Energy=0.3706279	Valence=0.490889	Tag+=4/4:Tempo";

        private const string SongC =
            @"SongId={05178359-eaae-4463-8de1-4c79c403adb4}	.Merge=467220be-feac-4184-af47-1b15213bbc9b;0e288e64-6158-43fa-95b2-43dad1d085bf	User=batch	Time=06/12/2015 11:36:15	.Edit=	User=batch	Time=6/12/2015 11:36:15 AM	Title=I Get a Kick out of You	Tempo=102.5	Album:00=The Very Good Years	Track:00=3	Album:01=Sinatra and Swingin' Brass	Track:01=6	Purchase:01:AS=D:B00FAXDMWU	Purchase:01:AA=D:B00FAXDD7Y	Purchase:01:IS=923098168	Purchase:01:IA=923094701	Album:02=X-Mas, Vol. 5 (Let It Snow! Let It Snow! Let It Snow!)	Track:02=10	Purchase:02:IS=487823078	Purchase:02:IA=487822808	Album:03=In French or English - 3	Track:03=9	Purchase:03:IS=656991553	Purchase:03:IA=656991512	Album:04=In French or English ? 2	Track:04=17	Purchase:04:IS=656625882	Purchase:04:IA=656625397	Album:05=Jukebox Favourites - Best of Swing	Track:05=59	Purchase:05:IS=814204173	Purchase:05:IA=814204056	Album:06=Song Shop - Song Doubles	Track:06=6	Purchase:06:IS=663133012	Purchase:06:IA=663132879	Album:07=Song Doubles Volume One	Track:07=18	Purchase:07:IS=943867362	Purchase:07:IA=943867288	Album:08=Your Birthday Present - Song Doubles	Track:08=18	Purchase:08:IS=660024227	Purchase:08:IA=660024148	Album:09=Frank Sinatra & Sextet: In Paris - Live	Track:09=15	Purchase:09:SS=3tMZgBzebiTyQTP788GQM9	Purchase:09:SA=0U0dbAPfsrCn3P1ewtyJj0	.Create=	User=dwgray	OwnerHash=-984185430	Time=1/12/2015 12:58:27 PM	Title=I Get A Kick Out Of You	Artist=Frank Sinatra	Tempo=102.5	Length=194	.Edit=	User=AdamT	Time=4/16/2015 8:26:04 AM	Tag+=QuickStep:Dance	DanceRating=QST+4	DanceRating=FXT+1	Tag+:QST=Traditional:Style	.Edit=	User=batch-a	Time=4/16/2015 8:29:28 AM	Tag+=big-band-swing:Music	.Edit=	User=batch-i	Time=4/16/2015 8:29:29 AM	Title=I Get a Kick Out of You	Length=189	Tag+=Jazz:Music|Pop:Music|Swing:Music|Vocal Jazz:Music	.Edit=	User=batch-s	Time=4/16/2015 8:29:29 AM	Title=I Get a Kick out of You	Length=186	.Create=	User=TaylorZ	Time=05/07/2015 15:36:09	Title=I Get A Kick Out of You	Artist=Frank Sinatra	Tempo=144.0	Tag+=East Coast Swing:Dance|First Dance:Other|Foxtrot:Dance|Wedding:Other	DanceRating=FXT+3	DanceRating=ECS+4	DanceRating=SWG+1	DanceRating=LHP+1	.Edit=	User=TaylorZ	Time=5/7/2015 3:45:21 PM	Tag+=Father Daughter:Other|Slow Foxtrot:Dance	DanceRating=SFT+3	DanceRating=ECS+4	DanceRating=SWG+1	DanceRating=FXT+1	DanceRating=LHP+1	.Edit=	User=TaylorZ	Time=5/7/2015 3:46:18 PM	Tag+=Mother Son:Other	DanceRating=SFT+3	DanceRating=ECS+4	DanceRating=SWG+1	DanceRating=FXT+1	DanceRating=LHP+1	.Edit=	User=batch-x	Time=01/13/2016 21:54:27	Purchase:07:XS=music.0A82AA08-0100-11DB-89CA-0019B92A3933	Tag+=Pop:Music	.FailedLookup=-:0	.Edit=	User=batch-s	Time=02/08/2016 22:22:14	Sample=.	.Edit=	User=batch-e	Time=02/09/2016 19:31:22	Tempo=140.1	Danceability=0.5725134	Energy=0.3595052	Valence=0.4253719	Tag+=4/4:Tempo	.Edit=	User=batch-s	Time=02/17/2016 16:27:27	Sample=.";

        private const string SongD =
            @"SongId={9e8317e8-a965-4cd5-8091-b1d08b1cdef2}	.Create=	User=HunterZ	Time=3/17/2014 5:43:50 PM	Title=Bye Bye Blackbird	Artist=Sammy Davis Jr	Tempo=116.0	Time=9/4/2014 8:12:02 PM	User=batch	Time=9/4/2014 8:12:02 PM	User=batch	Time=10/9/2014 12:09:30 PM	DanceRating=SFT+4	User=HunterZ	Time=11/20/2014 11:32:39 AM	Tag+=Foxtrot:Dance	User=batch	Time=11/20/2014 11:32:39 AM	Tag+=Jazz:Music	User=HunterZ	Time=9/4/2014 8:06:58 PM	User=batch	OrderAlbums=1,0	Time=9/4/2014 8:06:58 PM	User=HunterZ	Artist=Sammy Davis Jr	Time=6/16/2014 12:41:48 PM	User=batch	PromoteAlbum:1=	Purchase:1:XS=music.086DC007-0100-11DB-89CA-0019B92A3933	Track:1=36	Album:1=The Great American Song Book	Length=171	Artist=Bye Bye Blackbird	Time=5/21/2014 2:05:46 PM	User=batch	DanceRating=FXT+5	Album:0=The Ultimate Ballroom Album 10	.FailedLookup=-:0";

        private const string SongE =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602c}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Harmonica Man (rekmix)	Artist=Paul Lamb & the King Snakes	Album:0=Harmonica Man	DanceRating=PLK+5	User=batch	Time=6/11/2014 9:02:15 PM	Title=Harmonica Man	Artist=Paul Lamb & The King Snakes	Length=243	Album:01=Harmonica Man - The Paul Lamb Anthology 1986-2002	Track:01=17	Purchase:01:XS=music.EB85DB07-0100-11DB-89CA-0019B92A3933	PromoteAlbum:01=	User=HunterZ	Time=9/4/2014 8:06:30 PM	User=batch	Time=9/4/2014 8:06:30 PM	User=HunterZ	Time=9/4/2014 8:11:32 PM	User=batch	Time=9/4/2014 8:11:32 PM	User=HunterZ	Time=11/20/2014 11:29:06 AM	Tag+=Polka:Dance	User=batch	Time=11/20/2014 11:29:06 AM	Tag+=Blues / Folk:Music	.Edit=	User=batch-a	Time=12/10/2014 6:22:32 PM	Artist=Paul Lamb And The King Snakes	Length=246	Track:01=1	Purchase:01:AS=D:B00E74SKH0	Purchase:01:AA=D:B00E74RS46	Album:02=Harmonica Man: The Anthology 1986-2002	Track:02=1	Purchase:02:AS=D:B000SHB22K	Purchase:02:AA=D:B000S59N3M	Tag+=alternative:Music|blues:Music	.Edit=	User=batch-i	Time=12/10/2014 6:22:33 PM	Artist=Paul Lamb & The King Snakes	Length=244	Purchase:01:IS=682496811	Purchase:01:IA=682496394	Tag+=Blues:Music	.Edit=	User=batch-x	Time=12/10/2014 6:22:33 PM	Length=243	Track:01=17	Tag+=Blues / Folk:Music	.Edit=	User=batch-s	Time=1/6/2015 4:52:58 PM	Length=244	Track:01=2001	Purchase:01:SS=5PEcFNy8EO8TseIRWE1f1H[0]	Purchase:01:SA=1RNDiDdVEPepXslQPh1lBm	OrderAlbums=1,0,2	.Edit=	User=batch	Time=01/11/2016 18:29:57	Album:02=Harmonica Man	Album:00=	.FailedLookup=-:0	.Edit=	User=dwgray	Time=02/06/2016 01:42:39	Tag+=Polka:Dance	DanceRating=PLK+2	.Edit=	User=dwgray	Time=02/06/2016 01:42:45	Tag+=!Polka:Dance	Tag-=Polka:Dance	DanceRating=PLK-3	.Edit=	User=batch-s	Time=02/08/2016 19:25:07	Sample=https://p.scdn.co/mp3-preview/53916c89ece0d94242dac076fe91f8f86d8a3713	.Edit=	User=batch-e	Time=02/09/2016 18:04:50	Tempo=122.1	Danceability=0.5333328	Energy=0.9064238	Valence=0.7872825	Tag+=4/4:Tempo";

        private const string SongLength =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602d}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Test Track	Artist=Test Artist	Length=2:05";

        private const string ExtraLength =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602d}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Test Track	Artist=Test Artist	Length=1:02:05";

        private const string SongPurchase =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602d}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Test Track	Album:01=Test Album	Track:01=6	Purchase:01:MS=T B00FAXDMWU";

        private const string SongDuplicateTags =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602d}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Test Track	Tag+=International:Style|Pop:Music|Pop:Music|Zoom:Other	Tag+=International:Style|Soundtrack:Music|Soundtracks:Music|soundtracks:Music|soundtrack:Music|Zoom:Other";

        private const string SongBadCategoryTags =
            @"SongId={a8cace40-03bc-47bf-b781-47a817a7602d}	.Create=	User=HunterZ	Time=3/17/2014 5:46:07 PM	Title=Test Track	Tag+=Christmas: Pop	User=FlowZ	Time=3/17/2014 5:46:08 PM	Tag+=Christmas: Other";

        public CleanupTests()
        {
            General = new TraceSwitch("General", "All Tests");
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

            var deltas = new List<int> { 13, 4 };

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
                new() { 1, 4, 5, 23 },
                new() { 17, 135, 152, 186 },
                new() { 2, 2, 13, 15 },
                new() { 0, 2, 2, 8 },
                new() { 4, 8, 11, 19 }
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
    }
}

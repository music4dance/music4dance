using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

//using System.Diagnostics;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongDetailTests
    {
        private const string SHeader = @"Title	Artist	BPM	Dance	Album	AMAZONTRACK	ITUNES";

        //private static readonly string[] RowPopsCreate =
        //{
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Black Sheep	Artist=Gin Wigmore	Tempo=120.0	Tag+=West Coast Swing:Dance	DanceRating=WCS+6	DanceRating=SWG+1	DanceRating=CSG+1	DanceRating=HST+1	DanceRating=LHP+1	Album:00=Gravel & Wine [+digital booklet]	Purchase:00:AS=D:B00BYKXC82",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=The L Train	Artist=Gabriel Yared	Tempo=72.0	Tag+=Slow Waltz:Dance	DanceRating=SWZ+5	DanceRating=WLZ+1	Album:00=Shall We Dance?	Purchase:00:AS=D:B001NYTZJY",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Come Wake Me Up	Artist=Rascal Flatts	Tempo=153.0	Tag+=Viennese Waltz:Dance	DanceRating=VWZ+5	DanceRating=WLZ+1	Album:00=Changed (Deluxe Version) [+Digital Booklet]	Purchase:00:AS=D:B007MSUAV2	Purchase:00:IA=512102570	Purchase:00:IS=512102578",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Inflitrado	Artist=Bajofondo	Tempo=120.0	Tag+=Tango:Dance	DanceRating=TNG+5	DanceRating=TGO+1	DanceRating=ATN+1	Album:00=Mar Dulce	Purchase:00:AS=D:B001C3G8MS",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Des Croissants de Soleil	Artist=Emilie-Claire Barlow	Tempo=96.0	Tag+=Bolero:Dance|International Rumba:Dance	DanceRating=BOL+5	DanceRating=RMBI+5	DanceRating=LTN+1	Album:00=Des croissants de soleil	Purchase:00:AS=D:B009CW0JFS",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Private Eyes	Artist=Brazilian Love Affair	Tempo=124.0	Tag+=American Rumba:Dance	DanceRating=RMBA+5	Album:00=Brazilian Lounge - Les Mysteres De Rio	Purchase:00:AS=D:B007UK5L52",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Glam	Artist=Dimie Cat	Tempo=200.0	Tag+=QuickStep:Dance	DanceRating=QST+6	DanceRating=FXT+1	Album:00=Glam!	Purchase:00:AS=D:B0042D1W6C",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=All For You	Artist=Imelda May	Tempo=120.0	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+6	DanceRating=FXT+1	Album:00=More Mayhem	Purchase:00:AS=D:B008VSKRAQ",
        //};

        private const string NHeader =
            @"Dance	Rating	Title	BPM	Time	Artist	Comment	DanceTags:Other	SongTags:Music";

        //private static readonly string[] MergeProps =
        //{
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Glam	Artist=Dimie Cat	Tempo=200.0	Tag+=QuickStep:Dance	DanceRating=QST+6	DanceRating=FXT+1	Album:00=Glam!	Purchase:00:AS=D:B0042D1W6C	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Length=200	Tag+=English:Other|Foxtrot:Dance|Pop:Music|Rock:Music	DanceRating=FXT+5	DanceRating=SFT+1	Tag+:FXT=David:Other|Goliath:Other|Traditional:Style",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Drop It On Me (Ft Daddy Yankee)	Artist=Ricky Martin	Tempo=100.0	Length=234	Tag+=Samba:Dance|Spanish:Other	DanceRating=SMB+3	DanceRating=LTN+1",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Bailemos Otra Vez	Artist=Jose Alberto El Canario	Tempo=195.0	Length=308	Tag+=Cha Cha:Dance|Mambo:Dance|Salsa:Dance	DanceRating=MBO+4	DanceRating=SLS+4	DanceRating=CHA+3	DanceRating=LTN+3	Tag+:MBO=Traditional:Style	Tag+:SLS=Traditional:Style",
        //};

        //private const string DHeader = @"Title	Artist	Comment";

        //private static readonly string[] DanceRows =
        //{
        //    @"The L Train	Gabriel Yared	Traditional Waltz",
        //    @"Come Wake Me Up	Rascal Flatts	Contemporary Waltz",
        //    @"A very traditional Waltz	Strauss	Old English Language",
        //};

        //private static readonly string[] DanceMergeProps =
        //{
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=The L Train	Artist=Gabriel Yared	Tempo=72.0	Tag+=Slow Waltz:Dance	DanceRating=SWZ+5	DanceRating=WLZ+1	Album:00=Shall We Dance?	Purchase:00:AS=D:B001NYTZJY	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Viennese Waltz:Dance	DanceRating=VWZ+2	DanceRating=WLZ+2",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Come Wake Me Up	Artist=Rascal Flatts	Tempo=153.0	Tag+=Viennese Waltz:Dance	DanceRating=VWZ+5	DanceRating=WLZ+1	Album:00=Changed (Deluxe Version) [+Digital Booklet]	Purchase:00:AS=D:B007MSUAV2	Purchase:00:IA=512102570	Purchase:00:IS=512102578	.Edit=	User=dwgray	Time=00/00/0000 0:00:00 PM	DanceRating=VWZ+2	DanceRating=WLZ+1",
        //    @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=A very traditional Waltz	Artist=Strauss	Tag+=English:Other	Tag+=Viennese Waltz:Dance	DanceRating=VWZ+2	DanceRating=WLZ+1",
        //};

        private const string WHeader = @"Dancers	Dance	Title+Artist";

        private const string RHeader = @"Title	Artist	Album	Track	Length	SongTags	MultiDance";

        private const string SQuuen =
            @"SongId={70b993fa-f821-44c7-bf5d-6076f4fe8f17}	User=batch	Time=3/19/2014 5:03:17 PM	Title=Crazy Little Thing Called Love	Artist=Queen	Tempo=154.0	Album:0=Greatest Hits	Album:1=The Game	Album:2=Queen - Greatest Hits	User=HunterZ	User=EthanH	User=ChaseP	DanceRating=LHP+10	DanceRating=ECS+5	DanceRating=WCS+10	User=batch	Time=5/7/2014 11:30:58 AM	Length=163	Genre=Rock	Track:1=5	Purchase:1:XS=music.F9021900-0100-11DB-89CA-0019B92A3933	User=batch	Time=5/7/2014 3:32:13 PM	Album:2=Queen: Greatest Hits	Track:2=9	Purchase:2:IS=27243763	Purchase:2:IA=27243728	User=batch	Time=5/20/2014 3:46:15 PM	Track:0=9	Purchase:0:AS=D:B00138K9CM	Purchase:0:AA=D:B00138F72E	User=JuliaS	Time=6/5/2014 8:46:10 PM	DanceRating=ECS+5	User=JuliaS	Time=6/9/2014 8:13:17 PM	DanceRating=JIV+6	User=LincolnA	Time=6/23/2014 1:56:23 PM	DanceRating=SWG+6";

        private static readonly string[] SongData =
        {
            @"SongId={70b993fa-f821-44c7-bf5d-6076f4fe8f17}	User=batch|P	Time=3/19/2014 5:03:17 PM	Title=Crazy Little Thing Called Love	Artist=Queen	Tempo=154.0	Album:0=Greatest Hits	Album:1=The Game	Album:2=Queen - Greatest Hits	User=HunterZ|P	User=EthanH|P	User=ChaseP|P	DanceRating=LHP+10	DanceRating=ECS+5	DanceRating=WCS+10	User=batch|P	Time=5/7/2014 11:30:58 AM	Length=163	Genre=Rock	Track:1=5	Purchase:1:XS=music.F9021900-0100-11DB-89CA-0019B92A3933	User=batch|P	Time=5/7/2014 3:32:13 PM	Album:2=Queen: Greatest Hits	Track:2=9	Purchase:2:IS=27243763	Purchase:2:IA=27243728	User=batch|P	Time=5/20/2014 3:46:15 PM	Track:0=9	Purchase:0:AS=D:B00138K9CM	Purchase:0:AA=D:B00138F72E	User=JuliaS|P	Time=6/5/2014 8:46:10 PM	DanceRating=ECS+5	User=JuliaS|P	Time=6/9/2014 8:13:17 PM	DanceRating=JIV+6	User=LincolnA|P	Time=6/23/2014 1:56:23 PM	DanceRating=SWG+6	User=HunterZ|P	Time=9/4/2014 8:06:37 PM	Tag=Lindy Hop	Tag=East Coast Swing	Tag=West Coast Swing	User=EthanH|P	Time=9/4/2014 8:06:37 PM	Tag=Lindy Hop	Tag=East Coast Swing	Tag=West Coast Swing	User=ChaseP|P	Time=9/4/2014 8:06:37 PM	Tag=Lindy Hop	Tag=East Coast Swing	Tag=West Coast Swing	User=JuliaS|P	Time=9/4/2014 8:06:37 PM	Tag=East Coast Swing	Tag=Jive	User=LincolnA|P	Time=9/4/2014 8:06:37 PM	Tag=Swing	User=batch|P	Time=9/4/2014 8:06:37 PM	Tag=Rock	User=HunterZ|P	Time=9/4/2014 8:11:39 PM	Tag=Lindy Hop	Tag=East Coast Swing	Tag=West Coast Swing	User=EthanH|P	Time=9/4/2014 8:11:39 PM	Tag=Lindy Hop	Tag=East Coast Swing	Tag=West Coast Swing	User=ChaseP|P	Time=9/4/2014 8:11:39 PM	Tag=Lindy Hop	Tag=East Coast Swing	Tag=West Coast Swing	User=JuliaS|P	Time=9/4/2014 8:11:39 PM	Tag=East Coast Swing	Tag=Jive	User=LincolnA|P	Time=9/4/2014 8:11:39 PM	Tag=Swing	User=batch|P	Time=9/4/2014 8:11:39 PM	Tag=Rock",
            @"SongId={ea55fcea-35f5-4d0d-81b5-a5264395945d}	Purchase:1:IA=554530	User=batch|P	Time=5/21/2014 9:15:26 PM	Length=155	Purchase:1:AS=D:B000W0CTAW	Purchase:1:AA=D:B000W0B00W	Purchase:1:IS=554314	Genre=Jazz	Length=156	Time=5/21/2014 7:16:49 PM	User=batch|P	PromoteAlbum:1=	Purchase:1:XS=music.B76B0F00-0100-11DB-89CA-0019B92A3933	Track:1=5	Album:1=Sings Great American Songwriters	Genre=Pop	Length=155	Time=5/21/2014 2:04:51 PM	User=batch|P	DanceRating=FXT+5	Album:0=The Ultimate Ballroom Album 12	Tempo=116.0	Artist=Carmen McRae	Title=Blue Moon	Time=3/17/2014 5:43:50 PM	User=HunterZ|P",
            @"SongId={7471bb23-cf92-468f-9fb6-e6031571f29a}	User=dwgray	Time=3/17/2014 5:45:32 PM	Title=Blue Moon	Artist=Carmen Mccrae	Tempo=112.0	DanceRating=ECS+5	User=batch|P	Time=7/11/2014 9:49:28 PM	Artist=Carmen McRae	Length=158	Genre=Jazz	Album:00=Greatest Hits	Track:00=19	Purchase:00:XS=music.18D74B06-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=	User=batch|P	Time=7/11/2014 9:50:07 PM	Length=156	Album:01=Sings Great American Songwriters	Track:01=5	Purchase:01:IS=554314	Purchase:01:IA=554530	PromoteAlbum:01=	User=batch|P	Time=7/11/2014 9:50:35 PM	Length=155	Purchase:01:AS=D:B000W0CTAW	Purchase:01:AA=D:B000W0B00W",
            @"SongId={0b1e4225-d782-41d1-9f16-b105e7bd0efa}	User=dwgray	Time=6/10/2014 3:11:03 PM	Title=Lady Marmalade	Artist=Christina Aguilera	DanceRating=CHA+5	User=batch|P	Time=6/10/2014 3:26:26 PM	Length=264	Genre=Pop	Album:00=Moulin Rouge	Track:00=2	Purchase:00:XS=music.9F480F00-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=",
            @"SongId={52cb6e8c-6f0f-469e-ac83-d353cbab6c96}	User=batch|P	Time=6/9/2014 8:54:43 PM	Title=Lady Marmalade	Artist=Christina Aguilera, Mya, Pink,  & Lil Kim	DanceRating=HST+5	User=batch|P	Time=7/4/2014 9:54:35 PM	Artist=Christina Aguilera	Length=264	Genre=Pop	Album:00=Moulin Rouge	Track:00=2	Purchase:00:XS=music.9F480F00-0100-11DB-89CA-0019B92A3933	PromoteAlbum:00=	User=batch|P	Time=7/4/2014 9:55:04 PM	Length=265	Album:00=Moulin Rouge (Soundtrack from the Motion Picture)	Purchase:00:IS=3577756	Purchase:00:IA=3579609	User=batch|P	Time=7/4/2014 9:55:49 PM	Album:01=Music From Nicole Kidman Movies	Track:01=3	Purchase:01:AS=D:B004XOHIH2	Purchase:01:AA=D:B004XOHHXM	PromoteAlbum:01="
        };

        private static readonly string[] SongRows =
        {
            @"Black Sheep	Gin Wigmore	120	WCS	Gravel & Wine [+digital booklet]	B00BYKXC82	",
            @"The L Train	Gabriel Yared	72	SWZ	Shall We Dance?	B001NYTZJY	",
            @"Come Wake Me Up	Rascal Flatts	153	VWZ	Changed (Deluxe Version) [+Digital Booklet]	B007MSUAV2	512102578|512102570",
            @"Inflitrado	Bajofondo	120	TNG	Mar Dulce	B001C3G8MS	",
            @"Des Croissants de Soleil	Emilie-Claire Barlow	96	BOL,RMBI	Des croissants de soleil	B009CW0JFS	",
            @"Private Eyes	Brazilian Love Affair	124	RMBA	Brazilian Lounge - Les Mysteres De Rio	B007UK5L52	",
            @"Glam	Dimie Cat	200	QST	Glam!	B0042D1W6C	",
            @"All For You	Imelda May	120	SFT	More Mayhem	B008VSKRAQ	"
        };

        private static readonly string[] RowProps =
        {
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Black Sheep	Artist=Gin Wigmore	Tempo=120.0	Tag+=West Coast Swing:Dance	DanceRating=WCS+5	Album:00=Gravel & Wine [+digital booklet]	Purchase:00:AS=D:B00BYKXC82	DanceRating=SWG+1	DanceRating=CSG+1	DanceRating=HST+1	DanceRating=WCS+1	DanceRating=LHP+1",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=The L Train	Artist=Gabriel Yared	Tempo=72.0	Tag+=Slow Waltz:Dance	DanceRating=SWZ+5	Album:00=Shall We Dance?	Purchase:00:AS=D:B001NYTZJY	DanceRating=WLZ+1",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Come Wake Me Up	Artist=Rascal Flatts	Tempo=153.0	Tag+=Viennese Waltz:Dance	DanceRating=VWZ+5	Album:00=Changed (Deluxe Version) [+Digital Booklet]	Purchase:00:AS=D:B007MSUAV2	Purchase:00:IA=512102570	Purchase:00:IS=512102578	DanceRating=WLZ+1",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Inflitrado	Artist=Bajofondo	Tempo=120.0	Tag+=Tango:Dance	DanceRating=TNG+5	Album:00=Mar Dulce	Purchase:00:AS=D:B001C3G8MS	DanceRating=TGO+1	DanceRating=ATN+1",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Des Croissants de Soleil	Artist=Emilie-Claire Barlow	Tempo=96.0	Tag+=Bolero:Dance|International Rumba:Dance	DanceRating=BOL+5	DanceRating=RMBI+5	Album:00=Des croissants de soleil	Purchase:00:AS=D:B009CW0JFS	DanceRating=LTN+1",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Private Eyes	Artist=Brazilian Love Affair	Tempo=124.0	Tag+=American Rumba:Dance	DanceRating=RMBA+5	Album:00=Brazilian Lounge - Les Mysteres De Rio	Purchase:00:AS=D:B007UK5L52",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Glam	Artist=Dimie Cat	Tempo=200.0	Tag+=QuickStep:Dance	DanceRating=QST+5	Album:00=Glam!	Purchase:00:AS=D:B0042D1W6C	DanceRating=FXT+1	DanceRating=QST+1",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=All For You	Artist=Imelda May	Tempo=120.0	Tag+=Slow Foxtrot:Dance	DanceRating=SFT+5	Album:00=More Mayhem	Purchase:00:AS=D:B008VSKRAQ	DanceRating=FXT+1	DanceRating=SFT+1"
        };

        private static readonly string[] TaggedRows =
        {
            @"Foxtrot	5	Glam	120	3:20	Dimie Cat	traditional english language foxtrot	David|Goliath	Pop|Rock",
            @"Samba	3	Drop It On Me (Ft Daddy Yankee)	100	3:54	Ricky Martin	good pop-latin spanish language samba		",
            @"Mambo,Salsa	4	Bailemos Otra Vez	195		Jose Alberto El Canario	Old sounding overall mambo/salsa with a clear rhythm		",
            @"Cha Cha	3	Bailemos Otra Vez	195	5:08	Jose Alberto El Canario			"
        };

        private static readonly string[] TaggedRowsProps =
        {
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=English:Other|Foxtrot:Dance|Pop:Music|Rock:Music	DanceRating=FXT+5	Title=Glam	Tempo=120.0	Length=200	Artist=Dimie Cat	Tag+:FXT=David:Other|Goliath:Other|Traditional:Style	DanceRating=SFT+1",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Samba:Dance|Spanish:Other	DanceRating=SMB+3	Title=Drop It On Me (Ft Daddy Yankee)	Tempo=100.0	Length=234	Artist=Ricky Martin	DanceRating=LTN+1",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Cha Cha:Dance|Mambo:Dance|Salsa:Dance	DanceRating=MBO+4	DanceRating=SLS+4	Title=Bailemos Otra Vez	Tempo=195.0	Artist=Jose Alberto El Canario	Tag+:MBO=Traditional:Style	Tag+:SLS=Traditional:Style	Length=308	DanceRating=CHA+3	DanceRating=LTN+3"
        };

        private static readonly string[] StarsRows =
        {
            @"Antonio & Cheryl	Cha-cha-cha	""Tonight (I'm Lovin' You)""—Enrique Iglesias feat. Ludacris & DJ Frank E",
            @"Lea & Artem	Foxtrot	""This Will Be (An Everlasting Love)""—Natalie Cole"
        };

        private static readonly string[] StarsRowsProps =
        {
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Cha Cha:Dance	DanceRating=CHA+5	Tag+:CHA=Antonio:Other|Cheryl:Other	Title=Tonight (I'm Lovin' You)	Artist=Enrique Iglesias feat. Ludacris & DJ Frank E	DanceRating=LTN+1	Tag+=Episode 1:Other|Season 19:Other",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Tag+=Foxtrot:Dance	DanceRating=FXT+5	Tag+:FXT=Artem:Other|Lea:Other	Title=This Will Be (An Everlasting Love)	Artist=Natalie Cole	Tag+=Episode 1:Other|Season 19:Other"
        };

        private static readonly string[] RhettRows =
        {
            @"(I've Had) The Time of My Life	Bill Medley & Jennifer Warnes	Dirty Dancing Soundtrack	1	290377		MBO||SLS||MRG||HST",
            @"A Namorada		Phoenix 2001 Cha#1	18	286511	Male Vocal:Other	CHA|International:Style",
            @"Ain't That A Kick In The Head	Dean Martin	Ultra-Lounge 'Wild, Cool & Swingin' Vol. 1	1	145355		SFT|International:Style||MBO||CHA"
        };

        private static readonly string[] RhettRowsProps =
        {
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=(I've Had) The Time of My Life	Artist=Bill Medley & Jennifer Warnes	Album:00=Dirty Dancing Soundtrack	Track:00=1	Length=290	Tag+=Mambo:Dance|Salsa:Dance|Merengue:Dance|Hustle:Dance	DanceRating=MBO+1	DanceRating=SLS+1	DanceRating=MRG+1	DanceRating=HST+1	DanceRating=SWG+1	DanceRating=LTN+3",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=A Namorada	Album:00=Phoenix 2001 Cha#1	Track:00=18	Length=287	Tag+=Cha Cha:Dance|Male Vocal:Other	DanceRating=CHA+1	Tag+:CHA=International:Style	DanceRating=LTN+1",
            @".Create=	User=dwgray	Time=00/00/0000 0:00:00 PM	Title=Ain't That A Kick In The Head	Artist=Dean Martin	Album:00=Ultra-Lounge 'Wild, Cool & Swingin' Vol. 1	Track:00=1	Length=145	Tag+=Slow Foxtrot:Dance|Mambo:Dance|Cha Cha:Dance	DanceRating=SFT+1	DanceRating=MBO+1	DanceRating=CHA+1	Tag+:SFT=International:Style	DanceRating=LTN+2	DanceRating=FXT+1"
        };

        [TestMethod]
        public void TitleArtistMatch()
        {
            var sd1 = new Song { Title = "A Song (With a subtitle)", Artist = "Crazy Artist" };
            var sd2 = new Song { Title = "Moliendo Café", Artist = "The Who" };
            var sd3 = new Song { Title = "If the song or not", Artist = "Señor Bolero" };

            Assert.IsTrue(
                sd1.TitleArtistMatch("A Song (With a subtitle)", "Crazy Artist"),
                "SD1: Exact");
            Assert.IsTrue(sd1.TitleArtistMatch("Song", "Crazy Artist"), "SD1: Weak");
            Assert.IsFalse(sd1.TitleArtistMatch("Song", "Crazy Artiste"), "SD1: No Match");

            Assert.IsTrue(sd2.TitleArtistMatch("Moliendo Café", "The Who"), "SD2: Exact");
            Assert.IsTrue(sd2.TitleArtistMatch("Moliendo Cafe", "Who"), "SD2: Weak");
            Assert.IsFalse(sd2.TitleArtistMatch("Molienda Café", "The Who"), "SD2: No Match");

            Assert.IsTrue(sd3.TitleArtistMatch("If the song or not", "Señor Bolero"), "SD3: Exact");
            Assert.IsTrue(sd3.TitleArtistMatch("If  Song and  NOT", "Senor Bolero "), "SD3: Weak");
            Assert.IsFalse(
                sd3.TitleArtistMatch("If the song with not", "Señor Bolero"),
                "SD3: No Match");
        }

        //TODO: Get create/Modified working correctly in reload case and make sure that doesn't re-break the loadcatalog case.
        [TestMethod]
        public async Task LoadingDetails()
        {
            var songs = await Load();
            Assert.AreEqual(SongData.Length, songs.Count);
        }

        [TestMethod]
        public async Task SavingDetails()
        {
            var songs = await Load();
            Assert.AreEqual(songs.Count, SongData.Length);
            for (var i = 0; i < songs.Count; i++)
            {
                var s = songs[i];
                DiffSerialized(s.ToString(), SongData[i], s.SongId);
            }
        }

        private static void DiffSerialized(string org, string str, Guid id)
        {
            str = str.Trim();
            org = org.Trim();
            Trace.WriteLine(org);
            Trace.WriteLine(str);

            if (!string.Equals(org, str))
            {
                if (org.Length == str.Length)
                {
                    for (var ich = 0; ich < str.Length; ich++)
                    {
                        if (org[ich] == str[ich])
                        {
                            continue;
                        }

                        Trace.WriteLine($"Results differ starting at {ich}");
                        break;
                    }
                }
                else if (org.Length < str.Length)
                {
                    Trace.WriteLine("Org shorter than result");
                }
                else
                {
                    Trace.WriteLine("Result shorter than org");
                }
            }

            Assert.AreEqual(org, str, $"{id.ToString("B")} failed to save.");
        }

        [TestMethod]
        public async Task LoadingRowDetails()
        {
            await ValidateLoadingRowDetails(SHeader, SongRows, RowProps);
        }

        [TestMethod]
        public async Task LoadingTaggedRowDetails()
        {
            await ValidateLoadingRowDetails(NHeader, TaggedRows, TaggedRowsProps, 1);
        }

        [TestMethod]
        public async Task LoadingDwtsRowDetails()
        {
            await ValidateLoadingRowDetails(
                WHeader, StarsRows, StarsRowsProps, 0,
                "Season 19:Other|Episode 1:Other");
        }

        [TestMethod]
        public async Task LoadingRhettRowDetails()
        {
            await ValidateLoadingRowDetails(RHeader, RhettRows, RhettRowsProps);
        }


        //[TestMethod]
        //public void CreatingSongs()
        //{
        //    var service = MockContext.CreateService(true);
        //    var guids = CreateSongs(SHeader,SongRows,service).ToList();

        //    Assert.AreEqual(guids.Count, service.Songs.Count());
        //    for (var i = 0; i < guids.Count; i++)
        //    {
        //        var s = service.Songs.Find(guids[i]);
        //        Assert.IsNotNull(s);

        //        var txt = DanceMusicTester.ReplaceTime(s.Serialize(new[] { Song.NoSongId }));
        //        Trace.WriteLine(txt);
        //        Assert.AreEqual(RowPopsCreate[i], txt);
        //    }
        //}

        //[TestMethod]
        //public void LoadingCatalog()
        //{
        //    var service = MockContext.CreateService(true);
        //    var guids = CreateSongs(SHeader, SongRows, service).ToList();

        //    Assert.AreEqual(guids.Count, service.Songs.Count());

        //    var songs = LoadRows(NHeader, TaggedRows, service, 1);

        //    var merges = service.MatchSongs(songs, DanceMusicService.MatchMethod.Merge);

        //    Assert.IsTrue(service.MergeCatalog(service.FindUser("dwgray"),merges));

        //    var i = 0;
        //    foreach (var song in merges.Select(merge => service.Songs.Find(merge.Right?.SongId ?? merge.Left.SongId)))
        //    {
        //        Assert.IsNotNull(song);
        //        var txt = DanceMusicTester.ReplaceTime(song.Serialize(new[] { Song.NoSongId }));
        //        Trace.WriteLine(txt);
        //        Assert.AreEqual(MergeProps[i++], txt);
        //    }
        //}

        //[TestMethod]
        //public void LoadingDanceCatalog()
        //{
        //    var service = MockContext.CreateService(true);
        //    var guids = CreateSongs(SHeader, SongRows, service).ToList();

        //    Assert.AreEqual(guids.Count, service.Songs.Count());

        //    var songs = LoadRows(DHeader, DanceRows, service);

        //    var merges = service.MatchSongs(songs, DanceMusicService.MatchMethod.Merge);

        //    Assert.IsTrue(service.MergeCatalog(service.FindUser("dwgray"), merges, new[] {"VWZ"}));

        //    var i = 0;
        //    foreach (var song in merges.Select(merge => service.Songs.Find(merge.Right?.SongId ?? merge.Left.SongId)))
        //    {
        //        Assert.IsNotNull(song);
        //        var txt = DanceMusicTester.ReplaceTime(song.Serialize(new[] { Song.NoSongId }));
        //        var ex = DanceMergeProps[i];
        //        Trace.WriteLine(ex);
        //        Trace.WriteLine(txt);
        //        if (!string.Equals(ex, txt))
        //        {
        //            if (ex.Length != txt.Length)
        //            {
        //                Assert.Fail($"Failed on Line {i}, lengths differ");
        //            }
        //            for (var j =  0; j < ex.Length; j++)
        //            {
        //                if (ex[j] != txt[j])
        //                {
        //                    Assert.Fail($"Failed on Line {i}, character {j}");
        //                }
        //            }
        //        }
        //        i += 1;
        //    }
        //}
        [TestMethod]
        public async Task PropertyByUser()
        {
            var song = await Song.Create(SQuuen, await GetService());

            var map = song.MapProperyByUsers(Song.DanceRatingField);

            //foreach (var kv in map)
            //{
            //    Trace.WriteLine(string.Format("{0}:{1}",kv.Key,string.Join(",",kv.Value)));
            //}

            //HunterZ:LHP+10,ECS+5,WCS+10
            //EthanH:LHP+10,ECS+5,WCS+10
            //ChaseP:LHP+10,ECS+5,WCS+10
            //JuliaS:ECS+5,JIV+6
            //LincolnA:SWG+6
            Assert.IsTrue(map["HunterZ"].Count == 3);
            Assert.IsTrue(map["EthanH"].Count == 3);
            Assert.IsTrue(map["ChaseP"].Count == 3);
            Assert.IsTrue(map["JuliaS"].Count == 2);
            Assert.IsTrue(map["LincolnA"].Count == 1);
            Assert.IsFalse(map.ContainsKey("dwgray"));
        }

        private static async Task ValidateLoadingRowDetails(string header, string[] rows,
            string[] expected, int dups = 0, string tags = null)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            var songs = await LoadRows(header, rows, await GetService(), dups);

            for (var i = 0; i < expected.Length; i++)
            {
                var song = songs[i];
                if (tags != null)
                {
                    song.AddTags(tags, "dwgray", await GetStats(), song);
                }

                var r = DanceMusicTester.ReplaceTime(song.Serialize(new[] { Song.NoSongId }));
                Trace.WriteLine(r);
                Assert.AreEqual(expected[i], r);
            }
        }

        //private static IEnumerable<Guid> CreateSongs(string header, string[] rows, DanceMusicService service)
        //{
        //    var ids = new List<Guid>();

        //    var songs = LoadRows(header, rows, service);

        //    foreach (var sd in songs)
        //    {
        //        var s = new Song { SongId = Guid.NewGuid() };
        //        s.Create(sd, null, "dwgray", Song.CreateCommand, null, service.DanceStats);
        //        ids.Add(s.SongId);
        //    }

        //    service.SaveChanges();

        //    return ids;
        //}

        private async Task<IList<Song>> Load()
        {
            var service = await GetService();
            var tasks = SongData.Select(str => Song.Create(str, service)).ToList();
            return await Task.WhenAll(tasks);
        }

        private static async Task<IList<Song>> LoadRows(string header,
            IReadOnlyCollection<string> rows,
            DanceMusicCoreService dms, int dups = 0)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            IList<string> headers = Song.BuildHeaderMap(header);
            var ret = await Song.CreateFromRows(
                new ApplicationUser("dwgray", "me@hotmail.com"), "\t", headers, rows, dms, 5);

            Assert.AreEqual(rows.Count, ret.Count + dups);
            return ret;
        }

        private static async Task<DanceMusicCoreService> GetService()
            => await DanceMusicTester.CreateServiceWithUsers("Song");

        private static async Task<DanceStatsInstance> GetStats() => (await GetService()).DanceStats;
    }
}

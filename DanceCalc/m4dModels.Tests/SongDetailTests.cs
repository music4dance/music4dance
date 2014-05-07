using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
//using System.Diagnostics;
using m4dModels;

namespace m4dModels.Tests
{
    [TestClass]
    public class SongDetailTests
    {
        [TestMethod]
        public void TitleArtistMatch()
        {
            SongDetails sd1 = new SongDetails { Title = "A Song (With a subtitle)", Artist = "Crazy Artist" };
            SongDetails sd2 = new SongDetails { Title = "Moliendo Café", Artist = "The Who" };
            SongDetails sd3 = new SongDetails { Title = "If the song or not", Artist = "Señor Bolero" };

            Assert.IsTrue(sd1.TitleArtistMatch("A Song (With a subtitle)","Crazy Artist"),"SD1: Exact");
            Assert.IsTrue(sd1.TitleArtistMatch("Song", "Crazy Artist"), "SD1: Weak");
            Assert.IsFalse(sd1.TitleArtistMatch("Song", "Crazy Artiste"), "SD1: No Match");

            Assert.IsTrue(sd2.TitleArtistMatch("Moliendo Café", "The Who"), "SD2: Exact");
            Assert.IsTrue(sd2.TitleArtistMatch("Moliendo Cafe", "Who"), "SD2: Weak");
            Assert.IsFalse(sd2.TitleArtistMatch("Molienda Café", "The Who"), "SD2: No Match");

            Assert.IsTrue(sd3.TitleArtistMatch("If the song or not", "Señor Bolero"), "SD3: Exact");
            Assert.IsTrue(sd3.TitleArtistMatch("If  Song and  NOT", "Senor Bolero "), "SD3: Weak");
            Assert.IsFalse(sd3.TitleArtistMatch("If the song with not", "Señor Bolero"), "SD3: No Match");
        }
    };
}

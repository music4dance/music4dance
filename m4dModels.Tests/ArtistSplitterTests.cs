using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace m4dModels.Tests;

[TestClass]
public class ArtistSplitterTests
{
    private static ArtistKnowledge Knowledge(string known)
    {
        var knowledge = new ArtistKnowledge();
        foreach (var name in (known ?? string.Empty).Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            knowledge.Add(name);
        }
        return knowledge;
    }

    private static void AssertSplit(string artist, string title, string known, string expected)
    {
        var result = ArtistSplitter.Split(artist, title, Knowledge(known));
        Assert.AreEqual(expected, string.Join("|", result.Artists),
            $"rules={string.Join(",", result.Rules)} unresolved={string.Join(",", result.Unresolved)}");
    }

    [TestMethod]
    [DataRow(null, "")]
    [DataRow("", "")]
    [DataRow("   ", "")]
    [DataRow("Norah Jones", "Norah Jones")]
    [DataRow("  Norah   Jones ", "Norah Jones")]
    [DataRow("AC/DC", "AC/DC")]
    [DataRow("10,000 Maniacs", "10,000 Maniacs")]
    [DataRow("Sammy Davis, Jr.", "Sammy Davis, Jr.")]
    [DataRow("Little Feat", "Little Feat")]
    [DataRow("A|B", "A/B")]
    public void SingleArtists(string artist, string expected)
    {
        AssertSplit(artist, null, null, expected);
    }

    [TestMethod]
    [DataRow("Pitbull feat. Ne-Yo", "Pitbull|Ne-Yo")]
    [DataRow("Rosabel Featuring Jeanie Tracy", "Rosabel|Jeanie Tracy")]
    [DataRow("Latin Soul Orchestra ft. Francisco Rojos", "Latin Soul Orchestra|Francisco Rojos")]
    [DataRow("Lil' Kim (Featuring Lil' Cease)", "Lil' Kim|Lil' Cease")]
    [DataRow("Delano Weltevreden (feat. Carin Verhoeven)", "Delano Weltevreden|Carin Verhoeven")]
    [DataRow("B.o.B. w/ Bruno Mars", "B.o.B.|Bruno Mars")]
    [DataRow("Duke Ellington and His Cotton Club Orchestra feat. Teddy Bunn", "Duke Ellington|Teddy Bunn")]
    [DataRow("Featuring Hilary Alexander Jonathan Stout", "Featuring Hilary Alexander Jonathan Stout")]
    public void ArtistFeaturing(string artist, string expected)
    {
        AssertSplit(artist, null, null, expected);
    }

    [TestMethod]
    [DataRow("Lindsey Stirling", "Hold My Heart (feat. ZZ Ward)", "Lindsey Stirling|ZZ Ward")]
    [DataRow("Chick Webb", "Don't Be That Way [Featuring Ella Fitzgearld]", "Chick Webb|Ella Fitzgearld")]
    [DataRow("Katy Perry", "California Gurls - feat. Snoop Dogg", "Katy Perry|Snoop Dogg")]
    [DataRow("Parov Stelar", "Charleston Butterfly feat. Gabriella Hänninen", "Parov Stelar|Gabriella Hänninen")]
    [DataRow("LOVRA", "Tension (feat. Mila Falls) - Radio Edit", "LOVRA|Mila Falls")]
    [DataRow("Joe Arroyo", "Esta Noche Es Nuestra (feat. Joe Arroyo)", "Joe Arroyo")]
    [DataRow("Sunscreen", "Valerie (Extended Floorfiller Mark Ronson Featuring Amy Winehouse Interpretation)", "Sunscreen")]
    [DataRow("Kid Cudi, GLC, Chip tha Ripper & Nicole Wray", "The End (feat. GLC, Chip tha Ripper & Nicole Wray)", "Kid Cudi, GLC, Chip tha Ripper & Nicole Wray|GLC|Chip tha Ripper|Nicole Wray")]
    public void TitleFeaturing(string artist, string title, string expected)
    {
        AssertSplit(artist, title, null, expected);
    }

    [TestMethod]
    [DataRow("Carly Rae Jepsen", "Good Time (Feat. Owl City and Carly Rae Jepsen)", "Owl City", "Carly Rae Jepsen|Owl City")]
    [DataRow("Blake Shelton", "Boys 'Round Here (feat. Pistol Annies & Friends)", "Pistol Annies", "Blake Shelton|Pistol Annies & Friends")]
    [DataRow("Charlie Christian", "Honeysuckle Rose (feat. Benny Goodman and His Orchestra)", "", "Charlie Christian|Benny Goodman")]
    [DataRow("Yandel", "Báilame (feat. Shaggy & Alex Sensation)", "Shaggy", "Yandel|Shaggy|Alex Sensation")]
    public void TitleFeaturingLists(string artist, string title, string known, string expected)
    {
        AssertSplit(artist, title, known, expected);
    }

    [TestMethod]
    [DataRow("Dolly Parton & Kenny Rogers", "Dolly Parton|Kenny Rogers", "Dolly Parton|Kenny Rogers")]
    [DataRow("Dolly Parton & Kenny Rogers", "Dolly Parton", "Dolly Parton|Kenny Rogers")]
    [DataRow("Dolly Parton & Kenny Rogers", "Kenny Rogers", "Dolly Parton|Kenny Rogers")]
    [DataRow("Dolly Parton & Kenny Rogers", "", "Dolly Parton & Kenny Rogers")]
    [DataRow("Lisa Loeb & Nine Stories", "Lisa Loeb", "Lisa Loeb|Nine Stories")]
    [DataRow("Bob Marley & The Wailers", "Bob Marley", "Bob Marley|The Wailers")]
    [DataRow("Bob Marley & The Wailers", "The Wailers", "Bob Marley & The Wailers")]
    [DataRow("Angus & Julia Stone", "Julia Stone", "Angus & Julia Stone")]
    [DataRow("Whitney Houston with Jermaine Jackson", "Whitney Houston|Jermaine Jackson", "Whitney Houston|Jermaine Jackson")]
    [DataRow("Chris Brown X Tyga", "Chris Brown|Tyga", "Chris Brown|Tyga")]
    [DataRow("Bill Evans And Paul Motian And Scott LaFaro", "Bill Evans|Paul Motian|Scott LaFaro", "Bill Evans|Paul Motian|Scott LaFaro")]
    [DataRow("Queen + Paul Rodgers", "Queen|Paul Rodgers", "Queen|Paul Rodgers")]
    [DataRow("Lil Nas X & Billy Ray Cyrus", "Billy Ray Cyrus", "Lil Nas X|Billy Ray Cyrus")]
    public void EvidenceSplits(string artist, string known, string expected)
    {
        AssertSplit(artist, null, known, expected);
    }

    [TestMethod]
    [DataRow("Simon & Garfunkel", "Simon|Garfunkel")]
    [DataRow("Earth, Wind & Fire", "Earth|Wind|Fire")]
    [DataRow("Crosby, Stills, Nash  & Young", "Crosby|Stills|Nash|Young")]
    [DataRow("Peter, Paul and Mary", "Peter|Paul|Mary")]
    [DataRow("Mumford & Sons", "Mumford")]
    [DataRow("Kool & The Gang", "Kool|The Gang")]
    [DataRow("The Mamas & The Papas", "The Mamas|The Papas")]
    [DataRow("Fitz and The Tantrums", "Fitz")]
    [DataRow("Benny y Erik Sasha", "Benny")]
    [DataRow("BnB (Blanco & Black)", "Blanco|Black")]
    [DataRow("Dimitri Vegas & Like Mike", "Dimitri Vegas")]
    [DataRow("Huey Lewis & The News", "The News")]
    [DataRow("Musica & Poesia Orchestra", "")]
    [DataRow("Orquesta Tabaco Y Ron", "")]
    [DataRow("Ike & Tina Turner", "Ike|Tina Turner")]
    [DataRow("Tyler, the Creator", "Tyler")]
    [DataRow("Harry Connick, Jr.", "Harry Connick")]
    public void ProtectedActs(string artist, string known)
    {
        AssertSplit(artist, null, known, ArtistSplitter.CleanName(artist));
    }

    [TestMethod]
    [DataRow("Rodrigo y Gabriela", "Rodrigo|Gabriela")]
    [DataRow("Sonny & Cher", "Sonny|Cher")]
    [DataRow("Bebe Neuwirth & Girls", "Bebe Neuwirth|Girls")]
    public void DuosAndGroupsDespiteEvidence(string artist, string known)
    {
        AssertSplit(artist, null, known, artist);
    }

    [TestMethod]
    [DataRow("RBD", "Aún Hay Algo (Feat Christopher von Uckermann & Alfonso Herrera)", "RBD|Christopher von Uckermann|Alfonso Herrera")]
    [DataRow("Lou Bega feat. KLAZZ Brothers & Cuba Percussion", null, "Lou Bega|KLAZZ Brothers|Cuba Percussion")]
    [DataRow("Falcon Punch", "Higher feat. Wild & Free", "Falcon Punch|Wild & Free")]
    public void FeaturedListsOfFullNames(string artist, string title, string expected)
    {
        AssertSplit(artist, title, null, expected);
    }

    [TestMethod]
    [DataRow("Duke Ellington and His Orchestra", "Duke Ellington")]
    [DataRow("Tony Evans & His Orchestra", "Tony Evans")]
    [DataRow("Louis Armstrong & All His Stars", "Louis Armstrong")]
    [DataRow("Naomi & Her Handsome Devils", "Naomi")]
    [DataRow("David Calzado y su Charanga Habanera", "David Calzado")]
    [DataRow("Fruko Y Sus Tesos", "Fruko")]
    [DataRow("Hugo Strasser Und Sein Tanzorchester", "Hugo Strasser")]
    [DataRow("Kay Starr with Orchestra", "Kay Starr")]
    [DataRow("Ballroom Orchestra and Singers", "Ballroom Orchestra")]
    [DataRow("Andy Ross with His Orchestra & Singers", "Andy Ross")]
    [DataRow("His Band Ross Mitchell & Singers", "Ross Mitchell")]
    [DataRow("Teddy Wilson And His Orchestra With Billie Holliday", "Teddy Wilson|Billie Holliday")]
    [DataRow("Benny Goodman & His Orchestra; Vocal By Helen Forrest", "Benny Goodman|Helen Forrest")]
    [DataRow("Alan Campbell, Judy Kuhn, George Hearn & Orchestra", "Alan Campbell|Judy Kuhn|George Hearn")]
    public void LeaderOfBackingGroup(string artist, string expected)
    {
        AssertSplit(artist, null, null, expected);
    }

    [TestMethod]
    [DataRow("London Symphony Orchestra and Árpád Joó", "London Symphony Orchestra|Árpád Joó")]
    [DataRow("Vienna State Opera Orchestra & Felix Prohaska", "Vienna State Opera Orchestra|Felix Prohaska")]
    [DataRow("Glenn Miller Orchestra & Ray Eberle", "Glenn Miller Orchestra|Ray Eberle")]
    [DataRow("Herbert von Karajan and Philharmonia Orchestra", "Herbert von Karajan|Philharmonia Orchestra")]
    [DataRow("Peggy Lee With The Benny Goodman Orchestra", "Peggy Lee|The Benny Goodman Orchestra")]
    [DataRow("The Dave Brubeck Trio & Gerry Mulligan", "The Dave Brubeck Trio|Gerry Mulligan")]
    [DataRow("Bach Collegium Japan and Masaaki Suzuki", "Bach Collegium Japan|Masaaki Suzuki")]
    public void NamedEnsembles(string artist, string expected)
    {
        AssertSplit(artist, null, null, expected);
    }

    [TestMethod]
    public void VariousArtistsIsLeftAlone()
    {
        AssertSplit("Various Artists", null, null, "Various Artists");
        AssertSplit("Various Artists, Budapest Strings, Béla Bánfalvi", null, null,
            "Various Artists|Budapest Strings|Béla Bánfalvi");
    }

    [TestMethod]
    public void MaskedActInsideLongerCredit()
    {
        AssertSplit("Earth, Wind & Fire with The Emotions", null, "Earth, Wind & Fire|The Emotions",
            "Earth, Wind & Fire|The Emotions");
    }

    [TestMethod]
    [DataRow("London Philharmonic Orchestra, London Philharmonic Choir, The London Chorus and David Parry", "", "London Philharmonic Orchestra|London Philharmonic Choir|The London Chorus|David Parry")]
    [DataRow("Madonna, Antonio Banderas, Jonathon Pryce, Jimmy Nail", "Madonna", "Madonna|Antonio Banderas|Jonathon Pryce|Jimmy Nail")]
    [DataRow("Bette Midler, Sarah Jessica Parker & Kathy Najimy", "", "Bette Midler|Sarah Jessica Parker|Kathy Najimy")]
    [DataRow("Kenneth Gilbert, Lars Ulrik Mortensen, Nicholas Kraemer", "", "Kenneth Gilbert|Lars Ulrik Mortensen|Nicholas Kraemer")]
    public void CommaLists(string artist, string known, string expected)
    {
        AssertSplit(artist, null, known, expected);
    }

    [TestMethod]
    [DataRow("Itzhak Perlman; James Levine: Vienna Philharmonic Orchestra", "Itzhak Perlman|James Levine|Vienna Philharmonic Orchestra")]
    [DataRow("Christa Ludwig; alto; Herbert von Karajan: Berlin Philharmonic Orchestra", "Christa Ludwig|Herbert von Karajan|Berlin Philharmonic Orchestra")]
    [DataRow("Dave Brubeck;Gerry Mulligan", "Dave Brubeck|Gerry Mulligan")]
    [DataRow("Yusuf / Cat Stevens", "Yusuf|Cat Stevens")]
    [DataRow("Tommy Dorsey / Jimmy Dorsey", "Tommy Dorsey|Jimmy Dorsey")]
    public void StrongSeparators(string artist, string expected)
    {
        AssertSplit(artist, null, null, expected);
    }

    [TestMethod]
    public void IsSplit()
    {
        Assert.IsFalse(ArtistSplitter.Split("Norah Jones").IsSplit("Norah Jones"));
        Assert.IsFalse(ArtistSplitter.Split(" Norah  Jones").IsSplit(" Norah  Jones"));
        Assert.IsFalse(ArtistSplitter.Split(null).IsSplit(null));
        Assert.IsTrue(ArtistSplitter.Split("Pitbull feat. Ne-Yo").IsSplit("Pitbull feat. Ne-Yo"));
        Assert.IsTrue(ArtistSplitter.Split("Mystikal", "Shake Ya Ass (feat. Pharrell Williams)").IsSplit("Mystikal"));
    }

    [TestMethod]
    public void SerializeRoundTrips()
    {
        var value = ArtistSplitter.Serialize(["Dolly Parton", " Kenny  Rogers ", "", "A|B"]);
        Assert.AreEqual("Dolly Parton|Kenny Rogers|A/B", value);
        CollectionAssert.AreEqual(new[] { "Dolly Parton", "Kenny Rogers", "A/B" },
            ArtistSplitter.Deserialize(value).ToArray());
        Assert.AreEqual(0, ArtistSplitter.Deserialize("").Count);
        Assert.AreEqual(0, ArtistSplitter.Deserialize(null).Count);
    }
}

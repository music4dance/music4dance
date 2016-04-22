using System.Collections.Generic;
using System.Linq;
using DanceLibrary;

namespace m4d.ViewModels
{
    public class SiteMapEntry
    {
        public virtual string Title { get; set; }
        public virtual string Reference { get; set; }
        public virtual string Description { get; set; }
        public virtual bool OneTime { get; set; }
        public virtual IEnumerable<SiteMapEntry> Children { get; set; }

        public string FullPath => MakeFullPath(Reference);

        protected string MakeFullPath(string rel)
        {
            return $"https://www.music4dance.net/{rel}";
        }
    }

    public class SiteMapDance : SiteMapEntry
    {
        public SiteMapDance(DanceObject dance)
        {
            _dance = dance;
        }

        public override string Title => _dance.Name;
        public override string Reference => $"dances/{_dance.CleanName}";

        public string CatalogReference => $"song/search?dances={_dance.Id}";

        public string CatalogFullPath => MakeFullPath(CatalogReference);

        private readonly DanceObject _dance;
    }

    public class SiteMapCategory
    {
        public virtual string Name { get; set; }
        public virtual string Type { get; set; }
        public virtual IEnumerable<SiteMapEntry> Entries { get; set; }
    }

    public class SiteMapDances : SiteMapCategory
    {
        public override string Name => "Dances";
        public override string Type => "music";
        public override IEnumerable<SiteMapEntry> Entries => Dances.Instance.AllDances.Where(d => !(d is DanceInstance)).OrderBy(d => d.Name).Select(d => new SiteMapDance(d));
    }

    public static class SiteMapInfo
    {
        public static IEnumerable<SiteMapCategory> Categories => _categories;

        private static readonly List<SiteMapCategory> _categories = new List<SiteMapCategory>
        {
            new SiteMapCategory
            {
                Name = "Music",
                Type = "music",
                Entries = new List<SiteMapEntry>
                {
                    new SiteMapEntry
                    {
                        Title =  "Song Library", Reference="song",
                        Children = new List<SiteMapEntry>
                        {
                            new SiteMapEntry {Title = "Advanced Search", Reference = "song/advancedsearchform"},
                            new SiteMapEntry {Title = "Saved Searches", Reference = "searches/index"},
                            new SiteMapEntry {Title = "Simple Search (BETA)", Reference = "song/azuresearch"},
                        }
                    },
                    new SiteMapEntry {Title =  "Dance Index", Reference="dances"},
                    new SiteMapEntry
                    {
                        Title =  "Ballroom Competition Categories", Reference="dances/ballroom-competition-categories",
                        Children = new List<SiteMapEntry>
                        {
                            new SiteMapEntry {Title = "International Standard", Reference = "dances/international-standard"},
                            new SiteMapEntry {Title = "International Latin", Reference = "dances/international-latin"},
                            new SiteMapEntry {Title = "American Smooth", Reference = "dances/american-smooth"},
                            new SiteMapEntry {Title = "American Rhythm", Reference = "dances/american-rhythm"},
                        }
                    },
                    new SiteMapEntry {Title =  "Wedding Music", Reference="dances/wedding-music"},
                }
            },
            new SiteMapDances(),
            new SiteMapCategory
            {
                Name = "Info",
                Type = "info",
                Entries = new List<SiteMapEntry>
                {
                    new SiteMapEntry {Title =  "Home Page", Reference=""},
                    new SiteMapEntry {Title =  "About Us", Reference="home/about"},
                    new SiteMapEntry {Title =  "FAQ", Reference="home/faq"},
                    new SiteMapEntry {Title =  "Privacy Policy", Reference="home/privacypolicy"},
                    new SiteMapEntry {Title =  "Terms of Service", Reference="home/termsofservice"},
                    new SiteMapEntry {Title =  "Credits", Reference="home/credits"},
                    new SiteMapEntry {Title =  "Reading List", Reference="blog/reading-list"},
                    new SiteMapEntry
                    {
                        Title =  "Blog", Reference="blog",
                        Children = new List<SiteMapEntry>
                        {
                            new SiteMapEntry {Title = "Introducing Music4Dance.Net/Blog", OneTime=true, Reference = "blog/hello-world/", Description="I’m an amateur dancer and with some training in music who also happens to be a professional software engineer. I love dancing..."},
                            new SiteMapEntry {Title = "The Two Questions that Inspired Music4Dance", Reference = "blog/the-two-questions-that-inspired-music4dance", Description="As a beginning ballroom dancer there were two questions that kept coming up..."},
                            new SiteMapEntry {Title = "Question 1: I’m learning to Cha Cha, where is some great music for practicing?", Reference = "blog/question-1-im-learning-to-cha-cha-where-is-some-great-music-for-practicing", Description="So how do I do that?  Dance generally co-evolves with music, so to get a very traditional song..."},
                            new SiteMapEntry {Title = "Question 2: What dance styles can I dance to my favorite song(s)?", Reference = "blog/question-2-what-dance-styles-can-i-dance-to-my-favorite-songs", Description="One of the things that amazes me about the best dance teachers..."},
                            new SiteMapEntry {Title = "The Pink Martini Solution", Reference = "blog/the-pink-martini-solution", Description="Not all artists are created equal when it comes to creating dance-able music. For instance..."},
                            new SiteMapEntry {Title = "Help: How would you group this dance style?", OneTime=true, Reference = "blog/help-how-would-you-group-this-dance-style", Description="One of the fun things about learning more about different dance styles is..."},
                            new SiteMapEntry {Title = "The “Dancing with the Stars” Solution", Reference = "blog/the-dancing-with-the-stars-solution", Description="I learned to dance in part because <a href='http://www.imdb.com/title/tt0092890/?ref_=nv_sr_1'><i>Dirty Dancing</i></a> made me want to be Johnny Castle..."},
                            new SiteMapEntry {Title = "Wedding Music Part I: Can we dance the Foxtrot to our song?", Reference = "blog/wedding-music-part-i-can-we-dance-the-foxtrot-to-our-song", Description="When did you first learn to dance? For many people it was so that they could dance at their wedding..."},
                            new SiteMapEntry {Title = "Wedding Music Part II: We’re learning to Rumba, help us find a good song for our first dance", Reference = "blog/wedding-music-part-ii-were-learning-to-rumba-help-us-find-a-good-song-for-our-first-dance", Description="What if you are particularly in love with one dance style or are just learning to dance one particular style and are looking for an inspiring first dance song in that style?"},
                            new SiteMapEntry {Title = "I'm a competition ballroom dancer, can I find practice songs that are a specific tempo?", Reference = "blog/im-a-competition-ballroom-dancer-can-i-find-practice-songs-that-are-a-specific-tempo", Description="The quick answer to this question is yes, definitely!First, many of the songs in our catalog have been tagged with a tempo, so it is easy..."},
                            new SiteMapEntry {Title = "What if I want to build a list of songs that are tagged as either Bolero or Rumba?", Reference = "blog/what-if-i-want-to-build-a-list-of-songs-that-are-tagged-as-either-bolero-or-rumba", Description="There are a bunch of different reasons that you might want to build lists of songs that are more sophisticated than just the songs that can be danced to a specific style..."},
                            new SiteMapEntry {Title = "Let's tag some songs", Reference = "blog/lets-tag-some-songs", Description="The tag editor is the first of a number of features that I'm planning that will enable you to customize your music4dance experience..."},
                            new SiteMapEntry {Title = "I am learning the Foxtrot, where can I find some music?", Reference = "blog/i-am-learning-the-foxtrot-where-can-i-find-some-music", Description="The quick answer is to just <a href='https://www.music4dance.net/song/search?dances=FXT'>click this link</a> where you will find a list of over a thousand songs..."},
                            new SiteMapEntry {Title = "Searching for music to dance to just got a whole lot easier", Reference = "blog/searching-for-music-to-dance-to-just-got-a-whole-lot-easier", Description="I have been adding capabilities to the music4dance advanced search control as they are suggested and as time permits.  And it got a bit out of control, so to speak..."},
                            new SiteMapEntry {Title = "Are there songs that you never want to dance to again?", Reference = "blog/are-there-songs-that-you-never-want-to-dance-to-again", Description="I have been adding capabilities to the music4dance advanced search control as they are suggested and as time permits.  And it got a bit out of control, so to speak..."},
                            new SiteMapEntry {Title = "Top Songs of 2015 --  And what to dance to them.", Reference = "blog/top-songs-of-2015-and-what-to-dance-to-them", Description="hat better than a top 100 list to end the year? I've taken the <a href='https://open.spotify.com/user/spotifyyearinmusic/playlist/55tXTZZg4Xtk0BA3kPoJ1s'>Spotify top 100 songs of 2015 (for the USA)</a> and ..."},
                            new SiteMapEntry {Title = "If you like to dance Cha-Cha to a song does that mean you “like” that song?", Reference = "blog/if-you-like-to-dance-cha-cha-to-a-song-does-that-mean-you-like-that-song", Description="I wanted to build a system where dancers could vote on... But then Amanda (the music4dance intern) pointed out..."},
                            new SiteMapEntry {Title = "What are Your Favorite Song to Dance Bachata?", Reference = "blog/what-are-your-favorite-song-to-dance-bachata", Description="Since I’m going to be taking <a href='https://www.music4dance.net/dances/bachata'>Bachata</a> lessons for the first time starting next week..."},
                            new SiteMapEntry {Title = "Quality over Quantity?", Reference = "blog/quality-over-quantity/", Description="One of the things that I’m struggling with ... is the pull between finding lots of recommendations for songs to dance to against the desire that those recommendations being in some sense ‘good.’"},
                            new SiteMapEntry {Title = "EchoNest Integration - Loads of new tempo, meter and other information to help you find music to dance  to", Reference = "blog/echonest-integration-loads-of-new-tempo-meter-and-other-information-to-help-you-find-music-to-dance-to/", Description="I’ve cross indexed the <a href='https://www.music4dance.net/song'>music4dance catalog</a> with the <a href='http://the.echonest.com/'>EchoNest database</a> and exposed some new features..."},
                            new SiteMapEntry {Title = "What if I just want to search for songs on music4dance like I do on Google?", Reference = "blog/what-if-i-just-want-to-search-for-songs-on-music4dance-like-i-do-on-google/", Description="One of the things that I've had a lot of fun with is building a sophisticated search engine where..."},
                            new SiteMapEntry {Title = "Mobile First improvements to the music4dance website", Reference = "blog/mobile-first-improvements-to-the-music4dance-website/", Description="Most of the time that I use music4dance it’s on desktop computer, but I certainly want access to all of what it can do on my phone and tablet..."},
                            new SiteMapEntry {Title = "Search like Google Part II: Autocomplete, Filter by Dance Style and Sorting", Reference = "blog/search-like-google-part-ii-autocomplete-filter-by-dance-style-and-sorting", Description="Auto-complete is something everyone expects when searching..."},
                            new SiteMapEntry {Title = "What are your favorite Prince songs for partner dancing?", Reference = "blog/what-are-your-favorite-prince-songs-for-partner-dancing", Description="I, like many, am mourning and listening to Prince's music. Over and over again..."},
                            //new SiteMapEntry {Title = "", Reference = "", Description=""},
                        }
                    },
                    new SiteMapEntry
                    {
                        Title =  "Help", Reference="blog/music4dance-help",
                        Children = new List<SiteMapEntry>
                        {
                            new SiteMapEntry {Title = "Song List", Reference = "blog/music4dance-help/song-list"},
                            new SiteMapEntry
                            {
                                Title = "Song Details", Reference = "blog/music4dance-help/song-details",
                                Children = new List<SiteMapEntry>
                                {
                                    new SiteMapEntry {Title = "Advanced Search", Reference = "blog/music4dance-help/advanced-search"},
                                    new SiteMapEntry {Title = "Simple Search (BETA)", Reference = "blog/music4dance-help/simple-search"},
                                    new SiteMapEntry {Title = "Full Search (BETA)", Reference = "blog/music4dance-help/full-search"},
                                }
                            },
                            new SiteMapEntry {Title = "Dance Styles", Reference = "blog/music4dance-help/dance-styles-help"},
                            new SiteMapEntry {Title = "Dance Categories", Reference = "blog/music4dance-help/dance-category"},
                            new SiteMapEntry {Title = "Dance Details", Reference = "blog/music4dance-help/dance-details"},
                            new SiteMapEntry {Title = "Tag Cloud", Reference = "blog/music4dance-help/tag-cloud"},
                            new SiteMapEntry {Title = "Tag Definition", Reference = "blog/music4dance-help/tag-definitions"},
                            new SiteMapEntry {Title = "Tag Filtering", Reference = "blog/music4dance-help/tag-filtering"},
                            new SiteMapEntry {Title = "Tag Editing", Reference = "blog/music4dance-help/tag-editing"},
                            new SiteMapEntry {Title = "Tempo Counter", Reference = "blog/music4dance-help/tempo-counter"},
                            new SiteMapEntry {Title = "Dance Tempi", Reference = "blog/music4dance-help/dance-tempi"},
                            new SiteMapEntry {Title = "Account Management", Reference = "blog/music4dance-help/account-management"},
                            new SiteMapEntry
                            {
                                Title = "Playing or Purchasing Songs", Reference = "blog/music4dance-help/playing-or-purchasing-songs/",
                                Children = new List<SiteMapEntry>
                                {
                                    new SiteMapEntry {Title = "ITunes", Reference = "blog/music4dance-help/playing-or-purchasing-songs/itunes"},
                                    new SiteMapEntry {Title = "Amazon", Reference = "blog/music4dance-help/playing-or-purchasing-songs/amazon"},
                                    new SiteMapEntry {Title = "Spotify", Reference = "blog/music4dance-help/playing-or-purchasing-songs/spotify"},
                                    new SiteMapEntry {Title = "Groove", Reference = "blog/music4dance-help/playing-or-purchasing-songs/groove"},
                                    new SiteMapEntry {Title = "EchoNest", Reference = "blog/music4dance-help/playing-or-purchasing-songs/echonest"},
                                }
                            },
                            new SiteMapEntry {Title = "Beta", Reference = "blog/music4dance-help/beta"},
                            new SiteMapEntry {Title = "Feedback", Reference = "blog/feedback"},
                            new SiteMapEntry {Title = "Bug Report", Reference = "blog/bug-report"},
                        }
                    },
                }
            },
            new SiteMapCategory
            {
                Name = "Tools",
                Type = "tools",
                Entries = new List<SiteMapEntry>
                {
                    new SiteMapEntry {Title =  "Dance Counter", Reference="home/counter", Description="Count out the tempo of a song and see a list of dance styles that can be danced at that tempo."},
                    new SiteMapEntry {Title =  "Dance Tempi", Reference="home/tempi", Description="A table of ballroom and social dances organized by tempo."},
                }
            },
            new SiteMapCategory
            {
                Name = "Account",
                Type = "tools",
                Entries = new List<SiteMapEntry>
                {
                    new SiteMapEntry {Title =  "Profile", Reference="manage/userprofile"},
                    new SiteMapEntry {Title =  "Settings", Reference="manage/settings"},
                    new SiteMapEntry {Title =  "Sign In", Reference="account/signin"},
                    new SiteMapEntry {Title =  "Sign Up", Reference="account/signup"},
                }
            },
        };
    }
}

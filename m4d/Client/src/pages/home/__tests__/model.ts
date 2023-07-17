export const model = {
  blogEntries: [
    {
      title: "About music4dance",
      reference: "blog/category/about-music4dance",
      description:
        "Posts that are about the music4dance.net site features, excluding features that are specifically about searching for music.",
      oneTime: false,
      crawl: false,
      children: [
        {
          title: "Introducing Music4Dance.Net/Blog",
          reference: "blog/hello-world/",
          description:
            "I’m an amateur dancer and with some training in music who also happens to be a professional software engineer. I love dancing...",
          oneTime: true,
          crawl: false,
          order: 1,
          fullPath: "https://music4dance.blog/hello-world/",
        },
        {
          title: "Where to Start",
          reference: "blog/where-to-start/",
          description:
            "I've been spinning my wheels a bit with respect to this blog. It is much easier for me to write code than to write prose, but I realize that for anyone to see what I've been doing coding-wise…",
          oneTime: true,
          crawl: false,
          order: 2,
          fullPath: "https://music4dance.blog/where-to-start/",
        },
        {
          title: "The Two Questions that Inspired Music4Dance",
          reference: "blog/the-two-questions-that-inspired-music4dance",
          description:
            "As a beginning ballroom dancer there were two questions that kept coming up...",
          oneTime: false,
          crawl: false,
          order: 3,
          fullPath:
            "https://music4dance.blog/the-two-questions-that-inspired-music4dance",
        },
        {
          title:
            "Question 1: I’m learning to Cha Cha, where is some great music for practicing?",
          reference:
            "blog/question-1-im-learning-to-cha-cha-where-is-some-great-music-for-practicing",
          description:
            "So how do I do that?  Dance generally co-evolves with music, so to get a very traditional song...",
          oneTime: false,
          crawl: false,
          order: 4,
          fullPath:
            "https://music4dance.blog/question-1-im-learning-to-cha-cha-where-is-some-great-music-for-practicing",
        },
        {
          title:
            "Question 2: What dance styles can I dance to my favorite song(s)?",
          reference:
            "blog/question-2-what-dance-styles-can-i-dance-to-my-favorite-songs",
          description:
            "One of the things that amazes me about the best dance teachers...",
          oneTime: false,
          crawl: false,
          order: 5,
          fullPath:
            "https://music4dance.blog/question-2-what-dance-styles-can-i-dance-to-my-favorite-songs",
        },
        {
          title: "Help: How would you group this dance style?",
          reference: "blog/help-how-would-you-group-this-dance-style",
          description:
            "One of the fun things about learning more about different dance styles is...",
          oneTime: true,
          crawl: false,
          order: 7,
          fullPath:
            "https://music4dance.blog/help-how-would-you-group-this-dance-style",
        },
        {
          title: "Documentation for the song list",
          reference: "blog/documentation-for-the-song-list/",
          description:
            'I have resisted writing any real documentation for the <a href="https://www.music4dance.net>music4dance</a> website as it is evolving quickly enough that keeping up with the changes will be difficult',
          oneTime: true,
          crawl: false,
          order: 9,
          fullPath: "https://music4dance.blog/documentation-for-the-song-list/",
        },
        {
          title: "Documentation for the Tempo Counter and Tempi (tempos) tools",
          reference:
            "blog/documentation-for-the-tempo-counter-and-tempi-tempos-tools",
          description:
            "One of the things that was difficult for me as a beginning ballroom dancer (even though I had decent amount of musical background) was to judge the tempo of a song…",
          oneTime: true,
          crawl: false,
          order: 12,
          fullPath:
            "https://music4dance.blog/documentation-for-the-tempo-counter-and-tempi-tempos-tools",
        },
        {
          title: "Documentation for the Tag Cloud",
          reference: "blog/documentation-for-the-tag-cloud",
          description:
            "I've added a brief description of the tag cloud page, as well as modifying the documentation for the song list and dance style pages to reflect changes…",
          oneTime: true,
          crawl: false,
          order: 13,
          fullPath: "https://music4dance.blog/documentation-for-the-tag-cloud",
        },
        {
          title: "Let's tag some songs",
          reference: "blog/lets-tag-some-songs",
          description:
            "The tag editor is the first of a number of features that I'm planning that will enable you to customize your music4dance experience...",
          oneTime: false,
          crawl: false,
          order: 16,
          fullPath: "https://music4dance.blog/lets-tag-some-songs",
        },
        {
          title:
            "If you like to dance Cha-Cha to a song does that mean you “like” that song?",
          reference:
            "blog/if-you-like-to-dance-cha-cha-to-a-song-does-that-mean-you-like-that-song",
          description:
            "I wanted to build a system where dancers could vote on... But then Amanda (the music4dance intern) pointed out...",
          oneTime: true,
          crawl: false,
          order: 21,
          fullPath:
            "https://music4dance.blog/if-you-like-to-dance-cha-cha-to-a-song-does-that-mean-you-like-that-song",
        },
        {
          title: "Quality over Quantity?",
          reference: "blog/quality-over-quantity/",
          description:
            "One of the things that I’m struggling with ... is the pull between finding lots of recommendations for songs to dance to against the desire that those recommendations being in some sense ‘good.’",
          oneTime: false,
          crawl: false,
          order: 23,
          fullPath: "https://music4dance.blog/quality-over-quantity/",
        },
        {
          title:
            "EchoNest Integration - Loads of new tempo, meter and other information to help you find music to dance  to",
          reference:
            "blog/echonest-integration-loads-of-new-tempo-meter-and-other-information-to-help-you-find-music-to-dance-to/",
          description:
            "I’ve cross indexed the <a href='https://www.music4dance.net/song'>music4dance catalog</a> with the <a href='https://en.wikipedia.org/wiki/The_Echo_Nest'>EchoNest database</a> and exposed some new features...",
          oneTime: false,
          crawl: false,
          order: 24,
          fullPath:
            "https://music4dance.blog/echonest-integration-loads-of-new-tempo-meter-and-other-information-to-help-you-find-music-to-dance-to/",
        },
        {
          title: "Mobile First improvements to the music4dance website",
          reference:
            "blog/mobile-first-improvements-to-the-music4dance-website/",
          description:
            "Most of the time that I use music4dance it’s on desktop computer, but I certainly want access to all of what it can do on my phone and tablet...",
          oneTime: true,
          crawl: false,
          order: 26,
          fullPath:
            "https://music4dance.blog/mobile-first-improvements-to-the-music4dance-website/",
        },
        {
          title: "Content Over Code",
          reference: "blog/content-over-code",
          description:
            "As an engineer, I have a rather extreme tendency to dive into code when trying to improve the site...",
          oneTime: true,
          crawl: false,
          order: 31,
          fullPath: "https://music4dance.blog/content-over-code",
        },
        {
          title: "Oops, I didn't mean to throw that needle into the haystack…",
          reference:
            "blog/oops-i-didnt-mean-to-throw-that-needle-into-the-haystack/",
          description:
            "Oh, These Dark Eyes by <i>Tango No. 9</i> is in the <a href='https://www.music4dance.net/song/'>music4dance catalog</a>, but it wasn't even showing up on the first page...",
          oneTime: true,
          crawl: false,
          order: 35,
          fullPath:
            "https://music4dance.blog/oops-i-didnt-mean-to-throw-that-needle-into-the-haystack/",
        },
        {
          title: "How do I find the latest music added to music4dance?",
          reference:
            "blog/how-do-i-find-the-latest-music-added-to-music4dance/",
          description:
            "I'm adding new music just about every week, so if you're a frequent visitor to music4dance how can you see what is new..",
          oneTime: false,
          crawl: false,
          order: 37,
          fullPath:
            "https://music4dance.blog/how-do-i-find-the-latest-music-added-to-music4dance/",
        },
        {
          title: "Share Your Favorite Searches",
          reference: "blog/share-your-favorite-searches/",
          description:
            "Have you found a particularly useful or exciting way to search for music on the <a href='https://www.music4dance.net/'>music4dance site?</a>  Just for instance...",
          oneTime: false,
          crawl: false,
          order: 38,
          fullPath: "https://music4dance.blog/share-your-favorite-searches/",
        },
        {
          title: "Where did all the Collegiate Shag music go?",
          reference: "blog/where-did-all-the-collegiate-shag-music-go/",
          description:
            "When I first started publishing lists of <a href='https://www.music4dance.net/dances/swing'>swing music</a> on the <a href='https://www.music4dance.net/'>music4dance site?</a>...",
          oneTime: true,
          crawl: false,
          order: 39,
          fullPath:
            "https://music4dance.blog/where-did-all-the-collegiate-shag-music-go/",
        },
        {
          title: "Farewell to Groove Music",
          reference: "blog/farewell-to-groove-music/",
          description:
            "<a href='https://www.theverge.com/2017/10/2/16401898/microsoft-groove-music-pass-discontinued-spotify-partner'>Microsoft is \"retiring\" its Groove Music Service</a> (aka Xbox Music aka Zune).  Why am I blogging about this?  Partly...",
          oneTime: true,
          crawl: false,
          order: 45,
          fullPath: "https://music4dance.blog/farewell-to-groove-music/",
        },
        {
          title: "Playing songs from the music4dance catalog",
          reference: "blog/playing-songs-from-music4dance/",
          description:
            "One of the coolest things about the music4dance website was the ability to use the embedded Spotify player to play the results of a search...",
          oneTime: true,
          crawl: false,
          order: 49,
          fullPath: "https://music4dance.blog/playing-songs-from-music4dance/",
        },
        {
          title: "Please Support www.music4dance.net",
          reference: "blog/please-support-www-music4dance-net/",
          description:
            "With a <a href='https://www.music4dance.net/song'>catalog of over twenty-five thousand songs</a> cross-referenced by <a href='https://www.music4dance.net/dances'>dozens of dance styles</a> and <a href='https://www.music4dance.net/tag'>hundreds of tags</a>, we've built a real treasure trove of music to explore...",
          oneTime: false,
          crawl: false,
          order: 51,
          fullPath:
            "https://music4dance.blog/please-support-www-music4dance-net/",
        },
        {
          title: "Check out our new Bonus Content Feature",
          reference: "blog/check-out-our-new-bonus-content-feature/",
          description:
            "As of this writing the publicly visible <a href='https://www.music4dance.net/song'>music4dance catalog</a> contains just over twenty seven thousand songs.  But the underlying index ...",
          oneTime: false,
          crawl: false,
          order: 52,
          fullPath:
            "https://music4dance.blog/check-out-our-new-bonus-content-feature/",
        },
        {
          title: "Music for Dance Through the Decades",
          reference: "blog/music-for-dance-through-the-decades/",
          description:
            "One of the ways that I like to search for music is by era.  At least as far as twentieth-century American music goes...",
          oneTime: false,
          crawl: false,
          order: 53,
          fullPath:
            "https://music4dance.blog/music-for-dance-through-the-decades/",
        },
        {
          title:
            'Farewell to the "Sign in with Microsoft" option on music4dance',
          reference:
            "blog/farewell-to-the-sign-in-with-microsoft-option-on-music4dance/",
          description:
            "Microsoft has moved to a new sign-in protocol and our current system for signing in with a Microsoft account started failing...",
          oneTime: true,
          crawl: false,
          order: 54,
          fullPath:
            "https://music4dance.blog/farewell-to-the-sign-in-with-microsoft-option-on-music4dance/",
        },
        {
          title: "Announcing a music4dance Bug Bounty",
          reference: "blog/announcing-a-musci4dance-bug-bounty/",
          description:
            'Software bugs are miserable things in any context.  But when you have a small project like <a href="https://www.music4dance.net">music4dance</a> where there are so many external dependencies...',
          oneTime: false,
          crawl: false,
          order: 55,
          fullPath:
            "https://music4dance.blog/announcing-a-musci4dance-bug-bounty/",
        },
        {
          title: "The music4dance Blog Just Moved",
          reference: "blog/the-music4dance-blog-just-moved/",
          description:
            'I just moved the music4dance <a href="https://music4dance.blog">blog</a> and <a href="http://music4dance.blog/music4dance-help">help system</a> to a new provider.  I hate the fact that...',
          oneTime: true,
          crawl: false,
          order: 58,
          fullPath: "https://music4dance.blog/the-music4dance-blog-just-moved/",
        },
        {
          title: "Create a Spotify Playlist",
          reference: "blog/create-a-spotify-playlist/",
          description:
            'One of my initial goals with <a href="https://www.music4dance.net">music4dance</a> was to be able to create playlists to dance to.  I can finally say that I’ve got this working in a way that is close to my original vision...',
          oneTime: false,
          crawl: false,
          order: 60,
          fullPath: "https://music4dance.blog/create-a-spotify-playlist/",
        },
        {
          title:
            "Ask music4dance: Can I export music4dance playlists to play locally?",
          reference:
            "blog/ask-music4dance-can-i-export-music4dance-playlist-to-play-locally/",
          description:
            'I’ve been thinking about the concept of <a href="https://www.music4dance.net">music4dance</a> since long before streaming services like <a href="spotify.com">Spotify</a> existed, so the idea of generating playlists...',
          oneTime: false,
          crawl: false,
          order: 63,
          fullPath:
            "https://music4dance.blog/ask-music4dance-can-i-export-music4dance-playlist-to-play-locally/",
        },
        {
          title: "Where is that feature I asked about?",
          reference: "blog/where-is-that-feature-i-asked-about/",
          description:
            'There are a bunch of features that folks have requested that I am really interested in working on.  These are basic features like <a href="https://music4dance.blog/music4dance-help/add-songs/">adding your own songs</a>, community features like being able to ask what to dance to a song or seeing all of a specific dancer’s recommendations...',
          oneTime: true,
          crawl: false,
          order: 64,
          fullPath:
            "https://music4dance.blog/where-is-that-feature-i-asked-about/",
        },
        {
          title: "Tempo Counter (Revisited)",
          reference: "blog/tempo-counter-revisited/",
          description:
            'I just rewrote the <a href="https://www.music4dance.net/Home/tempi">Tempo tool</a> for the <a href="https://www.music4dance.net">music4dance site</a> as part of the current effort to update the site.  In the process I went back...',
          oneTime: false,
          crawl: false,
          order: 65,
          fullPath: "https://music4dance.blog/tempo-counter-revisited/",
        },
        {
          title: "Playing with Dance Tempos",
          reference: "blog/playing-with-dance-tempos/",
          description:
            'As I’ve <a href="https://music4dance.blog/2015/06/04/documentation-for-the-tempo-counter-and-tempi-tempos-tools/">mentioned before</a>, one of the things that I find helpful is to have access to a <a href="https://www.music4dance.net/Home/counter">tempo counter</a> that allows me to tap a beat and both measure the tempo and show me the dance style...',
          oneTime: false,
          crawl: false,
          order: 66,
          fullPath: "https://music4dance.blog/playing-with-dance-tempos/",
        },
        {
          title: "Is Simple Better?",
          reference: "blog/is-simple-better/",
          description:
            "I'm going for the simpler is better concept.  Where the old site had a different color for each section, the new pages are all themed in the same way.  I've also...",
          oneTime: true,
          crawl: false,
          order: 67,
          fullPath: "https://music4dance.blog/is-simple-better/",
        },
        {
          title: "What is Your Favorite music4dance Feature?",
          reference: "blog/what-is-your-favorite-music4dance-feature/",
          description:
            "I'm in the middle of doing a substantial rewrite of music4dance... So, before I arbitrarily start cutting things, I thought I'd ask:  How do you use music4dance?  What are your favorite features?  Please let me know...",
          oneTime: true,
          crawl: false,
          order: 68,
          fullPath:
            "https://music4dance.blog/what-is-your-favorite-music4dance-feature/",
        },
        {
          title: "How do you like to see lists of music to dance to?",
          reference: "blog/how-do-you-like-to-see-lists-of-music-to-dance-to/",
          description:
            'One of the core features of music4dance is to be able to list songs for dancing ...  just roll out what I\'ve got on <a href="https://music4dance.net/song/newmusic">some of the pages</a> and leave the old stuff in place on <a href="https://music4dance.net/song">others</a>. That will give you the opportunity to see them both and compare and give feedback...',
          oneTime: false,
          crawl: false,
          order: 70,
          fullPath:
            "https://music4dance.blog/how-do-you-like-to-see-lists-of-music-to-dance-to/",
        },
        {
          title:
            "Who else likes to dance to this song (and what do they dance to it)?",
          reference:
            "blog/who-else-likes-to-dance-to-this-song-and-what-do-they-dance-to-it/",
          description:
            'As I browse the <a href="https://www.music4dance.net/song">music4dance catalog</a> and find a song I like, it’s nice to be able to see who added it and use that as a way to find other songs that I might like.  To this end...',
          oneTime: false,
          crawl: false,
          order: 76,
          fullPath:
            "https://music4dance.blog/who-else-likes-to-dance-to-this-song-and-what-do-they-dance-to-it/",
        },
        {
          title: "New Feature: Adding Songs to the music4dance Catalog",
          reference:
            "blog/new-feature-adding-songs-to-the-music4dance-catalog/",
          description:
            'I\'m excited to announce that I\'ve nearly completed a feature that will let you <a href="https://www.music4dance.net/song/augment">add songs</a> to the <a href="https://www.music4dance.net/song">music4dance catalog</a>.  I\'ve documented the feature <a href="https://music4dance.blog/music4dance-help/add-songs/">here</a>...  ',
          oneTime: false,
          crawl: false,
          order: 77,
          fullPath:
            "https://music4dance.blog/new-feature-adding-songs-to-the-music4dance-catalog/",
        },
        {
          title:
            "What is the difference between adding a song to Favorites and voting on a  Song's Danceability?",
          reference:
            "blog/what-is-the-difference-between-adding-a-song-to-favorites-and-voting-on-a-songs-danceability/",
          description:
            "I realize that <a href=\"https://music4dance.blog/2016/01/29/if-you-like-to-dance-cha-cha-to-a-song-does-that-mean-you-like-that-song/\">I still haven't made it easy</a> to understand the nuances of a couple of important features.  So I made some changes in terminology and behavior and I'm interested to know if this makes more sense...",
          oneTime: false,
          crawl: false,
          order: 79,
          fullPath:
            "https://music4dance.blog/what-is-the-difference-between-adding-a-song-to-favorites-and-voting-on-a-songs-danceability/",
        },
        {
          title: "Sorry about that nasty bug",
          reference: "blog/sorry-about-that-nasty-bug/",
          description:
            'I introduced a bug in the last update of <a href="https://www.music4dance.net">music4dance</a> and then went on vacation.  This is a pretty classic software engineering blunder and I\'m very sorry for the trouble it caused.  The good news is...',
          oneTime: true,
          crawl: false,
          order: 0,
          fullPath: "https://music4dance.blog/sorry-about-that-nasty-bug/",
        },
        {
          title: "New Feature: More ways to see what's going on at music4dance",
          reference:
            "blog/new-feature-more-ways-to-see-whats-going-on-at-music4dance/",
          description:
            'One of my goals for <a href="https://www.music4dance.net">music4dance</a> is to build a system that people can use to share their knowledge of partner dance music with others.  I probably spent too much time early on in this project...',
          oneTime: false,
          crawl: false,
          order: 82,
          fullPath:
            "https://music4dance.blog/new-feature-more-ways-to-see-whats-going-on-at-music4dance/",
        },
        {
          title: "Ask music4dance: Why am I listed as Anonymous?",
          reference: "blog/ask-music4dance-why-am-i-listed-as-anonymous/",
          description:
            'A few months ago I started working on a set of features with the goal of making <a href="https://www.music4dance.net">music4dance</a> more personalized.  ...   realized that almost all members of the music4dance community have chosen the privacy setting to not share their profile...',
          oneTime: false,
          crawl: false,
          order: 84,
          fullPath:
            "https://music4dance.blog/ask-music4dance-why-am-i-listed-as-anonymous/",
        },
        {
          title:
            "Ask music4dance: Should you add a Single Swing Dance category?",
          reference:
            "blog/ask-music4dance-should-you-add-a-single-swing-dance-category/",
          description:
            '<a href="https://www.music4dance.net/users/info/arne">Arne</a> had <a href="https://music4dance.blog/2022/02/20/new-feature-searching-for-a-song-from-spotify-or-itunes/">another</a> great question: I see Single Swing being danced a lot these days.  Should music4dance add another <a href="https://www.music4dance.net/dances/swing">swing category</a>? Is Single Swing...',
          oneTime: false,
          crawl: false,
          order: 88,
          fullPath:
            "https://music4dance.blog/ask-music4dance-should-you-add-a-single-swing-dance-category/",
        },
        {
          title: "New Feature: Saving and Sharing Searches",
          reference: "blog/new-feature-saving-and-sharing-searches/",
          description:
            'Searching for music to dance to is what <a href="https://www.music4dance.net">music4dance</a> is all about ... Another thing that I hope music4dance will be used for is to share those songs with other dancers...',
          oneTime: false,
          crawl: false,
          order: 90,
          fullPath:
            "https://music4dance.blog/new-feature-saving-and-sharing-searches/",
        },
        {
          title: "Beta Feature: Export to a file",
          reference: "blog/holiday-music-for-partner-dancing-2022/",
          description:
            'A number of the most active members of the <a href="https://www.music4dance.net/">music4dance.net</a> community have requested the ability to download all or part of the song database. My sense is that this has generally been with the intent tag songs in one’s local catalog with the <a href="https://www.music4dance.net/dances">dance style</a>...',
          oneTime: false,
          crawl: false,
          order: 95,
          fullPath:
            "https://music4dance.blog/holiday-music-for-partner-dancing-2022/",
        },
        {
          title:
            "Would you like more content on music4dance.net? If so, what kind?",
          reference:
            "blog/would-you-like-more-content-on-music4dance-net-if-so-what-kind/",
          description:
            '<a href="https://ballroomdj.org/">Brad’s</a> comment on my <a href="https://music4dance.blog/2023/01/29/new-dance-single-swing/">Single Swing post</a> made me realize that I’ve done a bunch of research on dancing and dance music that I haven’t effectively conveyed on the site or the blog. He pointed me to a <a href="http://www.superdancing.com/tempo.asp">site</a> that listed tempo values for <a href="https://www.music4dance.net/dances/single-swing">Single Swing</a>...',
          oneTime: false,
          crawl: false,
          order: 97,
          fullPath:
            "https://music4dance.blog/would-you-like-more-content-on-music4dance-net-if-so-what-kind/",
        },
      ],
      order: 0,
      fullPath: "https://music4dance.blog/category/about-music4dance",
    },
    {
      title: "Searching for Music",
      reference: "blog/category/searching-for-music",
      description:
        "Posts that are about searching for songs, excluding those about special occasions.",
      oneTime: false,
      crawl: false,
      children: [
        {
          title:
            "I'm a competition ballroom dancer, can I find practice songs that are a specific tempo?",
          reference:
            "blog/im-a-competition-ballroom-dancer-can-i-find-practice-songs-that-are-a-specific-tempo",
          description:
            "The quick answer to this question is yes, definitely!First, many of the songs in our catalog have been tagged with a tempo, so it is easy...",
          oneTime: false,
          crawl: false,
          order: 14,
          fullPath:
            "https://music4dance.blog/im-a-competition-ballroom-dancer-can-i-find-practice-songs-that-are-a-specific-tempo",
        },
        {
          title:
            "What if I want to build a list of songs that are tagged as either Bolero or Rumba?",
          reference:
            "blog/what-if-i-want-to-build-a-list-of-songs-that-are-tagged-as-either-bolero-or-rumba",
          description:
            "There are a bunch of different reasons that you might want to build lists of songs that are more sophisticated than just the songs that can be danced to a specific style...",
          oneTime: false,
          crawl: false,
          order: 15,
          fullPath:
            "https://music4dance.blog/what-if-i-want-to-build-a-list-of-songs-that-are-tagged-as-either-bolero-or-rumba",
        },
        {
          title: "I am learning the Foxtrot, where can I find some music?",
          reference:
            "blog/i-am-learning-the-foxtrot-where-can-i-find-some-music",
          description:
            "The quick answer is to just <a href='https://www.music4dance.net/song/search?dances=FXT'>click this link</a> where you will find a list of over a thousand songs...",
          oneTime: false,
          crawl: false,
          order: 17,
          fullPath:
            "https://music4dance.blog/i-am-learning-the-foxtrot-where-can-i-find-some-music",
        },
        {
          title: "Searching for music to dance to just got a whole lot easier",
          reference:
            "blog/searching-for-music-to-dance-to-just-got-a-whole-lot-easier",
          description:
            "I have been adding capabilities to the music4dance advanced search control as they are suggested and as time permits.  And it got a bit out of control, so to speak...",
          oneTime: true,
          crawl: false,
          order: 18,
          fullPath:
            "https://music4dance.blog/searching-for-music-to-dance-to-just-got-a-whole-lot-easier",
        },
        {
          title: "Are there songs that you never want to dance to again?",
          reference:
            "blog/are-there-songs-that-you-never-want-to-dance-to-again",
          description:
            "I have been adding capabilities to the music4dance advanced search control as they are suggested and as time permits.  And it got a bit out of control, so to speak...",
          oneTime: false,
          crawl: false,
          order: 19,
          fullPath:
            "https://music4dance.blog/are-there-songs-that-you-never-want-to-dance-to-again",
        },
        {
          title:
            "What if I just want to search for songs on music4dance like I do on Google?",
          reference:
            "blog/what-if-i-just-want-to-search-for-songs-on-music4dance-like-i-do-on-google/",
          description:
            "One of the things that I've had a lot of fun with is building a sophisticated search engine where...",
          oneTime: false,
          crawl: false,
          order: 25,
          fullPath:
            "https://music4dance.blog/what-if-i-just-want-to-search-for-songs-on-music4dance-like-i-do-on-google/",
        },
        {
          title:
            "Search like Google Part II: Autocomplete, Filter by Dance Style and Sorting",
          reference:
            "blog/search-like-google-part-ii-autocomplete-filter-by-dance-style-and-sorting",
          description:
            "Auto-complete is something everyone expects when searching...",
          oneTime: false,
          crawl: false,
          order: 27,
          fullPath:
            "https://music4dance.blog/search-like-google-part-ii-autocomplete-filter-by-dance-style-and-sorting",
        },
        {
          title:
            "Search like Google Part III: Advanced Search - The Best of Both Worlds?",
          reference:
            "blog/search-like-google-part-iii-advanced-search-the-best-of-both-worlds",
          description:
            "I've just updated the music4dance site with the remaining features for our search beta...",
          oneTime: false,
          crawl: false,
          order: 29,
          fullPath:
            "https://music4dance.blog/search-like-google-part-iii-advanced-search-the-best-of-both-worlds",
        },
        {
          title: "“Search like Google” is now the default",
          reference: "blog/search-like-google-is-now-the-default",
          description:
            "I’ve just updated the music4dance site to make the new search engine the default...",
          oneTime: true,
          crawl: false,
          order: 30,
          fullPath:
            "https://music4dance.blog/search-like-google-is-now-the-default",
        },
        {
          title: "Finding the latest music on music4dance (take 2)",
          reference: "blog/finding-the-latest-music-on-music4dance-take-2/",
          description:
            "There are enough people that visit music4dance regularly that I thought...",
          oneTime: false,
          crawl: false,
          order: 47,
          fullPath:
            "https://music4dance.blog/finding-the-latest-music-on-music4dance-take-2/",
        },
        {
          title:
            "Ask music4dance: Why don't you have info about musical genres like you do about dance styles?",
          reference:
            "blog/ask-music4dance-why-dont-you-have-info-about-musical-genres-like-you-do-about-dance-styles/",
          description:
            "I searched on your webpage, I could not find info about genre Pop. Can you show me info about genre Pop...",
          oneTime: false,
          crawl: false,
          order: 62,
          fullPath:
            "https://music4dance.blog/ask-music4dance-why-dont-you-have-info-about-musical-genres-like-you-do-about-dance-styles/",
        },
        {
          title:
            'Ask music4Dance: How do I find a "Pop Rock" song to dance a Slow Foxtrot to?',
          reference:
            "blog/ask-music4dance-how-do-i-find-a-pop-rock-song-to-dance-a-slow-foxtrot-to/",
          description:
            'This is another question that I’ve seen a bunch of variations on over the years. I love <a href="https://en.wikipedia.org/wiki/Big_band">Big Band</a> music and grew up playing <a href="https://en.wikipedia.org/wiki/Count_Basie">Basie</a> and <a href="https://en.wikipedia.org/wiki/Benny_Goodman">Goodman</a> in Jazz bands...',
          oneTime: false,
          crawl: false,
          order: 74,
          fullPath:
            "https://music4dance.blog/ask-music4dance-how-do-i-find-a-pop-rock-song-to-dance-a-slow-foxtrot-to/",
        },
        {
          title: "New Feature: General Search",
          reference: "blog/new-feature-general-search/",
          description:
            "I've finally added a feature that should really be a part of any good website. A general search of the entire site is available by typing one or more keywords in the search box in the upper right and clicking search. Try it out, and let me know what you think...",
          oneTime: false,
          crawl: false,
          order: 86,
          fullPath: "https://music4dance.blog/new-feature-general-search/",
        },
        {
          title: "New Feature: Searching for a Song from Soptify or ITunes",
          reference:
            "blog/new-feature-searching-for-a-song-from-spotify-or-itunes/",
          description:
            'A new member of the <a href="https://www.music4dance.net">music4dance</a> community, <a href="https://www.music4dance.net/users/info/arne">Arne</a>, pointed out that he expected to be able to search by Spotify Id. Furthermore, he figured out how to do that...',
          oneTime: false,
          crawl: false,
          order: 87,
          fullPath:
            "https://music4dance.blog/new-feature-searching-for-a-song-from-spotify-or-itunes/",
        },
        {
          title: "New Feature: Filter by Song Length",
          reference: "blog/new-feature-filter-by-song-length/",
          description:
            "If you're trying to get a playlist together for a social dance, it would be nice for the songs to be a reasonable length for your audience. I realize that DJ tools will...",
          oneTime: false,
          crawl: false,
          order: 89,
          fullPath:
            "https://music4dance.blog/new-feature-filter-by-song-length/",
        },
        {
          title:
            "New Feature: Searching for only the songs that someone has voted for",
          reference:
            "blog/new-feature-searching-for-only-the-songs-that-someone-has-voted-for/",
          description:
            '<a href="https://www.music4dance.net/users/info/arne">Arne</a> pointed out the other day that it would be useful to be able to build a playlist for just the songs that he had <a href="https://music4dance.blog/music4dance-help/dance-tags/">voted</a> for dancing <a href="https://www.music4dance.net/dances/cha-cha">Cha Cha</a>. I scratched my head a bit...',
          oneTime: false,
          crawl: false,
          order: 93,
          fullPath:
            "https://music4dance.blog/new-feature-searching-for-only-the-songs-that-someone-has-voted-for/",
        },
        {
          title: "New Dance: Single Swing",
          reference: "blog/new-dance-single-swing/",
          description:
            'I’ve added <a href="https://www.music4dance.net/dances/single-swing">Single Swing</a> as a <a href="https://www.music4dance.net/dances">dance style</a> that can be <a href="https://music4dance.blog/music4dance-help/song-list/">searched on</a> and <a href="https://music4dance.blog/music4dance-help/dance-tags/">voted for</a> in the <a href="https://www.music4dance.net/song"music4dance catalog</a>. While I think of this dance as a short-cut to use when I want to dance East Coast Swing to faster Jive or Lindy-Hop music, I’ve received <a href="https://music4dance.blog/2022/03/27/ask-music4dance-should-you-add-a-single-swing-dance-category/">enough feedback</a> from the community...',
          oneTime: false,
          crawl: false,
          order: 96,
          fullPath: "https://music4dance.blog/new-dance-single-swing/",
        },
      ],
      order: 0,
      fullPath: "https://music4dance.blog/category/searching-for-music",
    },
    {
      title: "Special Occasions",
      reference: "blog/category/special-occasions",
      description:
        "Searching for music related to special occasions (Holidays, Weddings, etc.)",
      oneTime: false,
      crawl: false,
      children: [
        {
          title: "Wedding Music Part I: Can we dance the Foxtrot to our song?",
          reference:
            "blog/wedding-music-part-i-can-we-dance-the-foxtrot-to-our-song",
          description:
            "When did you first learn to dance? For many people it was so that they could dance at their wedding...",
          oneTime: true,
          crawl: false,
          order: 10,
          fullPath:
            "https://music4dance.blog/wedding-music-part-i-can-we-dance-the-foxtrot-to-our-song",
        },
        {
          title:
            "Wedding Music Part II: We’re learning to Rumba, help us find a good song for our first dance",
          reference:
            "blog/wedding-music-part-ii-were-learning-to-rumba-help-us-find-a-good-song-for-our-first-dance",
          description:
            "What if you are particularly in love with one dance style or are just learning to dance one particular style and are looking for an inspiring first dance song in that style?",
          oneTime: true,
          crawl: false,
          order: 11,
          fullPath:
            "https://music4dance.blog/wedding-music-part-ii-were-learning-to-rumba-help-us-find-a-good-song-for-our-first-dance",
        },
        {
          title: "Holiday Music for Partner Dancing",
          reference: "blog/holiday-music-for-partner-dancing/",
          description:
            "It is that time of year when dancers are looking for holiday music for dancing...",
          oneTime: false,
          crawl: false,
          order: 46,
          fullPath:
            "https://music4dance.blog/holiday-music-for-partner-dancing/",
        },
        {
          title: "Holiday Music for Partner Dancing (Take 2)",
          reference: "blog/holiday-music-for-partner-dancing-take-2/",
          description:
            "It's that time of year again - people are searching for holiday music for showcases and holiday party dances...",
          oneTime: false,
          crawl: false,
          order: 50,
          fullPath:
            "https://music4dance.blog/holiday-music-for-partner-dancing-take-2/",
        },
        {
          title: "Holiday Music for Partner Dancing 2019",
          reference: "blog/holiday-music-for-partner-dancing-2019/",
          description:
            "And yet again, it’s that time of year when dancers and DJs are looking for holiday music for routines and holiday dance parties.  In my third annual installment...",
          oneTime: false,
          crawl: false,
          order: 61,
          fullPath:
            "https://music4dance.blog/holiday-music-for-partner-dancing-2019/",
        },
        {
          title: "Are you looking for Halloween Music to dance to?",
          reference: "blog/are-you-looking-for-halloween-music-to-dance-to/",
          description:
            "Halloween is almost here and yet again I am late setting up something for Halloween related playlists.  In past years, I've just let this go since it feels like it's too late to get something together when I start thinking about it...",
          oneTime: false,
          crawl: false,
          order: 69,
          fullPath:
            "https://music4dance.blog/are-you-looking-for-halloween-music-to-dance-to/",
        },
        {
          title: "Holiday Music for Partner Dancing 2020",
          reference: "blog/holiday-music-for-partner-dancing-2020/",
          description:
            "On a normal year, this would be a bit late for my normal <a href=\"https://music4dance.blog/tag/holiday-music/\">Holiday Music</a> blog post.  But if you're like me, you're not planning to participate in a holiday dance party in the middle of a pandemic.  So it's more of a case of thinking about past and future years...",
          oneTime: false,
          crawl: false,
          order: 71,
          fullPath:
            "https://music4dance.blog/holiday-music-for-partner-dancing-2020/",
        },
        {
          title: "Holiday Music for Partner Dancing 2021",
          reference: "blog/holiday-music-for-partner-dancing-2021/",
          description:
            'It\'s the time of year again to talk about <a href="https://www.music4dance.net/song/holidaymusic">Holiday Music<a>....',
          oneTime: false,
          crawl: false,
          order: 81,
          fullPath:
            "https://music4dance.blog/holiday-music-for-partner-dancing-2021/",
        },
        {
          title: "Valentine's Day Edition: Love Songs that We Love to Dance to",
          reference:
            "blog/valentines-day-edition-love-songs-that-we-love-to-dance-to/",
          description:
            'I’ve been making an effort to tag songs and write sophisticated searches to find good songs for <a href="https://www.music4dance.net/song/holidaymusic">holiday dances</a>. The <a href="http://www.music4dance.net/song">music4dance catalog</a> is set up for this kind of search. Not only that, but I’ve always really enjoyed doing exhibition pieces for holiday parties...',
          oneTime: false,
          crawl: false,
          order: 85,
          fullPath:
            "https://music4dance.blog/valentines-day-edition-love-songs-that-we-love-to-dance-to/",
        },
        {
          title: "Holiday Music for Partner Dancing 2022",
          reference: "blog/holiday-music-for-partner-dancing-2022/",
          description:
            'It’s the time of year again to talk about <a href="https://music4dance.blog/tag/holiday-music/">Holiday Music</a>. For the second year in a row, I haven’t done any new work on the <a href="https://music4dance.blog/tag/holiday-music/">Holiday Music</a> page...',
          oneTime: false,
          crawl: false,
          order: 94,
          fullPath:
            "https://music4dance.blog/holiday-music-for-partner-dancing-2022/",
        },
        {
          title:
            'We’d like to dance a "real" partner dance as the first dance at our wedding (Part I: We already chose our song)',
          reference:
            "blog/wed-like-to-dance-a-real-partner-dance-as-the-first-dance-at-our-wedding-part-i-we-already-chose-our-song/",
          description:
            'Wedding season is upon us, and one of the things that come with weddings is receptions with <a href="https://www.music4dance.net/Song/?filter=Index-.-.-.-.-.-.-.-.-+First%20Dance:Other">first dances</a>, <a href="https://www.music4dance.net/Song/?filter=Index-.-.-.-.-.-.-.-.-+Father%20Daughter:Other">father/daughter dances</a>, <a href="https://www.music4dance.net/Song/?filter=Index-.-.-.-.-.-.-.-.-+Mother%20Son:Other"> mother/son dances</a>, <a href="https://www.music4dance.net/Song/?filter=Index-.-.-.-.-.-.-.-.-+Mother%20Daughter:Other"> mother/daughter dances</a>, and any other variation you can think of. I think it’s extra special when those dances are recognizably partner <a href="https://www.music4dance.net/dances">dances</a>...',
          oneTime: false,
          crawl: false,
          order: 98,
          fullPath:
            "https://music4dance.blog/wed-like-to-dance-a-real-partner-dance-as-the-first-dance-at-our-wedding-part-i-we-already-chose-our-song/",
        },
        {
          title:
            'We’d like to dance a "real" partner dance as the first dance at our wedding (Part II: We already chose our dance)',
          reference:
            "blog/wed-like-to-dance-a-real-partner-dance-as-the-first-dance-at-our-wedding-part-ii-we-already-chose-our-dance/",
          description:
            'Last time I wrote about how <a href="https://www.music4dance.net">music4dance</a> can help you find a dance to match the song you’d like to dance to for your first dance (or other wedding dances). This time, I’ll cover how the site can help you find a song if you already know what dance style you want to dance. Before I dig into that, I’d like to repeat that your local dance studio and your wedding DJ are both excellent sources of ideas...',
          oneTime: false,
          crawl: false,
          order: 99,
          fullPath:
            "https://music4dance.blog/wed-like-to-dance-a-real-partner-dance-as-the-first-dance-at-our-wedding-part-ii-we-already-chose-our-dance/",
        },
      ],
      order: 0,
      fullPath: "https://music4dance.blog/category/special-occasions",
    },
    {
      title: "Music and Dance",
      reference: "blog/category/music-and-dance",
      description:
        "Posts that are about music as it relates to dance and dance as it relates to music.",
      oneTime: false,
      crawl: false,
      children: [
        {
          title: "The Pink Martini Solution",
          reference: "blog/the-pink-martini-solution",
          description:
            "Not all artists are created equal when it comes to creating dance-able music. For instance...",
          oneTime: false,
          crawl: false,
          order: 6,
          fullPath: "https://music4dance.blog/the-pink-martini-solution",
        },
        {
          title: "The “Dancing with the Stars” Solution",
          reference: "blog/the-dancing-with-the-stars-solution",
          description:
            "I learned to dance in part because <a href='http://www.imdb.com/title/tt0092890/?ref_=nv_sr_1'><i>Dirty Dancing</i></a> made me want to be Johnny Castle...",
          oneTime: false,
          crawl: false,
          order: 8,
          fullPath:
            "https://music4dance.blog/the-dancing-with-the-stars-solution",
        },
        {
          title: "Top Songs of 2015 --  And what to dance to them.",
          reference: "blog/top-songs-of-2015-and-what-to-dance-to-them",
          description:
            "What better than a top 100 list to end the year? I've taken the <a href='https://open.spotify.com/user/spotifyyearinmusic/playlist/55tXTZZg4Xtk0BA3kPoJ1s'>Spotify top 100 songs of 2015 (for the USA)</a> and ...",
          oneTime: false,
          crawl: false,
          order: 20,
          fullPath:
            "https://music4dance.blog/top-songs-of-2015-and-what-to-dance-to-them",
        },
        {
          title: "What are Your Favorite Song to Dance Bachata?",
          reference: "blog/what-are-your-favorite-song-to-dance-bachata",
          description:
            "Since I’m going to be taking <a href='https://www.music4dance.net/dances/bachata'>Bachata</a> lessons for the first time starting next week...",
          oneTime: false,
          crawl: false,
          order: 22,
          fullPath:
            "https://music4dance.blog/what-are-your-favorite-song-to-dance-bachata",
        },
        {
          title: "What are your favorite Prince songs for partner dancing?",
          reference:
            "blog/what-are-your-favorite-prince-songs-for-partner-dancing",
          description:
            "I, like many, am mourning and listening to Prince's music. Over and over again...",
          oneTime: false,
          crawl: false,
          order: 28,
          fullPath:
            "https://music4dance.blog/what-are-your-favorite-prince-songs-for-partner-dancing",
        },
        {
          title: "Do Dancers Think in Eights?",
          reference: "blog/do-dancers-think-in-eights/",
          description:
            "I was tickled to hear <a href='https://en.wikipedia.org/wiki/Nigel_Lythgoe'>Nigel Lythgoe</a> talk a little about choreographing tap on a <a href='http://www.hulu.com/watch/977710'>recent episode</a> of <a href='https://en.wikipedia.org/wiki/So_You_Think_You_Can_Dance:_The_Next_Generation_(U.S.)'>So You Think You Can Dance</a>...",
          oneTime: false,
          crawl: false,
          order: 32,
          fullPath: "https://music4dance.blog/do-dancers-think-in-eights/",
        },
        {
          title: "Farewell to Rio 2016, but we'll always have The Samba",
          reference:
            "blog/farewell-to-rio-2016-but-well-always-have-the-samba/",
          description:
            "Now that the 2016 <a href='https://www.olympic.org'>Olympics</a> are over and the <a href='https://www.paralympic.org/'>Paralympics</a> are wrapping up,",
          oneTime: false,
          crawl: false,
          order: 33,
          fullPath:
            "https://music4dance.blog/farewell-to-rio-2016-but-well-always-have-the-samba/",
        },
        {
          title: "Dancing With The Stars, Revisited",
          reference: "blog/dancing-with-the-stars-revisited/",
          description:
            "I use this show and other as a source for new music, but that biases things...",
          oneTime: false,
          crawl: false,
          order: 34,
          fullPath:
            "https://music4dance.blog/dancing-with-the-stars-revisited/",
        },
        {
          title: "What is a Fake Waltz?",
          reference: "blog/what-is-a-fake-waltz/",
          description:
            'I was recently asked why there are songs tagged as <a href="https://www.music4dance.net/dances/waltz">Waltz</a> in the <a href=\'https://www.music4dance.net/song\'>music4dance catalog</a> that are in <a href="https://www.music4dance.net/song/addtags?tags=%2B4%2F4:Tempo">4/4</a> time...',
          oneTime: false,
          crawl: false,
          order: 40,
          fullPath: "https://music4dance.blog/what-is-a-fake-waltz/",
        },
        {
          title: "Musicians for Dancers",
          reference: "blog/musicians-for-dancers/",
          description:
            "One of the things I enjoy most about the <a href='https://www.music4dance.net/song'>music4dance project</a> is when I get feedback from people who have found the site useful...",
          oneTime: false,
          crawl: false,
          order: 41,
          fullPath: "https://music4dance.blog/musicians-for-dancers/",
        },
        {
          title: "World of Dance",
          reference: "blog/world-of-dance/",
          description:
            "Have you seen the new TV series <a href='https://worldofdance.com/'>World of DanceL</a>?  If you have any appreciation of dance you should really...",
          oneTime: false,
          crawl: false,
          order: 42,
          fullPath: "https://music4dance.blog/world-of-dance/",
        },
        {
          title: "Tango, Argentine Tango, Ballroom Tango, Oh My!",
          reference: "blog/tango-argentine-tango-ballroom-tango-oh-my/",
          description:
            "I just took a beginning Argentine Tango class and really enjoyed the experience.  I’ve had some experience",
          oneTime: false,
          crawl: false,
          order: 44,
          fullPath:
            "https://music4dance.blog/tango-argentine-tango-ballroom-tango-oh-my/",
        },
        {
          title: "Dance Pride",
          reference: "blog/dance-pride/",
          description:
            'Each year <a href="https://www.spotify.com">Spotify</a> does a number of fun playlists in support of <a href="https://www.seattlepride.org/">Pride weekend</a>.  With this being the 50th anniversary of the <a href="https://en.wikipedia.org/wiki/Stonewall_riots"Stonewall</a> riots...   ',
          oneTime: false,
          crawl: false,
          order: 57,
          fullPath: "https://music4dance.blog/dance-pride/",
        },
        {
          title:
            "Ask music4dance: Why is the tempo that you're listing for Ricky Martin's \"Casi Un Bolero\" wrong?",
          reference:
            "blog/ask-music4dance-why-is-the-tempo-that-youre-listing-for-ricky-martins-casi-un-bolero-wrong/",
          description:
            "I’ve seen a number of questions recently about why information on the site is wrong.  So I’ll start with one of the easier ones, which I’ve seen a number of variations on...",
          oneTime: false,
          crawl: false,
          order: 72,
          fullPath:
            "https://music4dance.blog/ask-music4dance-why-is-the-tempo-that-youre-listing-for-ricky-martins-casi-un-bolero-wrong/",
        },
        {
          title:
            "Ask music4dance: Why is a song tagged with the wrong dance style?",
          reference:
            "blog/ask-music4dance-why-is-a-song-tagged-with-the-wrong-dance-style/",
          description:
            'Variations on that question make up a significant amount of the <a href="https://music4dance.blog/feedback/">feedback</a> I get here at <a href="https://www.music4dance.net">music4dance</a>.  The first answer to these questions is that <a href="https://www.music4dance.net">music4dance</a> is crowd-sourced.  So someone...',
          oneTime: false,
          crawl: false,
          order: 73,
          fullPath:
            "https://music4dance.blog/ask-music4dance-why-is-a-song-tagged-with-the-wrong-dance-style/",
        },
      ],
      order: 0,
      fullPath: "https://music4dance.blog/category/music-and-dance",
    },
    {
      title: "Reviews",
      reference: "blog/category/reviews",
      description:
        "Posts that are about music as it relates to dance and dance as it relates to music.",
      oneTime: false,
      crawl: false,
      children: [
        {
          title: "Feel the Beat",
          reference: "blog/feel-the-beat/",
          description:
            "If you want to be able to enjoy partner dancing and look good while doing it, you have to be able to dance to the music...",
          oneTime: false,
          crawl: false,
          order: 36,
          fullPath: "https://music4dance.blog/feel-the-beat/",
        },
        {
          title: "crowdnote.org",
          reference: "blog/crowdnote-org/",
          description:
            'Another programmer and amateur ballroom dancer created a site called <a href="https://www.crowdnote.org">crowdnote.org</a> that solves some...',
          oneTime: true,
          crawl: false,
          order: 43,
          fullPath: "https://music4dance.blog/crowdnote-org/",
        },
        {
          title: "Book Review: The meaning of TANGO",
          reference: "blog/book-review-the-meaning-of-tango/",
          description:
            "This is a fun book for Tango dancers of all types.  The book is very centered around traditional Argentine Tango and does an excellent job of...",
          oneTime: false,
          crawl: false,
          order: 48,
          fullPath:
            "https://music4dance.blog/book-review-the-meaning-of-tango/",
        },
        {
          title: "Beautiful Dance",
          reference: "blog/beautiful-dance/",
          description:
            '<a href="https://www.music4dance.net">music4dance</a> is all about the relationship between music and dance.  And naturally, on the website and the blog, I tend to concentrate on the musical aspect...But every once in a while it\'s nice to step back and remember that dance is visually beautiful...   ',
          oneTime: false,
          crawl: false,
          order: 56,
          fullPath: "https://music4dance.blog/beautiful-dance/",
        },
        {
          title: "Book Review: Hear the Beat, Feel the Music",
          reference: "blog/hear-the-beat-feel-the-music/",
          description:
            'As anyone who has spent any time reading my <a href="https://music4dance.blog">blog</a> or interacting with my <a href="https://www.music4dance.net">website</a> should know by now, I’m very passionate about music, dance and the relationship between the two...',
          oneTime: false,
          crawl: false,
          order: 59,
          fullPath: "https://music4dance.blog/hear-the-beat-feel-the-music/",
        },
        {
          title: "Book Review: Partner Dance Success",
          reference: "blog/book-review-partner-dance-success/",
          description:
            "This book is a great collection of practical advice for anyone new to partner dancing.  The author is a professional drummer turned social dancer who brings experience from building skill as a musician to the dance floor...",
          oneTime: false,
          crawl: false,
          order: 75,
          fullPath:
            "https://music4dance.blog/book-review-partner-dance-success/",
        },
        {
          title: "Dance in Science Fiction and Fantasy",
          reference: "blog/dance-in-science-fiction-and-fantasy/",
          description:
            "The music4dance project is an expression of the overlap of three of my lifelong interests – music, partner dancing, and programming. Reading Science Fiction and Fantasy is another life-long pass time that precedes both my entry into computer science and my introduction to ballroom dance...",
          oneTime: false,
          crawl: false,
          order: 78,
          fullPath:
            "https://music4dance.blog/dance-in-science-fiction-and-fantasy/",
        },
        {
          title: "Dance as Language",
          reference: "blog/dance-as-language/",
          description:
            'I was delighted to find that the folks at the <a href="https://www.npr.org/podcasts/510324/rough-translation">Rough Translation</a> podcast produced an episode called <a href="https://www.npr.org/2021/12/22/1066965712/may-we-have-this-dance">May We Have This Dance?</a>  For those who haven\'t heard of it...',
          oneTime: false,
          crawl: false,
          order: 83,
          fullPath: "https://music4dance.blog/dance-as-language/",
        },
        {
          title:
            "Book Review: Swingin' at the Savoy: The Memoir of a Jazz Dancer",
          reference:
            "blog/book-review-swingin-the-savoy-the-memoir-of-a-jazz-dancer/",
          description:
            '<a href="https://amzn.to/3RXnS6t">Swingin’ at the Savoy</a> is a beautiful memoir of one of the greatest <a href="https://www.music4dance.net/dances/lindy-hop">Lindy Hop</a> dancers of all time. Ms. Miller was not only one of the dancers that defined Lindy Hop, but as Lindy Hop faded for a while post World War II, she launched a career as a Jazz Dancer....',
          oneTime: false,
          crawl: false,
          order: 91,
          fullPath:
            "https://music4dance.blog/book-review-swingin-the-savoy-the-memoir-of-a-jazz-dancer/",
        },
        {
          title: "Book Review: How to Read Music in 30 Days",
          reference: "blog/book-review-how-to-read-music-in-30-days/",
          description:
            'While dancers definitely don’t need to be able to read music, it is helpful to be able to dig up sheet music for a song and understand the meter and tempo markings. This can act as a sanity check against what you hear, tap out in a <a href="https://www.music4dance.net/home/counter">tempo counter</a>, or find by just stepping out the dance. <a href="https://amzn.to/3AH5gji">How to Read Music in 30 Days</a>...',
          oneTime: false,
          crawl: false,
          order: 92,
          fullPath:
            "https://music4dance.blog/book-review-how-to-read-music-in-30-days/",
        },
      ],
      order: 0,
      fullPath: "https://music4dance.blog/category/reviews",
    },
  ],
  dances: [
    {
      dances: [
        {
          name: "international-standard",
          title: "International Standard",
          controller: "dances",
        },
        {
          name: "international-latin",
          title: "International Latin",
          controller: "dances",
        },
        {
          name: "american-smooth",
          title: "American Smooth",
          controller: "dances",
        },
        {
          name: "american-rhythm",
          title: "American Rhythm",
          controller: "dances",
        },
      ],
      image: "ballroom",
      title: "Ballroom",
      topDance: "ballroom-competition-categories",
      fullTitle: "Music for Ballroom Dancers",
    },
    {
      dances: [
        {
          name: "lindy-hop",
          title: "Lindy Hop",
          controller: "dances",
        },
        {
          name: "east-coast-swing",
          title: "East Coast Swing",
          controller: "dances",
        },
        {
          name: "west-coast-swing",
          title: "West Coast Swing",
          controller: "dances",
        },
        {
          name: "hustle",
          title: "Hustle",
          controller: "dances",
        },
        {
          name: "jive",
          title: "Jive",
          controller: "dances",
        },
        {
          name: "jump-swing",
          title: "Jump Swing",
          controller: "dances",
        },
        {
          name: "carolina-shag",
          title: "Carolina Shag",
          controller: "dances",
        },
        {
          name: "collegiate-shag",
          title: "Collegiate Shag",
          controller: "dances",
        },
      ],
      image: "swing",
      title: "Swing",
      topDance: "swing",
      fullTitle: "Music for Swing Dancers",
    },
    {
      dances: [
        {
          name: "salsa",
          title: "Salsa",
          controller: "dances",
        },
        {
          name: "bachata",
          title: "Bachata",
          controller: "dances",
        },
        {
          name: "cumbia",
          title: "Cumbia",
          controller: "dances",
        },
        {
          name: "merengue",
          title: "Merengue",
          controller: "dances",
        },
        {
          name: "mambo",
          title: "Mambo",
          controller: "dances",
        },
        {
          name: "cha-cha",
          title: "Cha Cha",
          controller: "dances",
        },
        {
          name: "rumba",
          title: "Rumba",
          controller: "dances",
        },
        {
          name: "samba",
          title: "Samba",
          controller: "dances",
        },
        {
          name: "bossa-nova",
          title: "Bossa Nova",
          controller: "dances",
        },
      ],
      image: "salsa",
      title: "Latin",
      topDance: "latin",
      fullTitle: "Music for Latin Dancers",
    },
    {
      dances: [
        {
          name: "argentine-tango",
          title: "Argentine Tango",
          controller: "dances",
        },
        {
          name: "neo-tango",
          title: "Neo Tango",
          controller: "dances",
        },
        {
          name: "milonga",
          title: "Milonga",
          controller: "dances",
        },
        {
          name: "tango-(ballroom)",
          title: "Ballroom Tango",
          controller: "dances",
        },
      ],
      image: "tango",
      title: "Tango",
      topDance: "tango",
      fullTitle: "Music for Tango Dancers",
    },
    {
      dances: [
        {
          title: "First Dance",
          name: "index",
          controller: "song",
          queryString: "filter=Index-.-.-.-.-.-.-.-1-+First Dance:Other",
        },
        {
          title: "Mother/Son",
          name: "index",
          controller: "song",
          queryString: "filter=Index-.-.-.-.-.-.-.-1-+Mother Son:Other",
        },
        {
          title: "Father/Daughter",
          name: "index",
          controller: "song",
          queryString: "filter=Index-.-.-.-.-.-.-.-1-+Father Daughter:Other",
        },
        {
          title: "Last Dance",
          name: "index",
          controller: "song",
          queryString: "filter=Index-.-.-.-.-.-.-.-1-+Last Dance:Other",
        },
      ],
      image: "wedding",
      title: "Wedding",
      topDance: "wedding-music",
      fullTitle: "Music for Wedding Dances",
    },
  ],
};

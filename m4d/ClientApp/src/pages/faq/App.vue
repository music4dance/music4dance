<script setup lang="ts">
import { type BreadCrumbItem, infoTrail } from "@/models/BreadCrumbItem";

const breadcrumbs: BreadCrumbItem[] = [...infoTrail, { text: "FAQ", active: true }];
</script>

<template>
  <PageFrame id="app" title="Frequently Asked Questions" :breadcrumbs="breadcrumbs">
    <div role="tablist">
      <BCard no-body class="mb-1">
        <BCardHeader header-tag="header" class="p-1" role="tab">
          <BButton v-b-toggle.accordion-1 block variant="primary">
            Where can I find songs that work with the dance style I'm currently practicing?
          </BButton>
        </BCardHeader>
        <BCollapse id="accordion-1" visible accordion="my-accordion" role="tabpanel">
          <BCardBody>
            <BCardText>
              <p>
                From the "Music Menu" at the top of the page, choose
                <a href="/song">Song Library</a>, then click on the "All Dances" button in the upper
                left to choose the style of dance you are interestaed in. Or from the same menu
                choose
                <a href="/song/advancedsearchform">Advanced Search</a>
                and include the style of dance you are interested then click search. In either case,
                the result should be a list of songs that can be danced to that style.
              </p>
              <p>
                If you would like more information check out our related blog posts.
                <a
                  href="https://music4dance.blog/question-1-im-learning-to-cha-cha-where-is-some-great-music-for-practicing/"
                >
                  I’m learning to Cha Cha, where is some great music for practicing?</a
                >
                and
                <a
                  href="https://music4dance.blog/i-am-learning-the-foxtrot-where-can-i-find-some-music/"
                >
                  I am learning the Foxtrot, where can I find some music?</a
                >
              </p>
              <p>
                The
                <a href="https://music4dance.blog/music4dance-help/advanced-search/"
                  >Advanced Search Documentation</a
                >
                describes the different search attributes in more detail.
              </p>
            </BCardText>
          </BCardBody>
        </BCollapse>
      </BCard>

      <BCard no-body class="mb-1">
        <BCardHeader header-tag="header" class="p-1" role="tab">
          <BButton v-b-toggle.accordion-2 block variant="primary">
            What dance styles work with the song I'm listening to or a specific song that I know of?
          </BButton>
        </BCardHeader>
        <BCollapse id="accordion-2" accordion="my-accordion" role="tabpanel">
          <BCardBody>
            <BCardText>
              <p>
                With the <a href="/home/counter">tempo counter tool</a> you can tap out the beat
                while listening to a song, and we'll show you a list of styles that can be danced to
                the beat you just tapped out as well as links to the dance styles' information
                pages.
              </p>
              <p>
                Or if you have a particular song in mind,
                <a href="/song/advancedsearchform">search</a> for it and if it is in the database
                look at what styles and tags are associated with it. Better yet, if you're listening
                to it in Spotify or Apple Music, you can drag the song to the text control on the
                <a href="/song">song library page</a> or
                <a href="/song/advancedsearchform">advanced search page</a> and if it's in the
                music4dance catalog, we'll take you right to it. See
                <a
                  href="https://music4dance.blog/new-feature-searching-for-a-song-from-spotify-or-itunes/"
                  >Searching for a song from Spotify or iTunes</a
                >
                for more details
              </p>
              <p>
                If you would like more information please read this blog blost
                <a
                  href="https://music4dance.blog/question-2-what-dance-styles-can-i-dance-to-my-favorite-songs/"
                  >What dance styles can I dance to my favorite song(s)?</a
                >
                and the
                <a href="https://music4dance.blog/music4dance-help/tempo-counter/">documentation</a>
                for the Tempo Counter.
              </p>
            </BCardText>
          </BCardBody>
        </BCollapse>
      </BCard>

      <BCard no-body class="mb-1">
        <BCardHeader header-tag="header" class="p-1" role="tab">
          <BButton v-b-toggle.accordion-3 block variant="primary"
            >Why should I sign up with Music4Dance?</BButton
          >
        </BCardHeader>
        <BCollapse id="accordion-3" accordion="my-accordion" role="tabpanel">
          <BCardBody>
            <BCardText>
              <p>
                <b>It's Free!</b> You'll be able to edit tags on songs and Music4Dance will remember
                the songs you like, edited, or even disliked. You'll alo be able to
                <a href="https://music4dance.blog/music4dance-help/tag-editing/">tag songs</a>,
                <a href="https://music4dance.blog/music4dance-help/dance-tags/"
                  >vote on the dancability of songs</a
                >
                and
                <a href="https://music4dance.blog/music4dance-help/add-songs/">add your own songs</a
                >. <a href="/identity/account/register">Register today!</a> We are always working on
                new features for our site and special features for our registered users.
              </p>
              <p>
                Once you've signed up, please consider
                <a href="https://music4dance.blog/music4dance-help/subscriptions/"
                  >purchasing a premium</a
                >
                subscriptions. This is one of the ways that you can help
                <a href="/home/contribute">support the music4dance project</a>
                (and you never have to look at ads on the site again - well, at least as long as
                your subscription lasts).
              </p>
            </BCardText>
          </BCardBody>
        </BCollapse>
      </BCard>

      <BCard no-body class="mb-1">
        <BCardHeader header-tag="header" class="p-1" role="tab">
          <BButton v-b-toggle.accordion-3b block variant="primary"
            >Why should I purchase a subscription to music4dance.net?</BButton
          >
        </BCardHeader>
        <BCollapse id="accordion-3b" accordion="my-accordion" role="tabpanel">
          <BCardBody>
            <BCardText>
              <p>
                <a href="https://music4dance.blog/music4dance-help/subscriptions/"
                  >Purchasing a premium subscription</a
                >
                is one of the ways that you can help
                <a href="/home/contribute">support the music4dance project.</a> It currently costs
                us more to keep the website running than we collect in advertising, referal and
                subscription fees. So if you enjoy using this site, please consider purchasing a
                <a href="/home/contribute">subscription or donating</a>.
              </p>
              <p>
                There are some additional
                <a href="https://music4dance.blog/music4dance-help/subscriptions/">perks</a> that
                come with a subscription:
              </p>
              <ul>
                <li>Advertisements are removed.</li>
                <li>
                  You will have access to Access to
                  <a href="https://music4dance.blog/music4dance-help/bonus-content/"
                    >bonus content</a
                  >
                  – songs that we weren’t able to match to a publisher or songs that have not yet
                  been tagged with a dance style.
                </li>
                <li>
                  You can
                  <a href="https://music4dance.blog/2019/09/22/create-a-spotify-playlist/"
                    >create a Spotify playlists</a
                  >
                  from search results.
                </li>
              </ul>
            </BCardText>
          </BCardBody>
        </BCollapse>
      </BCard>

      <BCard no-body class="mb-1">
        <BCardHeader header-tag="header" class="p-1" role="tab">
          <BButton v-b-toggle.accordion-4 block variant="primary">
            I can't find the song I'm looking for, what should I do?
          </BButton>
        </BCardHeader>
        <BCollapse id="accordion-4" accordion="my-accordion" role="tabpanel">
          <BCardBody>
            <BCardText>
              <p>
                You can go ahead and
                <a href="https://music4dance.blog/music4dance-help/add-songs/">add it</a>
                yourself.
              </p>
              <p>
                If you you're looking for the song because you don't know what to dance to it,
                please contact us at
                <a href="mailto:info@music4dance.net">info@music4dance.net</a>
                or through our
                <a href="https://music4dance.blog/feedback/">feedback form</a>
                and we will add it if we can.
              </p>
            </BCardText>
          </BCardBody>
        </BCollapse>
      </BCard>

      <BCard no-body class="mb-1">
        <BCardHeader header-tag="header" class="p-1" role="tab">
          <BButton v-b-toggle.accordion-5 block variant="primary">
            I'm a competition Ballroom dancer, how can music4dance help me find music?
          </BButton>
        </BCardHeader>
        <BCollapse id="accordion-5" accordion="my-accordion" role="tabpanel">
          <BCardBody>
            <BCardText>
              <p>
                Go to our
                <a href="/dances/ballroom-competition-categories"
                  >Ballroom competition categories</a
                >
                page and see the list of the different competition categories and the dance/song
                requirements for several organizations that publish competition rules.
              </p>
              <p>
                If you would like more information check out our related blog post.
                <a
                  href="https://music4dance.blog/im-a-ballroom-dancer-can-i-find-practice-songs-that-are-at-competition-tempo-revisited/"
                >
                  I'm a competition ballroom dancer, can I find practice songs that are a specific
                  tempo?</a
                >
              </p>
            </BCardText>
          </BCardBody>
        </BCollapse>
      </BCard>

      <BCard no-body class="mb-1">
        <BCardHeader header-tag="header" class="p-1" role="tab">
          <BButton v-b-toggle.accordion-6 block variant="primary">
            I want to find a great song for my first dance at my wedding, where should I look?
          </BButton>
        </BCardHeader>
        <BCollapse id="accordion-6" accordion="my-accordion" role="tabpanel">
          <BCardBody>
            <BCardText>
              <p>
                Check out the
                <a href="/dances/wedding-music">wedding page</a> for information to help you get
                started, or you can go to the
                <a href="/song/advancedsearchform">advanced search</a> page and look for songs that
                are tagged as First Dance and/or Wedding.
              </p>
              <p>
                If you would like more information check out our related blog posts.
                <a
                  href="https://music4dance.blog/wed-like-to-dance-a-real-partner-dance-as-the-first-dance-at-our-wedding-part-i-we-already-chose-our-song/"
                >
                  Wedding Music Part I: Can we dance the Foxtrot to our song?</a
                >
                and
                <a
                  href="https://music4dance.blog/wed-like-to-dance-a-real-partner-dance-as-the-first-dance-at-our-wedding-part-ii-we-already-chose-our-dance/"
                >
                  Wedding Music Part II: We’re learning to Rumba, help us find a good song for our
                  first dance</a
                >
              </p>
            </BCardText>
          </BCardBody>
        </BCollapse>
      </BCard>

      <BCard no-body class="mb-1">
        <BCardHeader header-tag="header" class="p-1" role="tab">
          <BButton v-b-toggle.accordion-7 block variant="primary">
            Not finding the answer to your question or want to give us feedback?
          </BButton>
        </BCardHeader>
        <BCollapse id="accordion-7" accordion="my-accordion" role="tabpanel">
          <BCardBody>
            <BCardText>
              <p>
                If you are not finding the answer to your question here or wish to give us feedback
                on any part of the site please contact us at
                <a href="mailto:info@music4dance.net">info@music4dance.net</a>
                or through our
                <a href="https://music4dance.blog/feedback/">feedback form</a>.
              </p>
            </BCardText>
          </BCardBody>
        </BCollapse>
      </BCard>
    </div>
  </PageFrame>
</template>

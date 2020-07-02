<template>
  <div id="app">
    <h1>Frequently Asked Questions:</h1>
    <div role="tablist">
    <b-card no-body class="mb-1">
      <b-card-header header-tag="header" class="p-1" role="tab">
        <b-button block v-b-toggle.accordion-1 variant="primary">
          Where can I find songs that work with the dance style I'm currently practicing?
        </b-button>
      </b-card-header>
      <b-collapse id="accordion-1" visible accordion="my-accordion" role="tabpanel">
        <b-card-body>
          <b-card-text>
            <p>
              Go to the  <a href="/song/advancedsearchform">Advanced Search</a> available from the "Music"
              menu and include the style of dance you are interested then click search. The result should
              be a list of songs that can be danced to that style.
            </p>
            <p>
              If you would like more information check out our related blog posts.
              <a href="https://music4dance.blog/question-1-im-learning-to-cha-cha-where-is-some-great-music-for-practicing/">
              I’m learning to Cha Cha, where is some great music for practicing?</a> and
              <a href="https://music4dance.blog/i-am-learning-the-foxtrot-where-can-i-find-some-music/">
              I am learning the Foxtrot, where can I find some music?</a>
            </p>
            <p>
              The <a href="https://music4dance.blog/music4dance-help/advanced-search/">Advanced Search Documentation</a>
              describes the different search attributes in more detail.
            </p>
          </b-card-text>
        </b-card-body>
      </b-collapse>
    </b-card>

    <b-card no-body class="mb-1">
      <b-card-header header-tag="header" class="p-1" role="tab">
        <b-button block v-b-toggle.accordion-2 variant="primary">
          What dance styles work with the song I'm listening to or a specific song that I know of?
        </b-button>
      </b-card-header>
      <b-collapse id="accordion-2" accordion="my-accordion" role="tabpanel">
        <b-card-body>
          <b-card-text>
            <p>With the <a href="/home/counter">tempo counter tool</a> you can tap out the beat while listening to a song, and we'll show you a list of styles that can be danced to the beat you just tapped out as well as links to the dance styles' information pages.</p>
            <p>Or if you have a particular song in mind, <a href="/song/advancedsearchform">search</a> for it and if it is in the database look at what styles and tags are associated with it.</p>
            <p>If you would like more information please read this blog blost <a href="https://music4dance.blog/question-2-what-dance-styles-can-i-dance-to-my-favorite-songs/">What dance styles can I dance to my favorite song(s)?</a> and the <a href="https://music4dance.blog/music4dance-help/tempo-counter/">documentation</a> for the Tempo Counter.</p>
          </b-card-text>
        </b-card-body>
      </b-collapse>
    </b-card>

    <b-card no-body class="mb-1">
      <b-card-header header-tag="header" class="p-1" role="tab">
        <b-button block v-b-toggle.accordion-3 variant="primary">Why should I sign up with Music4Dance?</b-button>
      </b-card-header>
      <b-collapse id="accordion-3" accordion="my-accordion" role="tabpanel">
        <b-card-body>
          <b-card-text>
            <p>
              It's Free! You'll be able to edit tags on songs and Music4Dance will remember the songs you like,
              edited, or even disliked. <a href="/identity/account/register">Register today!</a> We are always
              working on new features for our site and special features for our registered users.
            </p>
          </b-card-text>
        </b-card-body>
      </b-collapse>
    </b-card>

    <b-card no-body class="mb-1">
      <b-card-header header-tag="header" class="p-1" role="tab">
        <b-button block v-b-toggle.accordion-4 variant="primary">
          I'm not finding the song I want, what should I do?
        </b-button>
      </b-card-header>
      <b-collapse id="accordion-4" accordion="my-accordion" role="tabpanel">
        <b-card-body>
          <b-card-text>
            <p>
              Contact us at <a href="mailto:info@music4dance.net">info@music4dance.net</a> or through
              our <a href="https://music4dance.blog/feedback/">feedback form</a> and we will try to add it!
              Feel free to give us any other feedback you might have as well.
            </p>
          </b-card-text>
        </b-card-body>
      </b-collapse>
    </b-card>

    <b-card no-body class="mb-1">
      <b-card-header header-tag="header" class="p-1" role="tab">
        <b-button block v-b-toggle.accordion-5 variant="primary">
          I'm a competition Ballroom dancer, how can music4dance help me find music?
        </b-button>
      </b-card-header>
      <b-collapse id="accordion-5" accordion="my-accordion" role="tabpanel">
        <b-card-body>
          <b-card-text>
            <p>
                Go to our <a href="/dances/ballroom-competition-categories">Ballroom competition categories</a>
                page and see the list of the different competition categories and the dance/song requirements
                for several organizations that publish competition rules.
            </p>
            <p>
              If you would like more information check out our related blog post.
              <a href="https://music4dance.blog/im-a-competition-ballroom-dancer-can-i-find-practice-songs-that-are-a-specific-tempo/">
              I'm a competition ballroom dancer, can I find practice songs that are a specific tempo?</a>
            </p>
          </b-card-text>
        </b-card-body>
      </b-collapse>
    </b-card>

    <b-card no-body class="mb-1">
      <b-card-header header-tag="header" class="p-1" role="tab">
        <b-button block v-b-toggle.accordion-6 variant="primary">
          I want to find a great song for my first dance at my wedding, where should I look?
        </b-button>
      </b-card-header>
      <b-collapse id="accordion-6" accordion="my-accordion" role="tabpanel">
        <b-card-body>
          <b-card-text>
          <p>
            Check out the <a href="/dances/wedding-music">wedding page</a> for information to help you get started,
            or you can go to the <a href="/song/advancedsearchform">advanced search</a> page and look for songs
            that are tagged as First Dance and/or Wedding.
          </p>
          <p>
            If you would like more information check out our related blog posts.
            <a href="https://music4dance.blog/wedding-music-part-i-can-we-dance-the-foxtrot-to-our-song/">
            Wedding Music Part I: Can we dance the Foxtrot to our song?</a> and 
            <a href="https://music4dance.blog/wedding-music-part-ii-were-learning-to-rumba-help-us-find-a-good-song-for-our-first-dance/">
            Wedding Music Part II: We’re learning to Rumba, help us find a good song for our first dance</a>
          </p>
          </b-card-text>
        </b-card-body>
      </b-collapse>
    </b-card>

    <b-card no-body class="mb-1">
      <b-card-header header-tag="header" class="p-1" role="tab">
        <b-button block v-b-toggle.accordion-7 variant="primary">
          Not finding the answer to your question or want to give us feedback?
        </b-button>
      </b-card-header>
      <b-collapse id="accordion-7" accordion="my-accordion" role="tabpanel">
        <b-card-body>
          <b-card-text>
            <p>
              If you are not finding the answer to your question here or wish to give us feedback on
              any part of the site please contact us at <a href="mailto:info@music4dance.net">info@music4dance.net</a>
              or through our <a href="https://music4dance.blog/feedback/">feedback form</a>.
            </p>
          </b-card-text>
        </b-card-body>
      </b-collapse>
    </b-card>

  </div>
  </div>
</template>

<script lang="ts">
import { Component, Vue } from 'vue-property-decorator';
import { SongFilter } from '../model/SongFilter';
import { DanceQuery } from '../model/DanceQuery';
import { UserQuery } from '../model/UserQuery';
import { SongSort } from '../model/SongSort';
import { ListOption } from '../model/ListOption';
import { DanceObject } from '../model/DanceStats';
import { Tag } from '../model/Tag';

import DanceSelector from '../components/DanceSelector.vue';
import TagCategorySelector from '../components/TagCategorySelector.vue';
import { TypedJSON } from 'typedjson';

@Component({
  components: {
  },
})
export default class App extends Vue {

}
</script>
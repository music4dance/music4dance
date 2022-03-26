<template>
  <page id="app" title="music4dance background info" :breadcrumbs="breadcrumbs">
    <h2>About music4dance</h2>
    <p>
      <b>music4dance</b> is a project that has been floating around in my head
      in various forms since I first started ballroom dancing - so what
      <i>is</i> music4dance, really?
    </p>
    <ul>
      <li>
        <b>music4dance</b> is meant to help find music to dance to as well has
        help figure out what dances can be danced to a piece of music
      </li>
      <li>
        <b>music4dance</b> intended to help DJs find new music to add to their
        playlists as well as for students and teachers to build playlists to
        practice to
      </li>
      <li>
        <b>music4dance</b> meant to help dancers understand the relationship
        between dance and the music that they dance to
      </li>
      <li>
        <b>music4dance</b> a place for people to find music to dance to for
        special occassions like weddings and holidy parties
      </li>
    </ul>
    <p>
      Even to make a dent in any one of those goals is pretty ambitious. That's
      about all I've done is to make a dent. I certainly haven't solved any of
      these problems to my satisfaction. So most of all music4dance is a work in
      progress and a playground for me to experiment with the intersection of
      three of my lifelong passions - music, dance and programming.
    </p>
    <p>
      And while I'm doing that, I hope that music4dance provides at least a
      partial solution to some of the above for you. I'm sure that's true for
      some and I can say that some of the most interesting features of this site
      have started as questions from the community. So please keep using the
      site and <a href="https://music4dance.blog/feedback/">let me know</a> what
      you think.
    </p>
    <p class="neutral">
      Please read my <a href="https://www.music4dance.blog">blog</a> for more
      about the history and motivation behind
      <a href="https://www.music4dance.net">music4dance</a>.
    </p>
    <h2>About me</h2>
    <p>
      I’m an amateur dancer and with some training in music who also happens to
      be a professional software engineer. I love dancing, and I particularly
      love dancing when I enjoy the music that I’m dancing to.
    </p>
    <p>
      My dance background is primarily in Ballroom. I’ve been an amateur
      competitor in the
      <a href="https://www.music4dance.net/dances/american-rhythm"
        >American Rhythm style</a
      >). Recently I've moved more towards social partner dancing. As I was
      starting to work on music4dance in earnest, I dove into tap.
    </p>
    <p>
      Most of my programming time outside of my main day jobs has been related
      to music.
    </p>
    <ul>
      <li>
        When the Macintosh came out, my first thought was - Music Editor. And I
        wrote one - although I never got it to the point where I could sell it.
      </li>
      <li>
        My work study project in college was writing music theory software for
        the Macintosh
      </li>
      <li>
        When I left Corporate America to try my hand at the startup world I
        joined a company that was digitizing sheet music and did the initial
        work to take that online.
      </li>
      <li>
        When I left Corporate America for the second time to rediscover my love
        of programming (and generally find myself) I founded music4dance
      </li>
    </ul>
    <h2>My Resume</h2>
    <p>
      <a href="/Content/DavidGray-Resume.pdf">Printable Version</a>
    </p>
    <p>
      <a href="/Home/Resume">Web Version</a>
    </p>
  </page>
</template>

<script lang="ts">
import Page from "@/components/Page.vue";
import { BreadCrumbItem, infoTrail } from "@/model/BreadCrumbItem";
import { Component, Vue } from "vue-property-decorator";

@Component({
  components: {
    Page,
  },
})
export default class App extends Vue {
  private breadcrumbs: BreadCrumbItem[] = [
    ...infoTrail,
    { text: "About", active: true },
  ];
}
</script>

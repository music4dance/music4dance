<template>
  <page id="app" title="About music4dance" :breadcrumbs="breadcrumbs">
    <p class="neutral">
      I’m an amateur dancer and with some training in music who also happens to
      be a professional software engineer. I love dancing, and I particularly
      love dancing when I enjoy the music that I’m dancing to. I’m hoping to
      build some community around this idea since music and dance are both so
      personal that multiple viewpoints early will be essential to any kind of
      success in this area.
    </p>
    <p class="neutral">
      My dance background is primarily in Ballroom (I’ve been an amateur
      competitor in the
      <a href="https://www.music4dance.net/dances/american-rhythm"
        >American Rhythm style</a
      >), has moved more towards social partner dancing and I have just taken an
      interest in tap. So I’m going to start from what I know, but part of the
      point of building some community is to enlist experts in kinds of dance
      that I don’t know (or know as well) to help me broaden my scope.
    </p>
    <p class="neutral">
      The core of my initial idea is that I would like to be able to generate a
      playlist of songs to dance a slow
      <a href="https://www.music4dance.net/dances/salsa">Salsa</a> or fast
      <a href="https://www.music4dance.net/dances/west-coast-swing"
        >West Coast Swing</a
      >
      or a
      <a href="https://www.music4dance.net/dances/viennese-waltz"
        >Viennese Waltz</a
      >
      to. I’d like to do this against my own library or against one of a
      multitude of streaming and mp3 purchase services. The first baby step for
      that is the songs section of this website, where I’ve pulled together a
      bunch of that information from a number of different sources, merged and
      edited, so that I’ve got at least a start of a database of song
      title/artist/albums associated with various
      <a href="https://www.music4dance.net/dances"></a>dances. In the long run I
      would like this to be both crowd sourced and linked to actual music.
    </p>
    <p class="neutral">
      Please read my <a href="https://www.music4dance.blog">blog</a> for more
      about the history and motivation behind
      <a href="https://www.music4dance.net">music4dance</a>.
    </p>
    <p class="neutral">
      <a href="/Content/DavidGray-Resume.pdf">David's Resume</a>
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

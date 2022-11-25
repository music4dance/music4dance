<template>
  <page id="app" title="Wedding Dance Music" :breadcrumbs="breadcrumbs">
    <p>
      Are you getting married and trying to find the perfect song for your
      <a href="/Song/Index/?filter=Index-.-Title-.-.-.-.-.-1-+First Dance:Other"
        >first dance</a
      >? Or perhaps you're trying to find the perfect dance style for your
      favorite song? We can help with both of these as well as helping you find
      music for
      <a
        href="/Song/Index/?filter=Index-.-Title-.-.-.-.-.-1-+Father Daughter:Other"
        >Father/Daughter</a
      >
      or
      <a href="/Song/Index/?filter=Index-.-Title-.-.-.-.-.-1-+Mother Son:Other"
        >Mother/Son</a
      >
      dances.
    </p>
    <p>
      If you know the song and you would like help figuring out what dance style
      to dance to it, try looking it up in our
      <a href="/Song/Index/">music catalog</a>. More details on how to do that
      are available on this
      <a
        href="https://music4dance.blog/wedding-music-part-i-can-we-dance-the-foxtrot-to-our-song/"
        >blog post</a
      >. If we haven't cataloged your song yet,
      <a href="https://music4dance.blog/feedback/">let us know</a>
      and we'll happily add it.
    </p>
    <p>
      If you know what dance style you would like to dance, but haven't found
      the perfect song yet, you can do that kind of search as well.
      <a
        href="https://music4dance.blog/wedding-music-part-ii-were-learning-to-rumba-help-us-find-a-good-song-for-our-first-dance/"
      >
        Read the post</a
      >
      on how to do that, or just click on one of the quick links below. Each
      number links to a list of songs that have can be danced to the style
      specified by the row and have also been tagged with the text in the column
      heading.
    </p>
    <p>
      If you're looking for a slow dance for your wedding, try browsing the
      <a href="/Song/Index/?filter=Index-CFT-Dances-.-.-.-.-.-1-+Wedding:Other"
        >Castle Foxtrot</a
      >
      and
      <a href="/Song/Index/?filter=Index-SFT-Dances-.-.-.-.-.-1-+Wedding:Other"
        >Slow Foxtrot</a
      >
      lists, they are full of songs with a good slow and steady beat to dance
      either a simplified Foxtrot or traditional slow dance.
    </p>
    <p>
      Don't forget to <a href="/identity/account/register">register</a> (it's
      free) so you can
      <a href="https://music4dance.blog/lets-tag-some-songs/"
        >tag the songs you like</a
      >
      and have us remember your favorites while you're playing with options.
    </p>

    <div style="margin: 0 auto; max-width: 800px">
      <tag-matrix-table :matrix="model" style="idt"></tag-matrix-table>
    </div>

    <dl><blog-tag-link title="Wedding Music" tag="wedding"></blog-tag-link></dl>
  </page>
</template>

<script lang="ts">
import BlogTagLink from "@/components/BlogTagLink.vue";
import Page from "@/components/Page.vue";
import TagMatrixTable from "@/components/TagMatrixTable.vue";
import { BreadCrumbItem, danceTrail } from "@/model/BreadCrumbItem";
import { TagMatrix } from "@/model/TagMatrix";
import { TypedJSON } from "typedjson";
import Vue from "vue";

declare const model: string;

export default Vue.extend({
  components: {
    BlogTagLink,
    Page,
    TagMatrixTable,
  },
  props: {},
  data() {
    return new (class {
      model: TagMatrix = TypedJSON.parse(model, TagMatrix)!;
      breadcrumbs: BreadCrumbItem[] = [
        ...danceTrail,
        { text: "WeddingDanceMusic", active: true },
      ];
    })();
  },
  computed: {},
});
</script>

<template>
  <page id="app">
    <b-card-group deck>
      <info-card :card="funCard">
        <div style="padding: 0.25em 1em">
          <div style="font-size: 1.5em">
            <a href="/home/contribute"
              ><b-icon-music-note-list></b-icon-music-note-list> Follow
              music4dance on Spotify</a
            >
          </div>
          <iframe
            src="https://open.spotify.com/follow/1/?uri=spotify:user:ebo1rk39vp51kkyjps45eobph&size=basic&theme=light&show-count=0"
            width="200"
            height="25"
            scrolling="no"
            frameborder="0"
            style="border: none; overflow: hidden; padding-left: 1em"
            allowtransparency="true"
          ></iframe>
        </div>
        <div style="padding: 0.25em 1em">
          <div style="font-size: 1.5em">
            <a href="/home/contribute"
              ><b-icon-music-note-list></b-icon-music-note-list> Like
              music4dance on Facebook</a
            >
          </div>
          <div
            class="fb-like"
            data-href="https://www.facebook.com/music4dance.net"
            data-width=""
            data-layout="standard"
            data-action="like"
            data-size="small"
            data-share="true"
            style="padding-left: 1em"
          ></div>
        </div>
      </info-card>
      <info-card
        v-for="card in cards"
        :key="card.title.link"
        :card="card"
      ></info-card>
    </b-card-group>
    <b-row>
      <area-icon
        v-for="area in areas"
        :key="area.name"
        :area="area"
      ></area-icon>
    </b-row>
    <b-row>
      <home-section
        name="Music"
        category="music"
        :features="musicFeatures"
        class="col-xl"
      ></home-section>
      <home-section
        name="Tools"
        category="tools"
        :features="toolFeatures"
        class="col-xl"
      ></home-section>
      <home-section name="Info" category="info" class="col-xl">
        <blog-feature-link
          v-for="entry in model.blogEntries"
          :key="entry.title"
          :entry="entry"
        ></blog-feature-link>
      </home-section>
    </b-row>
  </page>
</template>

<script lang="ts">
// tslint:disable: max-classes-per-file
import "reflect-metadata";
import { Component, Vue } from "vue-property-decorator";
import Page from "@/components/Page.vue";
import BlogFeatureLink from "@/components/BlogFeatureLink.vue";
import HomeSection from "./HomeSection.vue";
import InfoCard, { CardInfo } from "./InfoCard.vue";
import AreaIcon from "./AreaIcon.vue";
import { jsonObject, TypedJSON, jsonArrayMember } from "typedjson";
import { SiteMapEntry } from "@/model/SiteMapInfo";
import { DanceClass, DanceMapping } from "./DanceClass";
import { Link } from "@/model/Link";
import { AreaInfo } from "./AreaInfo";
import { FeatureInfo } from "@/model/FeatureInfo";

@jsonObject
class HomeModel {
  @jsonArrayMember(SiteMapEntry) public blogEntries!: SiteMapEntry[];
  @jsonArrayMember(DanceClass) public dances!: DanceClass[];
}

declare const model: string;

@Component({
  components: {
    AreaIcon,
    BlogFeatureLink,
    HomeSection,
    InfoCard,
    Page,
  },
})
export default class App extends Vue {
  private model: HomeModel;

  private readonly areas: AreaInfo[] = [
    { name: "Contribute", link: "https://www.music4dance.net/home/contribute" },
    { name: "Music" },
    { name: "Tools" },
    { name: "Info" },
  ];

  private readonly musicFeatures: FeatureInfo[] = [
    {
      title: "Find songs to dance a specific dance style.",
      type: "music",
      tryIt: "/dance",
      docs: "/dance-styles-help/",
      posts:
        "/question-1-im-learning-to-cha-cha-where-is-some-great-music-for-practicing/",
    },
    {
      title: "Find the most recently added songs",
      type: "music",
      tryIt: "/song/newmusic?type=Created",
      posts: "/tag/new-music/",
    },
    {
      title: "Find out what dance styles can be danced to a song.",
      type: "music",
      tryIt: "/song",
      docs: "/song-list/",
      posts: "/wedding-music-part-i-can-we-dance-the-foxtrot-to-our-song/",
    },
    {
      title:
        "Start with a musical genre (or other tags) and find songs to dance a particular style.",
      type: "music",
      tryIt: "/tag",
      docs: "/tag-cloud/",
      posts: "/tag/tag/",
    },
    {
      title: "Find wedding songs and match them with dance styles.",
      type: "music",
      tryIt: "/dances/wedding-music",
      posts: "/tag/wedding/",
    },
    {
      title: "Find songs that match competition tempo ranges.",
      type: "music",
      tryIt: "/dances/ballroom-competition-categories",
      docs: "/dance-category/",
      posts:
        "/im-a-competition-ballroom-dancer-can-i-find-practice-songs-that-are-a-specific-tempo/",
    },
    {
      title: "Vote on what style you would dance to any song in our catalog.",
      type: "music",
      tryIt: "/identity/account/register",
      docs: "/dance-tags/",
      posts: "/tag/vote/",
    },
    {
      title: "Tag songs to create your own ways of making song lists.",
      type: "music",
      tryIt: "/identity/account/register",
      docs: "/tag-editing/",
      posts: "lets-tag-some-songs/",
    },
    {
      title: "Find holiday music to dance to.",
      type: "music",
      tryIt: "/dances/holiday-music",
      posts: "/tag/holiday/",
    },
  ];

  private readonly toolFeatures: FeatureInfo[] = [
    {
      title:
        "Use our tempo counter tool to find the tempo of a song and match it to dance styles.",
      type: "tools",
      tryIt: "/home/counter",
      docs: "/tempo-counter/",
      posts: "/question-2-what-dance-styles-can-i-dance-to-my-favorite-songs/",
    },
    {
      title:
        "Explore relationships between the meter and tempi of different dance styles and find songs that match.",
      type: "tools",
      tryIt: "/home/tempi",
      docs: "/dance-tempi/",
      posts: "/tag/tempo/",
    },
    {
      title:
        "Use our advanced search tool to find songs to dance to based one tempo, tags, dance style, and more.",
      type: "tools",
      tryIt: "/song/advancedsearchform",
      docs: "/advanced-search/",
      posts: "/tag/search/",
    },
  ];

  constructor() {
    super();

    this.model = TypedJSON.parse(model, HomeModel)!;
  }

  private get cards(): CardInfo[] {
    return this.model.dances.map((d) => ({
      title: { text: d.fullTitle, link: `/dances/${d.topDance}` },
      image: d.image,
      items: d.dances.map((dm) => ({
        text: dm.title,
        link: this.danceLink(dm),
      })),
      blog: d.title.toLowerCase(),
    }));
  }

  private get funCard(): CardInfo {
    return {
      title: {
        text: "Fun with Music for All Kinds of Dancers",
        link: "https://music4dance.blog/",
      },
      image: "fun",
      items: this.headLinks,
    };
  }

  private get headLinks(): Link[] {
    const blogEntries = this.model.blogEntries;
    const headLinks: Link[] = [blogEntries[blogEntries.length - 1].link];

    const used = new Set<number>();
    while (headLinks.length < 4) {
      const idx = Math.floor(Math.random() * (blogEntries.length - 2));
      if (used.has(idx)) {
        continue;
      }
      headLinks.push(blogEntries[idx].link);
    }

    headLinks.push({
      text: "Check out our Tempo Counter Tool",
      link: "/home/counter",
    });

    headLinks.push({
      text: "Check out our Tempo Matrix Tool",
      link: "/home/tempi",
    });

    return headLinks;
  }

  private danceLink(dm: DanceMapping): string {
    return `/${dm.controller}/${dm.name}`;
  }
}
</script>

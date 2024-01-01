<script setup lang="ts">
import BlogFeatureLink from "@/components/BlogFeatureLink.vue";
import PageFrame from "@/components/PageFrame.vue";
import { type FeatureInfo } from "@/models/FeatureInfo";
import { type Link } from "@/models/Link";
import { SiteMapEntry } from "@/models/SiteMapInfo";
import { random, seed } from "@/helpers/Random";
import { jsonArrayMember, jsonObject, TypedJSON } from "typedjson";
import AreaIcon from "./components/AreaIcon.vue";
import { type AreaInfo } from "./AreaInfo";
import { DanceClass, DanceMapping } from "./DanceClass";
import HomeSection from "./components/HomeSection.vue";
import InfoCard, { type CardInfo } from "./components/InfoCard.vue";

declare global {
  interface Window {
    seedNumber: number;
  }
}

@jsonObject
class HomeModel {
  @jsonArrayMember(SiteMapEntry) public blogEntries!: SiteMapEntry[];
  @jsonArrayMember(DanceClass) public dances!: DanceClass[];
}

declare const model_: string;

const model: HomeModel = TypedJSON.parse(model_, HomeModel)!;

const areas: AreaInfo[] = [
  {
    name: "Contribute",
    link: "https://www.music4dance.net/home/contribute",
  },
  { name: "Music" },
  { name: "Tools" },
  { name: "Info" },
];

const musicFeatures: FeatureInfo[] = [
  {
    title: "Find songs to dance a specific dance style.",
    type: "music",
    tryIt: "/dance",
    docs: "/dance-styles-help/",
    posts: "/question-1-im-learning-to-cha-cha-where-is-some-great-music-for-practicing/",
    menu: ["Music", "Dances"],
  },
  {
    title: "Find the most recently added songs",
    type: "music",
    tryIt: "/song/newmusic?type=Created",
    posts: "/tag/new-music/",
    menu: ["Music", "Song Library", "New Music"],
  },
  {
    title: "Find out what dance styles can be danced to a song.",
    type: "music",
    tryIt: "/song",
    docs: "/song-list/",
    posts: "/wedding-music-part-i-can-we-dance-the-foxtrot-to-our-song/",
    menu: ["Music", "Song Library"],
  },
  {
    title: "Start with a musical genre (or other tags) and find songs to dance a particular style.",
    type: "music",
    tryIt: "/tag",
    docs: "/tag-cloud/",
    posts: "/tag/tag/",
    menu: ["Music", "Tags"],
  },
  {
    title: "Find wedding songs and match them with dance styles.",
    type: "music",
    tryIt: "/dances/wedding-music",
    posts: "/tag/wedding/",
    menu: ["Music", "Wedding"],
  },
  {
    title: "Find songs that match competition tempo ranges.",
    type: "music",
    tryIt: "/dances/ballroom-competition-categories",
    docs: "/dance-category/",
    posts: "/im-a-competition-ballroom-dancer-can-i-find-practice-songs-that-are-a-specific-tempo/",
    menu: ["Music", "Dances", "Ballroom"],
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
    posts: "/lets-tag-some-songs/",
  },
  {
    title: "Find holiday music to dance to.",
    type: "music",
    tryIt: "/song/holidaymusic",
    posts: "/tag/holiday/",
    menu: ["Music", "Holiday"],
  },
];

const toolFeatures: FeatureInfo[] = [
  {
    title: "Use our tempo counter tool to find the tempo of a song and match it to dance styles.",
    type: "tools",
    tryIt: "/home/counter",
    docs: "/tempo-counter/",
    posts: "/question-2-what-dance-styles-can-i-dance-to-my-favorite-songs/",
    menu: ["Tools", "Tempo Counter"],
  },
  {
    title:
      "Explore relationships between the meter and tempi of different dance styles and find songs that match.",
    type: "tools",
    tryIt: "/home/tempi",
    docs: "/dance-tempi/",
    posts: "/tag/tempo/",
    menu: ["Tools", "Tempi (Tempos)"],
  },
  {
    title:
      "Use our advanced search tool to find songs to dance to based one tempo, tags, dance style, and more.",
    type: "tools",
    tryIt: "/song/advancedsearchform",
    docs: "/advanced-search/",
    posts: "/tag/search/",
    menu: ["Music", "Song Library", "Advanced Search"],
  },
];

const danceLink = function (dm: DanceMapping): string {
  return dm.link;
};

const cards = model.dances.map((d) => ({
  title: { text: d.fullTitle, link: `/dances/${d.topDance}` },
  image: d.image,
  items: d.dances.map((dm) => ({
    text: dm.title,
    link: danceLink(dm),
  })),
  blog: d.title.toLowerCase(),
}));

const blogEntries: SiteMapEntry[] = model.blogEntries.flatMap((entry) => entry.children ?? []);

const newestEntry: SiteMapEntry = blogEntries.reduce((acc, curr) =>
  (acc.order ?? 0) > (curr.order ?? 0) ? acc : curr,
);

const headLinks: Link[] = computeHeadLinks();

const funCard: CardInfo = {
  title: {
    text: "Fun with Music for All Kinds of Dancers",
    link: "https://music4dance.blog/",
  },
  image: "fun",
  items: headLinks,
};

function computeHeadLinks(): Link[] {
  const headLinks: Link[] = [newestEntry.link];

  const used = new Set<number>();
  seed(window.seedNumber ?? Date.now());
  while (headLinks.length < 4) {
    const idx = Math.floor(random() * (blogEntries.length - 2));
    const entry = blogEntries[idx];
    if (entry.oneTime || used.has(idx)) {
      continue;
    }
    used.add(idx);
    headLinks.push(entry.link);
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
</script>

<template>
  <PageFrame id="home">
    <BCardGroup deck>
      <InfoCard id="fun" :card="funCard">
        <div style="padding: 0.25em 1em">
          <div style="font-size: 1.5em">
            <a href="https://open.spotify.com/user/ebo1rk39vp51kkyjps45eobph?si=31df6fc12cf04a64"
              ><IBiMusicNoteList></IBiMusicNoteList> Follow music4dance on Spotify</a
            >
          </div>
        </div>
        <div style="padding: 0.25em 1em">
          <div style="font-size: 1.5em">
            <a href="/home/contribute"
              ><IBiMusicNoteList></IBiMusicNoteList> Like music4dance on Facebook</a
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
      </InfoCard>
      <InfoCard v-for="card in cards" :key="card.title.link" :card="card"></InfoCard>
    </BCardGroup>
    <BRow>
      <AreaIcon v-for="area in areas" :key="area.name" :area="area"></AreaIcon>
    </BRow>
    <BRow>
      <HomeSection
        name="Music"
        category="music"
        :features="musicFeatures"
        class="col-xl"
      ></HomeSection>
      <HomeSection
        name="Tools"
        category="tools"
        :features="toolFeatures"
        class="col-xl"
      ></HomeSection>
      <HomeSection name="Info" category="info" class="col-xl">
        <BlogFeatureLink
          v-for="entry in model.blogEntries"
          :key="entry.title"
          :entry="entry"
        ></BlogFeatureLink>
      </HomeSection>
    </BRow>
  </PageFrame>
</template>

<template>
  <b-card
    no-body
    border-variant="primary"
    style="margin-bottom: 1rem; min-width: 25rem"
  >
    <b-card-header header-bg-variant="primary">
      <info-link
        :link="card.title.link"
        :text="card.title.text"
        :styles="{ color: 'white', 'font-size': '1.5rem' }"
      >
      </info-link>
    </b-card-header>

    <div :style="{ 'background-image': image }" class="home-card-background">
      <ul class="home-list" style="padding-left: 3rem; text-indent: -2rem">
        <li v-for="item in card.items" :key="item.text">
          <info-link :link="item.link">
            <b-icon-music-note-list></b-icon-music-note-list> {{ item.text }}
          </info-link>
        </li>
      </ul>
      <blog-tag-link
        v-if="card.blog"
        :tag="card.blog"
        style="margin-left: 2rem"
      ></blog-tag-link>
      <slot></slot>
    </div>
  </b-card>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";
import InfoLink from "./InfoLink.vue";
import BlogTagLink from "@/components/BlogTagLink.vue";
import { Link } from "@/model/Link";
import DanceItem from "./DanceItem.vue";

export interface CardInfo {
  title: Link;
  image: string;
  items: Link[];
  blog?: string;
}

@Component({
  components: {
    BlogTagLink,
    InfoLink,
  },
})
export default class InfoCard extends Vue {
  @Prop() private card!: CardInfo;

  private get image(): string {
    return `url(/images/shadows/${this.card.image}-bkg.png)`;
  }
}
</script>

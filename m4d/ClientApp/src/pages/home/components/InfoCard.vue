<script setup lang="ts">
import BlogTagLink from "@/components/BlogTagLink.vue";
import { type Link } from "@/models/Link";
import InfoLink from "./InfoLink.vue";

export interface CardInfo {
  title: Link;
  image: string;
  items: Link[];
  blog?: string;
}

const props = defineProps<{
  card: CardInfo;
  id?: string;
}>();

const image: string = `url(/images/shadows/${props.card.image}-bkg.png)`;
const computedId = function (): string {
  const id = props.id ?? props.card.blog;
  return `${id}-info`;
};
</script>

<template>
  <BCard
    :id="computedId()"
    no-body
    border-variant="primary"
    style="margin-bottom: 1rem; min-width: 25rem"
  >
    <BCardHeader variant="primary">
      <InfoLink
        :link="card.title.link"
        :text="card.title.text"
        :styles="{ color: 'white', fontSize: '1.5rem' }"
      >
      </InfoLink>
    </BCardHeader>

    <div :style="{ 'background-image': image }" class="home-card-background">
      <ul class="home-list" style="padding-left: 3rem; text-indent: -2rem">
        <li v-for="item in card.items" :key="item.text">
          <InfoLink :link="item.link">
            <IBiMusicNoteList></IBiMusicNoteList> {{ item.text }}
          </InfoLink>
        </li>
      </ul>
      <BlogTagLink v-if="card.blog" :tag="card.blog" style="margin-left: 2rem"></BlogTagLink>
      <slot></slot>
    </div>
  </BCard>
</template>

<style scoped>
.home-list {
  list-style-type: none;
  font-size: 1.5rem;
}

.home-card-background {
  min-height: 30rem;
  background-size: 25rem 30rem;
  background-repeat: no-repeat;
  background-position: center;
}
</style>

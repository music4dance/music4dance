<template>
  <div>
    <div>
      <img :src="icon" :alt="this.info.type" width="24" height="24" />
      {{ info.title }}
    </div>
    <div class="ml-3">
      <feature-button
        title="Try It!"
        type="play"
        :variant="info.type"
        :link="info.tryIt"
        style="margin-bottom: 0.25em"
      ></feature-button>
      <feature-button
        title="Documentation"
        type="docs"
        :variant="info.type"
        :link="info.docs"
        style="margin-bottom: 0.25em"
      ></feature-button>
      <feature-button
        title="Blog Posts"
        type="blog"
        :variant="info.type"
        :link="info.posts"
        style="margin-bottom: 0.25em"
      ></feature-button>
    </div>
    <div v-if="info.menu" class="ml-5">
      Menu Location:
      <span v-for="item in pathItems" :key="item">{{ item }} / </span>
      <a :href="info.tryIt">{{ actionItem }}</a>
    </div>
  </div>
</template>

<script lang="ts">
import type { FeatureInfo } from "@/model/FeatureInfo";
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import FeatureButton from "./FeatureButton.vue";

@Component({
  components: {
    FeatureButton,
  },
})
export default class FeatureLink extends Vue {
  @Prop() private readonly info!: FeatureInfo;

  private get icon(): string {
    return `/images/icons/${this.info.type}.png`;
  }

  private get pathItems(): string[] {
    const menu = this.info.menu;
    return menu ? menu.filter((s, i) => i != menu.length - 1) : [];
  }

  private get actionItem(): string {
    const menu = this.info.menu;
    return menu ? menu[menu.length - 1] : "";
  }
}
</script>

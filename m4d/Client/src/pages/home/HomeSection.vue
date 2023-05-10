<template>
  <div :id="name" style="margin-top: 1rem">
    <div class="row col" style="margin-bottom: 1rem">
      <img :src="image" :alt="name" width="48" height="48" />
      <h2
        :class="classes"
        style="padding-left: 0.5rem; display: inline; vertical-align: center"
      >
        {{ name }}
      </h2>
    </div>
    <slot>
      <div
        v-if="features"
        class="row col"
        style="display: block; margin-left: 1rem"
      >
        <feature-link
          v-for="feature in features"
          :key="feature.title"
          :info="feature"
        ></feature-link>
      </div>
    </slot>
  </div>
</template>

<script lang="ts">
import FeatureLink from "@/components/FeatureLink.vue";
import { FeatureInfo } from "@/model/FeatureInfo";
import { Link } from "@/model/Link";
import Vue, { PropType } from "vue";

export interface CardInfo {
  title: Link;
  image: string;
  items: Link[];
}

export default Vue.extend({
  components: {
    FeatureLink,
  },
  props: {
    name: {
      type: String,
      required: true,
    },
    category: {
      type: String,
      required: true,
    },
    features: {
      type: [] as PropType<FeatureInfo[]>,
    },
  },
  computed: {
    classes(): string[] {
      return [this.category];
    },
    image(): string {
      return `/images/icons/${this.category}.png`;
    },
  },
});
</script>

<template>
  <like-button
    :state="state"
    :authenticated="!!user"
    :title="song.title"
    :scale="scale"
    :toggleBehavior="toggleBehavior"
    @click-like="onClick"
  >
  </like-button>
</template>

<script lang="ts">
import LikeButton from "@/components/LikeButton.vue";
import { Song } from "@/model/Song";
import Vue, { PropType } from "vue";

export default Vue.extend({
  components: { LikeButton },
  props: {
    song: { type: Object as PropType<Song>, required: true },
    user: String,
    scale: Number,
    toggleBehavior: Boolean,
  },
  computed: {
    state(): boolean | undefined {
      const user = this.user;
      if (!user) {
        return undefined;
      }
      const modified = this.song.getUserModified(user);
      return modified ? modified.like : undefined;
    },

    redirect(): string {
      const location = window.location;
      return `${location.pathname}${location.search}${location.hash}`;
    },
  },
  methods: {
    onClick(): void {
      if (this.user) {
        this.$emit("click-like", this.song.id);
      } else {
        window.location.href = `/identity/account/login?returnUrl=${this.redirect}`;
      }
    },
  },
});
</script>

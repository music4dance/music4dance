<template>
  <div>
    <p v-if="empty">
      We haven't cataloged any <a :href="danceLink">{{ danceName }}</a> holiday
      music yet.
    </p>
    <p v-else>
      Not seeing as much <a :href="danceLink">{{ danceName }}</a> holiday music
      as you would like?
    </p>
    <p>
      If this is a dance that you're familiar with, here are a couple of ways
      that you can help.
    </p>
    <ul>
      <li>
        Browse our catalog of <a :href="danceLink">{{ danceName }}</a> music and
        <a href="https://music4dance.blog/music4dance-help/tag-editing/">tag</a>
        songs as Holiday when you find them.
      </li>
      <li>
        If you have a list of holiday {{ danceName }} that you are willing to
        share, please send us email at
        <a href="mailto:info@music4dance.net">info@music4dance.net</a> or
        contact us through our
        <a href="https://music4dance.blog/feedback/">feedback form.</a>
      </li>
      <li>
        Sign up for our <a href="/song/augment">add song beta</a> and add
        holiday songs.
      </li>
      <li>
        Or check out other ways to <a href="/home/contribute">contribute</a> to
        music4dance
      </li>
    </ul>
  </div>
</template>

<script lang="ts">
import "reflect-metadata";
import { Component, Prop, Vue } from "vue-property-decorator";
import { DanceEnvironment } from "@/model/DanceEnvironmet";
import { toTitleCase, wordsToKebab } from "@/helpers/StringHelpers";

declare const environment: DanceEnvironment;

@Component
export default class HolidayHelp extends Vue {
  @Prop() private readonly dance!: string;
  @Prop() private readonly empty?: boolean;

  private get danceName(): string {
    return toTitleCase(this.dance);
  }

  private get danceLink(): string {
    return `/dances/${wordsToKebab(this.dance)}`;
  }
}
</script>

<style scoped lang="scss"></style>

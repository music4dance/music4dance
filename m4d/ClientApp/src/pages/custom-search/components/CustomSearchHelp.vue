<script setup lang="ts">
import { toTitleCase, wordsToKebab } from "@/helpers/StringHelpers";

const props = defineProps<{ name: string; dance: string; empty?: boolean }>();

const danceName = toTitleCase(props.dance);
const danceLink = `/dances/${wordsToKebab(props.dance)}`;
</script>

<template>
  <div>
    <p v-if="empty">
      We haven't cataloged any <a :href="danceLink">{{ danceName }}</a> {{ name }} music yet.
    </p>
    <p v-else>
      Not seeing as much <a :href="danceLink">{{ danceName }}</a> {{ name }} music as you would
      like?
    </p>
    <p>
      If this is a dance that you're familiar with, here are a couple of ways that you can help.
    </p>
    <ul>
      <li>
        Browse our catalog of <a :href="danceLink">{{ danceName }}</a> music and
        <a href="https://music4dance.blog/music4dance-help/tag-editing/">tag</a>
        songs as <em>{{ name }}</em> when you find them.
      </li>
      <li>
        If you have a list of {{ name }} {{ danceName }} that you are willing to share, please send
        us email at <a href="mailto:info@music4dance.net">info@music4dance.net</a> or contact us
        through our
        <a href="https://music4dance.blog/feedback/">feedback form.</a>
      </li>
      <li>Sign up for our <a href="/song/augment">add song beta</a> and add {{ name }} songs.</li>
      <li>Or check out other ways to <a href="/home/contribute">contribute</a> to music4dance</li>
    </ul>
  </div>
</template>

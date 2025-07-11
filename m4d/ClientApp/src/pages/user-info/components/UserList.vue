<script setup lang="ts">
import { ProfileModel } from "@/models/ProfileModel";
import { computed } from "vue";

const props = defineProps<{ model: ProfileModel }>();

const hasSongs = computed(() => {
  return !!(props.model?.favoriteCount ?? props.model?.editCount ?? props.model?.blockedCount);
});

const allLinks = computed(() => {
  const info = props.model;

  return [
    {
      text: "Songs in {{ userName }}'s favorites.",
      type: "l",
      include: true,
      count: info.favoriteCount,
    },
    {
      text: "Songs that {{ userName }} has edited.",
      type: "a",
      include: true,
      count: info.editCount,
    },
    {
      text: "Songs in {{ userName }}'s blocked list.",
      type: "h",
      include: true,
      count: info.blockedCount,
    },
    {
      text: "Songs <em>not</em> in {{ userName }}'s favorites.",
      type: "l",
      include: false,
      count: info.favoriteCount,
    },
    {
      text: "Songs that {{ userName }} has <em>not</em> edited.",
      type: "a",
      include: false,
      count: info.editCount,
    },
    {
      text: "Songs <em>not</em> in {{ userName }}'s blocked list.",
      type: "h",
      include: false,
      count: info.blockedCount,
    },
  ];
});

const links = computed(() => {
  return allLinks.value.filter((l) => l.count);
});
</script>

<template>
  <div>
    <h2>{{ model.displayName }}'s Song Lists</h2>
    <ul>
      <UserInfoLink
        v-for="(link, index) in links"
        :key="index"
        :user-name="model.userName"
        :display-name="model.displayName"
        :text="link.text"
        :type="link.type"
        :include="link.include"
        :count="link.count!"
      />
    </ul>
    <div>
      <p v-if="!hasSongs">
        {{ model.displayName }} in order to add songs to your lists, either
        <BLink href="https://music4dance.blog/music4dance-help/dance-tags/"
          >vote on songs to dance to</BLink
        >
        or
        <BLink
          href="https://music4dance.blog/what-is-the-difference-between-adding-a-song-to-favorites-and-voting-on-a-songs-danceability/"
          >add song to your favorites or blocked lists</BLink
        >.
      </p>
      <p>
        The User Song List feature is still under development. We are considering including lists of
        dance styles that the user has tagged or possibly including lists of arbitrary tags. Please
        <a href="https://music4dance.blog/feedback/">let us know</a> if you like this feature or
        have ideas to improve it (or both).
      </p>
      <p v-if="model.isAnonymous">
        The association of username with dances and tags that the user has voted on is fundamental
        to how music4dance is set up, but we also very much want to respect our member's privacy. So
        we're obscuring user names for member who chose to remain anonymous. Since full privacy was
        the default setting for members who signed up for most of the existence of the site there
        are most likely many members who just left this as the default. If you (or a friend) are
        such a person, please consider going to your
        <a href="/identity/account/manage">profile</a> and changing your privace setting to allow
        other users to see your information. The only information that we currently share is your
        username in the context of lists like this and in the song details where we show the changes
        that each user makes to a song. If we gather additional information in the future you are
        welcome to not participate or change your privacy settings at that time.
        <a href="https://music4dance.blog/feedback/">contact us</a> with your prefered user name and
        we'll make the change.
      </p>
    </div>
  </div>
</template>

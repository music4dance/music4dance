<script setup lang="ts">
import { ProfileModel } from "@/models/ProfileModel";
import { computed } from "vue";
import type { MenuContext } from "@/models/MenuContext";

const props = defineProps<{ model: ProfileModel; menuContext: MenuContext }>();

const spotifyProfile = computed(() => {
  return `https://open.spotify.com/user/${props.model.spotifyId}`;
});

const isCurrentUser = computed(() => {
  return props.model.userName === props.model.userName;
});
</script>

<template>
  <div>
    <div v-if="model.spotifyId" class="fs-5">
      This is a proxy user based on a public spotify profile. View
      {{ model.displayName }}'s spotify profile
      <a :href="spotifyProfile" target="_blank"
        ><span class="me-1">here</span>
        <IBiBoxArrowUpRight style="vertical-align: top" class="fs-6" /></a
      >.
    </div>
    <MustRegister
      v-else-if="!menuContext.isAuthenticated"
      title="You must be registerd and logged in to view other user's profiles."
      :menu-context="menuContext"
    />
    <div v-else-if="model.isPseudo">
      This is a placeholder user representing information from public lists, spotify playlist or
      libraries of friend's of music4dance.
    </div>
    <div v-else>
      <p>
        The User Profile feature is still being designed. We are considering including the option to
        have a free form "about me" section, a possibly more structured section to specify what
        dance styles you're interested and/or expert in and the ability to link back to your own
        website if you are a dance/music professional. Please contact us with any suggestions
        <a href="https://music4dance.blog/feedback/">here.</a>
      </p>
      <p v-if="isCurrentUser && !model.isPublic">
        Your are currently set to not share this profile page and your activity with others. This
        may have been becuase that was the default setting when you created your account. By
        changing to show your profile publicly, you'll help other members learn by being able to see
        what you've tagged songs with.
      </p>
      <p v-else-if="!model.isPublic">
        This user has opted out of displaying profile information in the future.
      </p>
      <p>
        To change your preferences, please go to your
        <a href="/identity/account/manage">my profile</a> page and check the box next to "Share my
        profile with other members"
      </p>
    </div>
  </div>
</template>

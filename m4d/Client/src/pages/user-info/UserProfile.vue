<template>
  <div>
    <div v-if="model.spotifyId">
      This is a proxy user based on a public spotify profile. View
      {{ model.displayName }}'s spotify profile
      <a :href="spotifyProfile">here.</a>
    </div>
    <must-register
      v-else-if="!isAuthenticated"
      title="You must be registerd and logged in to view other user's profiles."
    >
    </must-register>
    <div v-else-if="model.isPseudo">
      This is a placeholder user representing information from public lists,
      spotify playlist or libraries of friend's of music4dance.
    </div>
    <div v-else>
      <p>
        The User Profile feature is still being designed. We are considering
        including the option to have a free form "about me" section, a possibly
        more structured section to specify what dance styles you're interested
        and/or expert in and the ability to link back to your own website if you
        are a dance/music professional. Please contact us with any suggestions
        <a href="https://music4dance.blog/feedback/">here.</a>
      </p>
      <p v-if="!model.isPublic">
        This user has opted out of displaying profile information in the future.
      </p>
      <p>
        To change your preferences, please go to your
        <a href="/identity/account/manage">my profile</a> page.
      </p>
    </div>
  </div>
</template>

<script lang="ts">
import MustRegister from "@/components/MustRegister.vue";
import AdminTools from "@/mix-ins/AdminTools";
import { ProfileModel } from "@/model/ProfileModel";
import "reflect-metadata";
import { Component, Mixins, Prop } from "vue-property-decorator";

@Component({ components: { MustRegister } })
export default class UserProfile extends Mixins(AdminTools) {
  @Prop() private model!: ProfileModel;

  private get spotifyProfile(): string {
    return `https://open.spotify.com/user/${this.model.spotifyId}`;
  }
}
</script>

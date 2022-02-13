<template>
  <div>
    <div v-if="openAugment">
      <p>
        In order to add songs, you must first
        <a :href="register">register</a>
        and
        <a :href="login">sign in.</a>
      </p>
    </div>
    <div v-else>
      <p>
        Adding songs is a "by invitation" beta feature. If you are interested in
        adding songs please do the following:
      </p>
      <ul>
        <li>
          <a href="/identity/account/register">Create and account</a>
          (<a
            href="https://music4dance.blog/music4dance-help/account-management/"
            >Account Management Help</a
          >).
        </li>
        <li>
          Send email to
          <a href="mailto:info@music4dance.net">info@music4dance.net</a> or fill
          out our
          <a href="https://music4dance.blog/feedback/">feedback form</a> and ask
          to participate in the adding songs beta.
        </li>
      </ul>
      <p>
        If you've already signed up for the beta and are
        <a href="/identity/account/login?returnUrl=/song/augment">signed in</a>
        but still seeing this message, we are still evaluting your request,
        please be patient.
      </p>
    </div>
  </div>
</template>

<script lang="ts">
import { Component, Prop, Vue } from "vue-property-decorator";

@Component
export default class AugmentInfo extends Vue {
  @Prop() private openAugment!: boolean;
  @Prop() private id?: string;

  private get register(): string {
    return this.authenticate("register");
  }

  private get login(): string {
    return this.authenticate("login");
  }

  private authenticate(type: string): string {
    const idParam = this.id ? `?id=${this.id}` : "";
    return `/identity/account/${type}?returnUrl=/song/augment${idParam}`;
  }
}
</script>

/
<template>
  <div>
    <h2>{{ model.displayName }}'s Song Lists</h2>
    <ul>
      <user-link
        v-for="(link, index) in links"
        :key="index"
        :userName="model.userName"
        :displayName="model.displayName"
        :text="link.text"
        :type="link.type"
        :include="link.include"
        :count="link.count"
      ></user-link>
    </ul>
    <div>
      <p>
        The User Song List feature is still under development. We are
        considering including lists of dance styles that the user has tagged as
        well as hiding the current links if the user has not specified any
        songs. We could also include lists of arbitrary tags.
      </p>
      <p>
        The association of username with dances and tags that the user has voted
        on is fundamental to how music4dance is set up, so it isn't possible to
        hide the user's song lists on a per user basis without a major rework of
        the system. However, if you chose a user name that is more public that
        you prefer (like your real-life name, for instance) we would be happy to
        change that to something anonymous. Please
        <a href="https://music4dance.blog/feedback/">contact us</a> with your
        prefered user name and we'll make the change.
      </p>
    </div>
  </div>
</template>

<script lang="ts">
import { ProfileModel } from "@/model/ProfileModel";
import { Component, Prop, Vue } from "vue-property-decorator";
import UserLink from "./UserLink.vue";

interface ProfileLink {
  text: string;
  type: string;
  include: boolean;
  count?: number;
}

@Component({
  components: {
    UserLink,
  },
})
export default class UserList extends Vue {
  @Prop() private model!: ProfileModel;

  private get links(): ProfileLink[] {
    return this.allLinks.filter((l) => l.count);
  }

  private get allLinks(): ProfileLink[] {
    const info = this.model;

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
  }
}
</script>
